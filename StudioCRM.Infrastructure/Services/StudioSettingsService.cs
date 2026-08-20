using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Settings;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class StudioSettingsService : IStudioSettingsService
{
    public const int FallbackDefaultPackageValidityDays = 45;
    public const int FallbackDefaultSessionDurationMinutes = 60;
    public const int FallbackDefaultPaymentDueDays = 7;

    private const string DefaultPackageValidityDaysKey = "DefaultPackageValidityDays";
    private const string DefaultSessionDurationMinutesKey = "DefaultSessionDurationMinutes";
    private const string DefaultPaymentDueDaysKey = "DefaultPaymentDueDays";

    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StudioSettingsService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<OwnerSettingsDto> GetOwnerSettingsAsync()
    {
        var settings = await _context.SystemSettings.ToListAsync();

        return new OwnerSettingsDto
        {
            DefaultPackageValidityDays = ReadInt(
                settings,
                DefaultPackageValidityDaysKey,
                FallbackDefaultPackageValidityDays),
            DefaultSessionDurationMinutes = ReadInt(
                settings,
                DefaultSessionDurationMinutesKey,
                FallbackDefaultSessionDurationMinutes),
            DefaultPaymentDueDays = ReadInt(
                settings,
                DefaultPaymentDueDaysKey,
                FallbackDefaultPaymentDueDays)
        };
    }

    public async Task<OwnerSettingsDto> UpdateOwnerSettingsAsync(UpdateOwnerSettingsDto request)
    {
        if (!_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Only owner can update system settings.");

        if (request.DefaultPackageValidityDays.HasValue)
            ValidateRange(
                request.DefaultPackageValidityDays.Value,
                1,
                730,
                nameof(request.DefaultPackageValidityDays));

        if (request.DefaultSessionDurationMinutes.HasValue)
            ValidateRange(
                request.DefaultSessionDurationMinutes.Value,
                15,
                480,
                nameof(request.DefaultSessionDurationMinutes));

        if (request.DefaultPaymentDueDays.HasValue)
            ValidateRange(
                request.DefaultPaymentDueDays.Value,
                0,
                365,
                nameof(request.DefaultPaymentDueDays));

        var now = DateTime.UtcNow;

        if (request.DefaultPackageValidityDays.HasValue)
            await UpsertAsync(DefaultPackageValidityDaysKey, request.DefaultPackageValidityDays.Value, now);

        if (request.DefaultSessionDurationMinutes.HasValue)
            await UpsertAsync(DefaultSessionDurationMinutesKey, request.DefaultSessionDurationMinutes.Value, now);

        if (request.DefaultPaymentDueDays.HasValue)
            await UpsertAsync(DefaultPaymentDueDaysKey, request.DefaultPaymentDueDays.Value, now);

        await _context.SaveChangesAsync();
        return await GetOwnerSettingsAsync();
    }

    private async Task UpsertAsync(string key, int value, DateTime now)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);

        if (setting is null)
        {
            setting = new SystemSetting
            {
                Key = key,
                CreatedAt = now
            };

            await _context.SystemSettings.AddAsync(setting);
        }

        setting.Value = value.ToString();
        setting.UpdatedAt = now;
        setting.UpdatedByUserId = _currentUser.UserId;
    }

    private static int ReadInt(List<SystemSetting> settings, string key, int fallback)
    {
        var value = settings.FirstOrDefault(s => s.Key == key)?.Value;
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static void ValidateRange(int value, int min, int max, string name)
    {
        if (value < min || value > max)
            throw new InvalidOperationException($"{name} must be between {min} and {max}.");
    }
}
