using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class AvatarService : IAvatarService
{
    private const int DefaultMaxAvatarFileSizeMb = 5;

    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IObjectStorageService _storage;
    private readonly CloudflareR2Settings _settings;
    private readonly ILogger<AvatarService> _logger;

    public AvatarService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IObjectStorageService storage,
        IOptions<CloudflareR2Settings> options,
        ILogger<AvatarService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<AvatarDto> UploadCurrentUserAvatarAsync(
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync();
        return await UploadForUserAsync(user, content, fileName, contentType, contentLength, cancellationToken);
    }

    public async Task<AvatarDto> DeleteCurrentUserAvatarAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync();
        return await DeleteForUserAsync(user, cancellationToken);
    }

    public async Task<AvatarDto> UploadClientAvatarAsync(
        int clientId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        var user = await GetClientUserAsync(clientId);
        return await UploadForUserAsync(user, content, fileName, contentType, contentLength, cancellationToken);
    }

    public async Task<AvatarDto> DeleteClientAvatarAsync(
        int clientId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetClientUserAsync(clientId);
        return await DeleteForUserAsync(user, cancellationToken);
    }

    public async Task<AvatarDto> UploadTrainerAvatarAsync(
        int trainerId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsOwner)
            throw new InvalidOperationException("Only owner can manage trainer avatars.");

        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == trainerId, cancellationToken);

        if (trainer is null)
            throw new InvalidOperationException("Trainer not found.");

        return await UploadForUserAsync(trainer.User, content, fileName, contentType, contentLength, cancellationToken);
    }

    public async Task<AvatarDto> DeleteTrainerAvatarAsync(
        int trainerId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsOwner)
            throw new InvalidOperationException("Only owner can manage trainer avatars.");

        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == trainerId, cancellationToken);

        if (trainer is null)
            throw new InvalidOperationException("Trainer not found.");

        return await DeleteForUserAsync(trainer.User, cancellationToken);
    }

    private async Task<AvatarDto> UploadForUserAsync(
        User user,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken)
    {
        EnsurePublicBaseUrlConfigured();
        var normalizedContentType = ValidateFile(fileName, contentType, contentLength);
        var maxBytes = GetMaxAvatarFileSizeBytes();
        var bytes = await ReadFileAsync(content, maxBytes, cancellationToken);
        var oldAvatarUrl = user.AvatarUrl;
        var key = BuildStorageKey(user.Id, normalizedContentType);

        var storedObject = await _storage.UploadAsync(
            key,
            bytes,
            normalizedContentType,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(storedObject.Url))
            throw new InvalidOperationException("Cloudflare R2 public base URL is not configured.");

        user.AvatarUrl = storedObject.Url;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await DeletePreviousAvatarIfNeededAsync(oldAvatarUrl, key, cancellationToken);

        return new AvatarDto
        {
            UserId = user.Id,
            AvatarUrl = user.AvatarUrl
        };
    }

    private async Task<AvatarDto> DeleteForUserAsync(User user, CancellationToken cancellationToken)
    {
        var oldAvatarUrl = user.AvatarUrl;
        user.AvatarUrl = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await DeletePreviousAvatarIfNeededAsync(oldAvatarUrl, null, cancellationToken);

        return new AvatarDto
        {
            UserId = user.Id,
            AvatarUrl = null
        };
    }

    private async Task<User> GetCurrentUserAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value && u.IsActive);

        return user ?? throw new InvalidOperationException("User not found.");
    }

    private async Task<User> GetClientUserAsync(int clientId)
    {
        await EnsureCanManageClientAvatarAsync(clientId);

        var client = await _context.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted);

        if (client is null)
            throw new InvalidOperationException("Client not found.");

        return client.User ?? throw new InvalidOperationException("Client does not have user account yet.");
    }

    private async Task EnsureCanManageClientAvatarAsync(int clientId)
    {
        if (_currentUser.IsOwner)
            return;

        if (!_currentUser.IsTrainer)
            throw new InvalidOperationException("Current user cannot manage this client avatar.");

        var trainerId = await _context.Trainers
            .Where(t => t.UserId == _currentUser.UserId)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();

        if (!trainerId.HasValue)
            throw new InvalidOperationException("Trainer profile not found.");

        var hasAccess = await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainerId.Value && !c.IsDeleted);

        if (!hasAccess)
            throw new InvalidOperationException("Trainer does not have access to this client.");
    }

    private string ValidateFile(string fileName, string? contentType, long contentLength)
    {
        if (contentLength <= 0)
            throw new InvalidOperationException("Avatar file is empty.");

        var maxBytes = GetMaxAvatarFileSizeBytes();
        if (contentLength > maxBytes)
            throw new InvalidOperationException($"Avatar file cannot exceed {GetMaxAvatarFileSizeMb()} MB.");

        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("Avatar file name is required.");

        var normalizedContentType = contentType?.Trim() ?? string.Empty;
        if (!AllowedContentTypes.ContainsKey(normalizedContentType))
            throw new InvalidOperationException("Avatar file must be JPG, PNG, WEBP or GIF.");

        return normalizedContentType;
    }

    private async Task<byte[]> ReadFileAsync(
        Stream content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            totalRead += read;
            if (totalRead > maxBytes)
                throw new InvalidOperationException($"Avatar file cannot exceed {GetMaxAvatarFileSizeMb()} MB.");

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private async Task DeletePreviousAvatarIfNeededAsync(
        string? oldAvatarUrl,
        string? newKey,
        CancellationToken cancellationToken)
    {
        var oldKey = ResolveStorageKey(oldAvatarUrl);
        if (string.IsNullOrWhiteSpace(oldKey) || oldKey == newKey)
            return;

        try
        {
            await _storage.DeleteAsync(oldKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete old avatar file {AvatarKey}.", oldKey);
        }
    }

    private string? ResolveStorageKey(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return null;

        if (avatarUrl.StartsWith("avatars/", StringComparison.OrdinalIgnoreCase))
            return avatarUrl;

        if (string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            return null;

        var publicBaseUrl = _settings.PublicBaseUrl.TrimEnd('/') + "/";
        if (!avatarUrl.StartsWith(publicBaseUrl, StringComparison.OrdinalIgnoreCase))
            return null;

        return Uri.UnescapeDataString(avatarUrl[publicBaseUrl.Length..]);
    }

    private string BuildStorageKey(int userId, string contentType)
    {
        return $"avatars/users/{userId}/{Guid.NewGuid():N}{AllowedContentTypes[contentType]}";
    }

    private void EnsurePublicBaseUrlConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            throw new InvalidOperationException("Cloudflare R2 public base URL is not configured.");
    }

    private long GetMaxAvatarFileSizeBytes()
    {
        return GetMaxAvatarFileSizeMb() * 1024L * 1024L;
    }

    private int GetMaxAvatarFileSizeMb()
    {
        return _settings.MaxAvatarFileSizeMb > 0
            ? _settings.MaxAvatarFileSizeMb
            : DefaultMaxAvatarFileSizeMb;
    }
}
