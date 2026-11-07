using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

// Builds a precise, user-facing reason why a participant's entry cannot be taken from a package when a session is completed
// (manually or by the auto-completion worker), e.g. "Anna Nowak: pakiet „X” był ważny do 31.10.2026, a sesja odbyła się 03.11.2026".
internal static class SessionPackageRejection
{
    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    public static async Task<CodedInvalidOperationException> NoUsablePackageAsync(
        StudioCRMDbContext context, Client client, Session session, SessionBillingType actualBillingType)
    {
        var group = actualBillingType == SessionBillingType.Group;
        var kind = group ? "grupowego" : "indywidualnego/semi";
        var candidates = await context.ClientPackages.AsNoTracking()
            .Where(cp => cp.ClientId == client.Id && (group
                ? cp.ExpectedBillingType == SessionBillingType.Group
                : cp.BillingMode == PackageBillingMode.SessionBased && cp.ExpectedBillingType != SessionBillingType.Group))
            .OrderByDescending(cp => cp.PurchaseDate).ThenByDescending(cp => cp.Id)
            .ToListAsync();
        var who = Name(client);
        var sessionDate = Date(session.StartAt);
        string reason;
        if (candidates.Count == 0)
            reason = $"{who}: klient nie ma żadnego pakietu {kind}. Przypisz pakiet albo rozlicz sesję bez odliczania wejścia.";
        else
        {
            // Explain the package that would most likely have been used: valid on the session date first, then the newest one.
            var p = candidates.FirstOrDefault(cp => cp.ValidUntil == null || cp.ValidUntil >= session.StartAt) ?? candidates[0];
            var name = $"„{p.Name}”";
            if (p.ValidUntil.HasValue && p.ValidUntil < session.StartAt)
                reason = $"{who}: pakiet {name} był ważny do {Date(p.ValidUntil.Value)}, a sesja odbyła się {sessionDate}. Przedłuż ważność pakietu albo przypisz nowy.";
            else if (p.ClosureDisposition != null)
                reason = $"{who}: pakiet {name} jest zamknięty{(p.ClosedAt.HasValue ? $" (od {Date(p.ClosedAt.Value)})" : "")}. Przypisz klientowi nowy pakiet.";
            else if (group && p.PaymentStatus != PaymentStatus.Paid)
                reason = $"{who}: pakiet grupowy {name} nie jest opłacony. Potwierdź wpłatę, aby odliczyć wejście.";
            else if (p.LocationId.HasValue && p.LocationId != session.LocationId)
            {
                var location = await context.Locations.AsNoTracking().Where(l => l.Id == p.LocationId).Select(l => l.Name).FirstOrDefaultAsync();
                reason = $"{who}: pakiet {name} dotyczy innej lokalizacji{(location != null ? $" ({location})" : "")} niż sesja. Zmień lokalizację pakietu albo sesji.";
            }
            else if (!p.IsActive && p.UsedSessions >= p.TotalSessions)
                reason = $"{who}: pakiet {name} jest wykorzystany ({p.UsedSessions}/{p.TotalSessions} wejść) i nie ma kolejnego aktywnego pakietu. Przypisz albo odnów pakiet.";
            else if (!p.IsActive)
                reason = $"{who}: pakiet {name} nie jest aktywny. Aktywuj go albo przypisz nowy pakiet.";
            else
                reason = $"{who}: brak pakietu {kind}, z którego można odliczyć wejście za sesję z {sessionDate}.";
        }
        return new CodedInvalidOperationException(SessionCorrectionErrorCodes.NoUsablePackage, reason);
    }

    public static CodedInvalidOperationException NoRemainingSessions(Client client, ClientPackage package, int charge) =>
        new(SessionCorrectionErrorCodes.PackageHasNoRemainingSessions,
            $"{Name(client)}: w pakiecie „{package.Name}” nie ma wolnych wejść (wykorzystano {Math.Max(package.UsedSessions, 0)}/{package.TotalSessions}, sesja wymaga {charge}). Odnów pakiet albo zwiększ liczbę wejść.");

    private static string Name(Client client) =>
        string.Join(" ", new[] { client.FirstName, client.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim() is { Length: > 0 } n ? n : $"Klient {client.Id}";

    private static string Date(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), StudioZone).ToString("dd.MM.yyyy", Pl);

    private static readonly TimeZoneInfo StudioZone = ResolveZone();
    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Europe/Warsaw", "Central European Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { } catch (InvalidTimeZoneException) { }
        return TimeZoneInfo.Utc;
    }
}
