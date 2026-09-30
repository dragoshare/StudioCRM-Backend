using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Branding;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Branding;

public class BrandingService(StudioCRMDbContext db, IBrandingProfileContext scope,
    BrandingAccess access, IObjectStorageService storage) : IBrandingService
{
    private Guid ProfileId => scope.ProfileId;
    private static string Serialize(BrandingSettingsDto settings) => JsonSerializer.Serialize(settings);
    private static BrandingSettingsDto Read(string? json) =>
        json == null ? new() : JsonSerializer.Deserialize<BrandingSettingsDto>(json) ?? new();

    public async Task<PublicBrandingDto> GetPublishedAsync()
    {
        var profile = await db.OrganizationBrandingProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == ProfileId);
        // Unconfigured profiles return the neutral theme, never another organization's settings.
        return await ToPublicAsync(Read(profile?.PublishedJson), profile?.PublishedVersion ?? 0,
            profile?.PublishedJson != null);
    }

    public async Task<BrandingEditorDto> GetDraftAsync()
    {
        await access.RequireAdminAsync();
        return await EditorAsync(await ProfileAsync());
    }

    public async Task<BrandingEditorDto> SaveDraftAsync(SaveBrandingDraftRequest request)
    {
        var actor = await access.RequireAdminAsync();
        var profile = await ProfileAsync();
        CheckRevision(profile, request);
        await ValidateSettingsAsync(request.Settings);
        var before = profile.DraftJson;
        profile.DraftJson = Serialize(request.Settings);
        profile.Revision++;
        Audit(actor, "DraftSaved", request.Reason, before, profile.DraftJson);
        await db.SaveChangesAsync();
        return await EditorAsync(profile);
    }

    public async Task<PublicBrandingDto> PublishAsync(BrandingChangeRequest request)
    {
        var actor = await access.RequireAdminAsync();
        var profile = await ProfileAsync();
        CheckRevision(profile, request);
        await ValidateSettingsAsync(Read(profile.DraftJson));
        var before = profile.PublishedJson ?? "{}";
        profile.PublishedJson = profile.DraftJson;
        profile.PublishedVersion++;
        profile.Revision++;
        db.OrganizationBrandingVersions.Add(new()
        {
            ProfileId = ProfileId, Version = profile.PublishedVersion,
            SettingsJson = profile.PublishedJson, ActorUserId = actor
        });
        Audit(actor, "Published", request.Reason, before, profile.PublishedJson);
        // EF executes the profile, immutable version and audit writes in one transaction.
        await db.SaveChangesAsync();
        return await ToPublicAsync(Read(profile.PublishedJson), profile.PublishedVersion, true);
    }

    public async Task<BrandingEditorDto> RestoreDraftAsync(RestoreBrandingRequest request)
    {
        var actor = await access.RequireAdminAsync();
        var profile = await ProfileAsync();
        CheckRevision(profile, request);
        var version = await db.OrganizationBrandingVersions.AsNoTracking()
            .SingleOrDefaultAsync(v => v.ProfileId == ProfileId && v.Version == request.Version)
            ?? throw new KeyNotFoundException("Branding version not found.");
        await ValidateSettingsAsync(Read(version.SettingsJson));
        var before = profile.DraftJson;
        profile.DraftJson = version.SettingsJson;
        profile.Revision++;
        Audit(actor, "VersionRestoredToDraft", request.Reason, before, profile.DraftJson);
        await db.SaveChangesAsync();
        return await EditorAsync(profile);
    }

    public async Task<List<BrandingVersionDto>> GetVersionsAsync(int page, int pageSize)
    {
        await access.RequireAdminAsync();
        CheckPage(page, pageSize);
        var versions = await db.OrganizationBrandingVersions.AsNoTracking()
            .Where(v => v.ProfileId == ProfileId).OrderByDescending(v => v.Version)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return versions.Select(v => new BrandingVersionDto(v.Version, v.ActorUserId, v.CreatedAt, Read(v.SettingsJson))).ToList();
    }

    public async Task<List<BrandingAuditDto>> GetAuditAsync(int page, int pageSize)
    {
        await access.RequireAdminAsync();
        CheckPage(page, pageSize);
        return await db.OrganizationBrandingAudits.AsNoTracking().Where(a => a.ProfileId == ProfileId)
            .OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new BrandingAuditDto(a.Id, a.ActorUserId, a.Action, a.Reason, a.CreatedAt, a.BeforeJson, a.AfterJson))
            .ToListAsync();
    }

    public async Task<BrandingAssetDto> UploadAsync(Stream stream, CancellationToken ct)
    {
        var actor = await access.RequireAdminAsync();
        await ProfileAsync();
        var bytes = await BrandingPng.ReadAndValidateAsync(stream, ct);
        var id = Guid.NewGuid();
        var key = $"branding/{ProfileId:N}/{id:N}.png";
        var stored = await storage.UploadAsync(key, bytes, "image/png", ct);
        if (!Uri.TryCreate(stored.Url, UriKind.Absolute, out var url) || url.Scheme != "https")
            throw new InvalidOperationException("Branding requires an HTTPS public storage URL.");
        var asset = new OrganizationBrandingAsset { Id = id, ProfileId = ProfileId, Url = url.AbsoluteUri, StorageKey = key };
        db.OrganizationBrandingAssets.Add(asset);
        Audit(actor, "AssetUploaded", "Uploaded PNG", "{}", JsonSerializer.Serialize(new { asset.Id, asset.StorageKey }));
        await db.SaveChangesAsync(ct);
        return new(id, asset.Url);
    }

    private async Task<OrganizationBrandingProfile> ProfileAsync() =>
        await db.OrganizationBrandingProfiles.SingleOrDefaultAsync(p => p.Id == ProfileId)
            ?? throw new KeyNotFoundException("Branding profile is not configured.");

    private async Task<BrandingEditorDto> EditorAsync(OrganizationBrandingProfile profile) =>
        new(ProfileId, profile.Revision, profile.PublishedVersion, Read(profile.DraftJson),
            await ToPublicAsync(Read(profile.DraftJson), profile.PublishedVersion, true));

    private async Task<PublicBrandingDto> ToPublicAsync(BrandingSettingsDto settings, int version, bool customized)
    {
        var ids = AssetIds(settings);
        var assets = await db.OrganizationBrandingAssets.AsNoTracking()
            .Where(a => a.ProfileId == ProfileId && ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Url);
        string? Url(Guid? id) => id.HasValue && assets.TryGetValue(id.Value, out var value) ? value : null;
        return new(ProfileId, version, customized, settings.ApplicationName, settings.WelcomeText,
            settings.PrimaryColor, settings.AccentColor, Url(settings.LightLogoAssetId),
            Url(settings.DarkLogoAssetId), Url(settings.IconAssetId), Url(settings.LoginImageAssetId));
    }

    public static void ValidateSettings(BrandingSettingsDto settings)
    {
        if (settings == null) throw new InvalidOperationException("Settings are required.");
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
        if (string.IsNullOrWhiteSpace(settings.ApplicationName))
            throw new InvalidOperationException("Application name is required.");
    }

    private async Task ValidateSettingsAsync(BrandingSettingsDto settings)
    {
        try { ValidateSettings(settings); }
        catch (ValidationException ex) { throw new InvalidOperationException(ex.Message); }
        var ids = AssetIds(settings);
        if (await db.OrganizationBrandingAssets.CountAsync(a => a.ProfileId == ProfileId && ids.Contains(a.Id)) != ids.Length)
            throw new InvalidOperationException("A branding asset does not belong to this profile.");
    }
    private static Guid[] AssetIds(BrandingSettingsDto s) =>
        new[] { s.LightLogoAssetId, s.DarkLogoAssetId, s.IconAssetId, s.LoginImageAssetId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();

    private static void CheckRevision(OrganizationBrandingProfile profile, BrandingChangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            throw new InvalidOperationException("Provide a reason of 1 to 1000 characters.");
        if (profile.Revision != request.ExpectedRevision)
            throw new DbUpdateConcurrencyException("Branding changed. Refresh the draft.");
    }
    private static void CheckPage(int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
            throw new InvalidOperationException("Invalid pagination.");
    }
    private void Audit(int actor, string action, string reason, string before, string after) =>
        db.OrganizationBrandingAudits.Add(new()
        {
            ProfileId = ProfileId, ActorUserId = actor, Action = action,
            Reason = reason.Trim(), BeforeJson = before, AfterJson = after
        });
}
