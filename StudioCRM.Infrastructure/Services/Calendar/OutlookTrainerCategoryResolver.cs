using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public static class OutlookTrainerCategoryResolver
{
    public const string WarningPrefix = "Zastępstwo Outlook: ";

    public static string? ApplyToSession(Session session, Trainer? trainer, string? warning)
    {
        if (warning != null || trainer == null || trainer.Id == session.TrainerId) return warning;
        if (session.Status != "Planned" || session.CompletedAt != null)
            return WarningPrefix + "Zmiana prowadzącego rozliczonej lub anulowanej sesji wymaga korekty w CRM.";
        session.TrainerId = trainer.Id;
        return null;
    }

    public static async Task<(Trainer? Trainer, string? Warning)> ResolveAsync(
        StudioCRMDbContext context, string? categoriesJson, int locationId)
    {
        var trainers = await context.Trainers.Include(t => t.User)
            .Include(t => t.TrainerLocations)
            .Where(t => t.OutlookCategoryName != null).ToListAsync();
        return Resolve(categoriesJson, locationId, trainers);
    }

    public static (Trainer? Trainer, string? Warning) Resolve(
        string? categoriesJson, int locationId, IEnumerable<Trainer> trainers)
    {
        List<string> categories;
        try { categories = JsonSerializer.Deserialize<List<string>>(categoriesJson ?? "[]") ?? new(); }
        catch (JsonException) { return (null, WarningPrefix + "Nieprawidłowe kategorie wydarzenia."); }
        var names = categories.Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = trainers.Where(t => !string.IsNullOrWhiteSpace(t.OutlookCategoryName) &&
            names.Contains(t.OutlookCategoryName.Trim())).ToList();
        if (matches.Count == 0) return (null, null);
        if (matches.Count != 1)
            return (null, WarningPrefix + "Wybierz kategorię dokładnie jednego trenera.");
        var trainer = matches[0];
        if (trainer.IsDeleted || trainer.Status != "Active" || !trainer.User.IsActive ||
            !trainer.TrainerLocations.Any(l => l.LocationId == locationId))
            return (null, WarningPrefix + "Trener kategorii nie jest aktywny lub nie pracuje w lokalizacji sesji.");
        return (trainer, null);
    }
}
