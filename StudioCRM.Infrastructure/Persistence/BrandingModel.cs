using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Persistence;

internal static class BrandingModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<OrganizationBrandingProfile>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.Revision).IsConcurrencyToken();
            e.HasData(new OrganizationBrandingProfile
            {
                Id = Guid.Parse("b5100000-0000-4000-8000-000000000001"), Name = "BSworkout"
            });
        });
        model.Entity<OrganizationBrandingVersion>(e =>
        {
            e.HasKey(v => new { v.ProfileId, v.Version });
            e.HasOne<OrganizationBrandingProfile>().WithMany().HasForeignKey(v => v.ProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<OrganizationBrandingAsset>(e =>
        {
            e.HasIndex(a => new { a.ProfileId, a.Id }).IsUnique();
            e.HasOne<OrganizationBrandingProfile>().WithMany().HasForeignKey(a => a.ProfileId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.Url).HasMaxLength(2000);
            e.Property(a => a.StorageKey).HasMaxLength(300);
        });
        model.Entity<OrganizationBrandingAudit>(e =>
        {
            e.HasIndex(a => new { a.ProfileId, a.Id });
            e.HasOne<OrganizationBrandingProfile>().WithMany().HasForeignKey(a => a.ProfileId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.Action).HasMaxLength(80);
            e.Property(a => a.Reason).HasMaxLength(1000);
        });
    }
}
