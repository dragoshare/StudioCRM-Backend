using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Persistence;
internal static class OrganizationAdministrationModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Organization>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<Organization>().Property(x => x.Status).HasMaxLength(30);
        model.Entity<OrganizationMembership>(e =>
        {
            e.HasKey(x => new { x.OrganizationId, x.UserId });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("OrganizationMemberships", t => t.HasCheckConstraint("CK_OrganizationMemberships_Role", "\"IsOwner\" OR \"IsTrainer\""));
        });
        model.Entity<PlatformAuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt, x.Id });
            e.Property(x => x.Action).HasMaxLength(80);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SubjectUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
