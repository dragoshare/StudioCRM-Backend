using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Persistence;

public partial class StudioCRMDbContext
{
    private readonly ICurrentUserService? _activityCurrentUser;
    public DbSet<ActivityLogEntry> ActivityLogEntries => Set<ActivityLogEntry>();

    private static void ConfigureActivityLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityLogEntry>(e =>
        {
            e.HasIndex(x => new { x.CreatedAt, x.Id });
            e.HasIndex(x => new { x.ActorUserId, x.CreatedAt });
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            e.HasIndex(x => x.ChangeSetId);
            e.Property(x => x.Source).HasMaxLength(20);
            e.Property(x => x.Operation).HasMaxLength(30);
            e.Property(x => x.EntityType).HasMaxLength(80);
            e.Property(x => x.EntityId).HasMaxLength(200);
        });
    }

    private List<ActivityLogCapture> PrepareActivityChanges()
    {
        if (ChangeTracker.Entries<ActivityLogEntry>().Any(e => e.State != EntityState.Unchanged))
            throw new InvalidOperationException("Activity history is read-only.");
        PrepareCalendarAddresses();
        NormalizeDateTimesToUtc();
        return ActivityLogCapture.Capture(ChangeTracker);
    }

    private int? ActivityActorId => _activityCurrentUser?.IsAuthenticated == true ? _activityCurrentUser.UserId : null;
    private IQueryable<string> ActorNameQuery(int? id) => Users.AsNoTracking().Where(u => u.Id == id)
        .Select(u => (u.FirstName + " " + u.LastName).Trim() == "" ? u.Email : (u.FirstName + " " + u.LastName).Trim());

    private static FormattableString InsertActivity(IEnumerable<ActivityLogEntry> entries) => $"""
        INSERT INTO "ActivityLogEntries"
        ("ChangeSetId", "CreatedAt", "ActorUserId", "ActorName", "Source", "Operation",
         "EntityType", "EntityId", "EntityLabel", "BeforeJson", "AfterJson", "ChangedFieldsJson")
        SELECT "ChangeSetId", "CreatedAt", "ActorUserId", "ActorName", "Source", "Operation",
               "EntityType", "EntityId", "EntityLabel", "BeforeJson", "AfterJson", "ChangedFieldsJson"
        FROM jsonb_to_recordset(CAST({JsonSerializer.Serialize(entries)} AS jsonb)) AS entries(
            "ChangeSetId" uuid, "CreatedAt" timestamptz, "ActorUserId" integer, "ActorName" text,
            "Source" text, "Operation" text, "EntityType" text, "EntityId" text, "EntityLabel" text,
            "BeforeJson" text, "AfterJson" text, "ChangedFieldsJson" text)
        """;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var pending = PrepareActivityChanges();
        if (pending.Count == 0) return base.SaveChanges(acceptAllChangesOnSuccess);
        using var owned = Database.CurrentTransaction == null ? Database.BeginTransaction() : null;
        var transaction = Database.CurrentTransaction!;
        var savepoint = "activity_" + Guid.NewGuid().ToString("N");
        if (owned == null) transaction.CreateSavepoint(savepoint);
        try
        {
            var actorId = ActivityActorId;
            var actorName = actorId.HasValue ? ActorNameQuery(actorId).FirstOrDefault() ?? $"User #{actorId}" : "System";
            var result = base.SaveChanges(false);
            var changeSet = Guid.NewGuid();
            var at = DateTime.UtcNow;
            Database.ExecuteSqlInterpolated(InsertActivity(pending.Select(c => c.Complete(changeSet, at, actorId, actorName))));
            if (owned != null) owned.Commit();
            else transaction.ReleaseSavepoint(savepoint);
            if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
            return result;
        }
        catch
        {
            if (owned != null) owned.Rollback();
            else transaction.RollbackToSavepoint(savepoint);
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var pending = PrepareActivityChanges();
        if (pending.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await using var owned = Database.CurrentTransaction == null ? await Database.BeginTransactionAsync(cancellationToken) : null;
        var transaction = Database.CurrentTransaction!;
        var savepoint = "activity_" + Guid.NewGuid().ToString("N");
        if (owned == null) await transaction.CreateSavepointAsync(savepoint, cancellationToken);
        try
        {
            var actorId = ActivityActorId;
            var actorName = actorId.HasValue ? await ActorNameQuery(actorId).FirstOrDefaultAsync(cancellationToken) ?? $"User #{actorId}" : "System";
            var result = await base.SaveChangesAsync(false, cancellationToken);
            var changeSet = Guid.NewGuid();
            var at = DateTime.UtcNow;
            await Database.ExecuteSqlInterpolatedAsync(InsertActivity(pending.Select(c => c.Complete(changeSet, at, actorId, actorName))), cancellationToken);
            if (owned != null) await owned.CommitAsync(cancellationToken);
            else await transaction.ReleaseSavepointAsync(savepoint, cancellationToken);
            if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
            return result;
        }
        catch
        {
            if (owned != null) await owned.RollbackAsync(CancellationToken.None);
            else await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
