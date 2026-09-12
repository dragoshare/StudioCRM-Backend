using StudioCRM.Application.DTOs.Notifications;

namespace StudioCRM.Application.Common;

public static class NotificationCategories
{
    public const string Fallback = "system";

    public static IReadOnlyList<NotificationCategoryDto> All { get; } = Array.AsReadOnly(new[]
    {
        new NotificationCategoryDto("payments", "Płatności"),
        new NotificationCategoryDto("packages", "Pakiety"),
        new NotificationCategoryDto("group_classes", "Zajęcia grupowe"),
        new NotificationCategoryDto("registrations", "Rejestracje"),
        new NotificationCategoryDto("schedule", "Grafik"),
        new NotificationCategoryDto("invitations", "Zaproszenia"),
        new NotificationCategoryDto("trainers", "Trenerzy"),
        new NotificationCategoryDto(Fallback, "System")
    });

    private static readonly IReadOnlyDictionary<string, string> ByType = new Dictionary<string, string>
    {
        ["PaymentPendingConfirmation"] = "payments",
        ["PackagePaymentRequired"] = "payments",
        ["PackageEndedPaymentRequired"] = "payments",
        ["GroupPackagePaymentRequired"] = "payments",
        ["GroupPackagePaymentConfirmed"] = "payments",
        ["ClientWithoutActivePackage"] = "packages",
        ["GroupPackageActivated"] = "packages",
        ["PackageEndingSoon"] = "packages",
        ["RenewalCancellationRequested"] = "packages",
        ["SessionNotSyncedToOutlook"] = "schedule",
        ["GroupClassBooked"] = "group_classes",
        ["GroupClassBookingCancelled"] = "group_classes",
        ["GroupClassCancelled"] = "group_classes",
        ["GroupClassRescheduled"] = "group_classes",
        ["GroupClassReminder"] = "group_classes",
        ["PublicGroupClientRegistered"] = "registrations",
        ["InvitationExpired"] = "invitations",
        ["InvitationPending"] = "invitations",
        ["ClientInvitationAccepted"] = "invitations",
        ["TrainerContractNeedsRenewal"] = "trainers",
        ["TrainerContractEndingSoon"] = "trainers",
        ["TrainerSettlementReminder"] = "trainers",
        ["LocationLimitExceeded"] = Fallback
    };

    public static string Resolve(string type) => ByType.TryGetValue(type, out var category) ? category : Fallback;

    public static string? Normalize(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;
        var normalized = category.Trim().ToLowerInvariant();
        if (!All.Any(x => x.Key == normalized))
            throw new ArgumentException("Unknown notification category. Use GET /api/Notifications/categories.", nameof(category));
        return normalized;
    }

    public static string[] TypesFor(string category) => ByType.Where(x => x.Value == category).Select(x => x.Key).ToArray();
    public static string[] NonSystemTypes() => ByType.Where(x => x.Value != Fallback).Select(x => x.Key).ToArray();
}
