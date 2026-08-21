using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.TrainingPlans;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainingPlanFileService : ITrainingPlanFileService
{
    private const int DefaultMaxFileSizeMb = 20;

    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IObjectStorageService _storage;
    private readonly CloudflareR2Settings _settings;
    private readonly ILogger<TrainingPlanFileService> _logger;

    public TrainingPlanFileService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IObjectStorageService storage,
        IOptions<CloudflareR2Settings> options,
        ILogger<TrainingPlanFileService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<TrainingPlanDto> UploadAsync(
        int clientId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        if (contentLength <= 0)
            throw new InvalidOperationException("Training plan file is empty.");

        var maxBytes = GetMaxTrainingPlanFileSizeBytes();
        if (contentLength > maxBytes)
            throw new InvalidOperationException($"Training plan file cannot exceed {GetMaxTrainingPlanFileSizeMb()} MB.");

        var originalFileName = NormalizeFileName(fileName);
        var client = await GetClientAsync(clientId);
        var oldFileId = client.TrainingPlanFileId;
        var key = BuildStorageKey(clientId, originalFileName);
        var bytes = await ReadFileAsync(content, maxBytes, cancellationToken);

        var storedObject = await _storage.UploadAsync(
            key,
            bytes,
            contentType ?? "application/octet-stream",
            cancellationToken);

        client.TrainingPlanFileId = storedObject.Key;
        client.TrainingPlanFileName = originalFileName;
        client.TrainingPlanUrl = storedObject.Url;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await DeletePreviousFileIfNeededAsync(oldFileId, storedObject.Key, cancellationToken);

        return MapTrainingPlan(client);
    }

    public async Task<TrainingPlanFileDownloadDto> DownloadAsync(
        int clientId,
        CancellationToken cancellationToken = default)
    {
        await EnsureStaffAccessToClientAsync(clientId);
        var client = await GetClientAsync(clientId);
        return await DownloadForClientAsync(client, cancellationToken);
    }

    public async Task<TrainingPlanFileDownloadDto> DownloadCurrentClientAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await GetCurrentClientAsync();
        return await DownloadForClientAsync(client, cancellationToken);
    }

    public async Task<TrainingPlanDto> DeleteAsync(
        int clientId,
        CancellationToken cancellationToken = default)
    {
        await EnsureStaffAccessToClientAsync(clientId);
        var client = await GetClientAsync(clientId);
        var oldFileId = client.TrainingPlanFileId;

        client.TrainingPlanFileId = null;
        client.TrainingPlanFileName = null;
        client.TrainingPlanUrl = null;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldFileId))
        {
            try
            {
                await _storage.DeleteAsync(oldFileId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete training plan file {FileId}.", oldFileId);
            }
        }

        return MapTrainingPlan(client);
    }

    private async Task<TrainingPlanFileDownloadDto> DownloadForClientAsync(
        Client client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(client.TrainingPlanFileId))
            throw new InvalidOperationException("Training plan file is not uploaded.");

        var file = await _storage.DownloadAsync(
            client.TrainingPlanFileId,
            client.TrainingPlanFileName,
            cancellationToken);

        return new TrainingPlanFileDownloadDto
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            Content = file.Content
        };
    }

    private async Task<Client> GetClientAsync(int clientId)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted);
        return client ?? throw new InvalidOperationException("Client not found.");
    }

    private async Task<Client> GetCurrentClientAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.UserId == _currentUser.UserId.Value && !c.IsDeleted);

        return client ?? throw new InvalidOperationException("Client profile not found.");
    }

    private async Task EnsureStaffAccessToClientAsync(int clientId)
    {
        if (_currentUser.IsOwner)
            return;

        if (!_currentUser.IsTrainer)
            throw new InvalidOperationException("Current user cannot manage this training plan.");

        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);

        if (trainer is null)
            throw new InvalidOperationException("Trainer profile not found.");

        var hasAccess = await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainer.Id && !c.IsDeleted);

        if (!hasAccess)
            throw new InvalidOperationException("Trainer does not have access to this client.");
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
                throw new InvalidOperationException($"Training plan file cannot exceed {GetMaxTrainingPlanFileSizeMb()} MB.");

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private async Task DeletePreviousFileIfNeededAsync(
        string? oldFileId,
        string newFileId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oldFileId) || oldFileId == newFileId)
            return;

        try
        {
            await _storage.DeleteAsync(oldFileId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete old training plan file {FileId}.", oldFileId);
        }
    }

    private long GetMaxTrainingPlanFileSizeBytes()
    {
        return GetMaxTrainingPlanFileSizeMb() * 1024L * 1024L;
    }

    private int GetMaxTrainingPlanFileSizeMb()
    {
        var configuredMb = _settings.MaxTrainingPlanFileSizeMb > 0
            ? _settings.MaxTrainingPlanFileSizeMb
            : DefaultMaxFileSizeMb;

        return configuredMb;
    }

    private static string BuildStorageKey(int clientId, string fileName)
    {
        return $"training-plans/clients/{clientId}/{Guid.NewGuid():N}-{SanitizeStorageFileName(fileName)}";
    }

    private static string NormalizeFileName(string fileName)
    {
        var normalized = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Training plan file name is required.");

        return normalized;
    }

    private static string SanitizeStorageFileName(string fileName)
    {
        var builder = new System.Text.StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(IsSafeFileNameCharacter(character) ? character : '-');
        }

        var sanitized = builder.ToString().Trim('-', '.');
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "training-plan";

        return sanitized.Length <= 120
            ? sanitized
            : sanitized[^120..];
    }

    private static bool IsSafeFileNameCharacter(char character)
    {
        return character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '.'
            or '-'
            or '_';
    }

    private static TrainingPlanDto MapTrainingPlan(Client client)
    {
        return new TrainingPlanDto
        {
            ClientId = client.Id,
            GoogleDriveFolderId = client.GoogleDriveFolderId,
            GoogleDriveFolderUrl = BuildDriveFolderUrl(client.GoogleDriveFolderId),
            FileId = client.TrainingPlanFileId,
            FileName = client.TrainingPlanFileName,
            Url = client.TrainingPlanUrl
        };
    }

    private static string? BuildDriveFolderUrl(string? folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            return null;

        if (folderId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            folderId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return folderId;

        return $"https://drive.google.com/drive/folders/{Uri.EscapeDataString(folderId)}";
    }
}
