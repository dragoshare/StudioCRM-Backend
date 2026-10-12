using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Persistence;

public partial class StudioCRMDbContext
{
    public static readonly Guid LegacyOrganizationId = Guid.Parse("b5100000-0000-4000-8000-000000000002");
    // Immutable per DbContext. HTTP tenant switching is intentionally not enabled by this foundation.
    public Guid OrganizationId { get; }
    internal static readonly HashSet<Type> OrganizationDataTypes = new()
    {
        typeof(Client), typeof(Trainer), typeof(Location), typeof(LegalEntity), typeof(Package), typeof(Session),
        typeof(ClientPackage), typeof(ClientPayment), typeof(ClientBalanceTransaction), typeof(ClientAuditEntry),
        typeof(ClientLocationMembership), typeof(ClientEmailChangeRequest), typeof(ClientMilestone),
        typeof(SessionParticipant), typeof(SessionCorrection), typeof(CompanyExpense), typeof(TrainerRate),
        typeof(TrainerContract), typeof(TrainerContractLocation), typeof(TrainerLocation), typeof(TrainerMonthlySettlement),
        typeof(CalendarIntegration), typeof(CalendarSubscription), typeof(CalendarEventLink), typeof(ExternalCalendarEvent),
        typeof(OutlookOAuthChallenge), typeof(Invitation), typeof(Notification), typeof(SystemSetting),
        typeof(UserLegalConsent), typeof(PaymentProviderAccount), typeof(GroupEnrollmentRequest), typeof(MilestoneDefinition), typeof(ActivityLogEntry)
    };

    private void ConfigureOrganizationData(ModelBuilder model)
    {
        foreach (var type in OrganizationDataTypes)
        {
            var entity = model.Entity(type);
            entity.Property<Guid>("OrganizationId").HasDefaultValue(LegacyOrganizationId).HasValueGenerator<OrganizationValueGenerator>().IsConcurrencyToken();

            entity.Property<Guid>("OrganizationId").Metadata.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Save);
            var parameter = Expression.Parameter(type, "row");
            var property = Expression.Call(typeof(EF), nameof(EF.Property), new[] { typeof(Guid) }, parameter, Expression.Constant("OrganizationId"));
            var tenant = Expression.Equal(property, Expression.Property(Expression.Constant(this), nameof(OrganizationId)));
            var oldFilter = entity.Metadata.GetQueryFilter();
            Expression body = oldFilter == null ? tenant : Expression.AndAlso(
                new ReplaceParameter(oldFilter.Parameters[0], parameter).Visit(oldFilter.Body)!, tenant);
            entity.HasQueryFilter(Expression.Lambda(body, parameter));
            entity.HasIndex("OrganizationId");
            entity.HasOne(typeof(Organization)).WithMany().HasForeignKey("OrganizationId").OnDelete(DeleteBehavior.Restrict);
        }
        // Additional composite constraints preserve existing navigation and delete behavior while
        // rejecting cross-organization links even in raw SQL and bulk operations.
        var relations = model.Model.GetEntityTypes().Where(e => OrganizationDataTypes.Contains(e.ClrType))
            .SelectMany(e => e.GetForeignKeys()).Where(f => OrganizationDataTypes.Contains(f.PrincipalEntityType.ClrType)).ToArray();
        foreach (var fk in relations)
        {
            var dependent = fk.Properties.Select(p => p.Name).Append("OrganizationId").ToArray();
            var principal = fk.PrincipalKey.Properties.Select(p => p.Name).Append("OrganizationId").ToArray();
            model.Entity(fk.DeclaringEntityType.ClrType).HasOne(fk.PrincipalEntityType.ClrType).WithMany()
                .HasForeignKey(dependent).HasPrincipalKey(principal).OnDelete(DeleteBehavior.NoAction);
        }
    }

    private void PrepareOrganizationWrites()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => OrganizationDataTypes.Contains(e.Metadata.ClrType)
                     && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var property = entry.Property("OrganizationId");
            if (entry.State == EntityState.Added && Equals(property.CurrentValue, Guid.Empty))
                property.CurrentValue = OrganizationId;
            if (entry.State == EntityState.Added) property.IsTemporary = false;
            if (!Equals(property.CurrentValue, OrganizationId) ||
                (entry.State != EntityState.Added && !Equals(property.OriginalValue, OrganizationId)))
                throw new InvalidOperationException("Cross-organization write is forbidden.");
        }
    }

    private sealed class OrganizationValueGenerator : Microsoft.EntityFrameworkCore.ValueGeneration.ValueGenerator<Guid>
    {
        public override bool GeneratesTemporaryValues => false;
        public override Guid Next(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
            => ((StudioCRMDbContext)entry.Context).OrganizationId;
    }

    private sealed class ReplaceParameter(ParameterExpression source, ParameterExpression destination) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? destination : base.VisitParameter(node);
    }
}


