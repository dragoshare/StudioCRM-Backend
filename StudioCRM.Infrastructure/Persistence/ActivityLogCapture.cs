using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Persistence;

internal sealed class ActivityLogCapture
{
    // Explicit allowlist: new properties, credentials and integration payloads are not logged implicitly.
    internal static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>
    {
        [nameof(Session)] = Split("Title Note StartAt EndAt TrainerId LocationId StudioRoom Status IsPubliclyBookable PublicCapacity EventRules RegistrationClosesBeforeMinutes CancellationClosesBeforeMinutes PlannedSessionType ActualSessionType ActualParticipantsCount CompletedAt IsDeleted IsRecurring RecurringGroupId"),
        [nameof(SessionParticipant)] = Split("SessionId ClientId ClientPackageId PackageId AttendanceStatus IsCountedFromPackage CountsAgainstPackage SessionsCharged PlannedBillingType ActualBillingType ExpectedUnitPrice ActualUnitPrice BalanceDifference Note"),
        [nameof(Client)] = Split("FirstName LastName Email CalendarEmail PhoneNumber LocationId TrainerId UserId Status IsDeleted PortalAccessBlocked ActivePackageId NextPackageId SubscriptionAutoRenewEnabled BillingStatus TrainingStartDate Goal Notes Source"),
        [nameof(Trainer)] = Split("UserId Bio Phone Status TeamJoinedDate OutlookCategoryName OutlookCategoryColor IsDeleted"),
        [nameof(User)] = Split("FirstName LastName Email IsActive"),
        [nameof(TrainerLocation)] = Split("TrainerId LocationId"),
        [nameof(ClientLocationMembership)] = Split("ClientId LocationId IsHomeLocation GroupAccessEnabled Source"),
        [nameof(Package)] = Split("Name Description Price Currency SessionsLimit SessionsPerWeek DurationDays BillingType ParticipantsCount LocationId IsPubliclyAvailable IsActive IsDeleted"),
        [nameof(ClientPackage)] = Split("ClientId PackageId Name TotalSessions SessionsPerWeek UsedSessions TotalPrice OriginalPrice BalanceApplied AmountPaid ExpectedUnitPrice Currency LocationId ExpectedBillingType PaymentStatus PurchaseDate ValidUntil PaidAt PaymentDueDate ActivatedAt ActivationMode PreviousClientPackageId RenewalSource IsActive ClosureDisposition ClosedAt ClosureReason RefundAmount RefundConfirmedAt RefundReference"),
        [nameof(ClientPayment)] = Split("ClientId ClientPackageId LocationId LegalEntityId Amount AppliedToPackageAmount BalanceCreditAmount Currency Method Source Status PaymentDate ConfirmedAt ConfirmedByUserId RejectedAt RejectedByUserId RejectionReason ReversedAt ReversedByUserId ReversalReason Note ReceiptStatus ReceiptNumber ReceiptRequired ReceiptIssuedAt ReceiptSentAt ProviderFeeAmount ProviderNetAmount ProviderSettledAt ProviderPayoutDate"),
        [nameof(ClientBalanceTransaction)] = Split("ClientId ClientPackageId SessionId Amount Type Description"),
        [nameof(TrainerRate)] = Split("TrainerId SessionType Rate ValidFrom ValidTo IsActive"),
        [nameof(TrainerContract)] = Split("TrainerId ContractType ContractNumber SignedAt ValidFrom ValidTo Notes IsActive"),
        [nameof(TrainerContractLocation)] = Split("TrainerContractId LocationId"),
        [nameof(TrainerMonthlySettlement)] = Split("TrainerId Year Month TotalAmount TotalHours TotalSessions IsPaid PaidAt PaidByUserId")
    };

    private static string[] Split(string fields) => fields.Split(' ');
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly EntityEntry entry;
    private readonly EntityState state;
    private readonly Dictionary<string, object?> before;
    private readonly string[] fields;
    private readonly string[] changed;

    private ActivityLogCapture(EntityEntry entry, string[] fields, string[] changed)
    {
        this.entry = entry;
        this.fields = fields;
        this.changed = changed;
        state = entry.State;
        before = Snapshot(original: true);
    }

    internal static List<ActivityLogCapture> Capture(ChangeTracker tracker)
    {
        tracker.DetectChanges();
        var result = new List<ActivityLogCapture>();
        foreach (var entry in tracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted) ||
                !Fields.TryGetValue(entry.Metadata.ClrType.Name, out var allowed)) continue;
            var fields = allowed.Where(n => entry.Metadata.FindProperty(n) != null)
                .Concat(entry.Metadata.FindPrimaryKey()!.Properties.Select(p => p.Name)).Distinct().ToArray();
            var changed = fields.Where(n => entry.State != EntityState.Modified ||
                (entry.Property(n).IsModified && !Equals(entry.Property(n).OriginalValue, entry.Property(n).CurrentValue))).ToArray();
            if (changed.Length > 0) result.Add(new(entry, fields, changed));
        }
        return result;
    }

    private Dictionary<string, object?> Snapshot(bool original) => fields.ToDictionary(n => n,
        n => original ? entry.Property(n).OriginalValue : entry.Property(n).CurrentValue);

    internal ActivityLogEntry Complete(Guid changeSetId, DateTime at, int? actorId, string actorName)
    {
        var after = state == EntityState.Deleted ? null : Snapshot(original: false);
        var values = after ?? before;
        var keys = entry.Metadata.FindPrimaryKey()!.Properties;
        var id = keys.Count == 1 ? Convert.ToString(values[keys[0].Name], CultureInfo.InvariantCulture)!
            : string.Join(";", keys.Select(k => $"{k.Name}={Convert.ToString(values[k.Name], CultureInfo.InvariantCulture)}"));
        var label = values.GetValueOrDefault("Name")?.ToString() ?? values.GetValueOrDefault("Title")?.ToString()
            ?? $"{values.GetValueOrDefault("FirstName")} {values.GetValueOrDefault("LastName")}".Trim();
        var operation = state switch { EntityState.Added => "Created", EntityState.Deleted => "Deleted", _ => "Updated" };
        if (state == EntityState.Modified && changed.Contains("IsDeleted"))
            operation = Equals(values["IsDeleted"], true) ? "Archived" : "Restored";
        return new ActivityLogEntry
        {
            ChangeSetId = changeSetId, CreatedAt = at, ActorUserId = actorId, ActorName = actorName,
            Source = actorId.HasValue ? "User" : "System", Operation = operation,
            EntityType = entry.Metadata.ClrType.Name, EntityId = id,
            EntityLabel = string.IsNullOrWhiteSpace(label) ? $"{entry.Metadata.ClrType.Name} #{id}" : label,
            BeforeJson = state == EntityState.Added ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after == null ? null : JsonSerializer.Serialize(after, JsonOptions),
            ChangedFieldsJson = JsonSerializer.Serialize(changed.Select(JsonNamingPolicy.CamelCase.ConvertName))
        };
    }
}
