using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Persistence;

public class StudioCRMDbContext : DbContext
{
    public StudioCRMDbContext(DbContextOptions<StudioCRMDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<TrainerLocation> TrainerLocations => Set<TrainerLocation>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<CalendarIntegration> CalendarIntegrations => Set<CalendarIntegration>();
    public DbSet<CalendarEventLink> CalendarEventLinks => Set<CalendarEventLink>();
    public DbSet<CalendarSubscription> CalendarSubscriptions => Set<CalendarSubscription>();
    public DbSet<ExternalCalendarEvent> ExternalCalendarEvents => Set<ExternalCalendarEvent>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
    .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        modelBuilder.Entity<Trainer>()
            .HasOne(t => t.User)
            .WithOne(u => u.TrainerProfile)
            .HasForeignKey<Trainer>(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Client>()
            .HasOne(c => c.Trainer)
            .WithMany(t => t.Clients)
            .HasForeignKey(c => c.TrainerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Client>()
            .HasOne(c => c.ActivePackage)
            .WithMany(p => p.Clients)
            .HasForeignKey(c => c.ActivePackageId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Trainer)
            .WithMany(t => t.Sessions)
            .HasForeignKey(s => s.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Client)
            .WithMany(c => c.Sessions)
            .HasForeignKey(s => s.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Package)
            .WithMany(p => p.Sessions)
            .HasForeignKey(s => s.PackageId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Trainer>()
            .Property(t => t.HourlyRate)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Package>()
            .Property(p => p.Price)
            .HasPrecision(10, 2);
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => !c.IsDeleted);

        modelBuilder.Entity<Trainer>()
            .HasQueryFilter(t => !t.IsDeleted);

        modelBuilder.Entity<Package>()
            .HasQueryFilter(p => !p.IsDeleted);

        modelBuilder.Entity<Session>()
            .HasQueryFilter(s => !s.IsDeleted);

        modelBuilder.Entity<TrainerLocation>()
            .HasKey(tl => new { tl.TrainerId, tl.LocationId });

        modelBuilder.Entity<TrainerLocation>()
            .HasOne(tl => tl.Trainer)
            .WithMany(t => t.TrainerLocations)
            .HasForeignKey(tl => tl.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainerLocation>()
            .HasOne(tl => tl.Location)
            .WithMany(l => l.TrainerLocations)
            .HasForeignKey(tl => tl.LocationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Client>()
            .HasOne(c => c.Location)
            .WithMany(l => l.Clients)
            .HasForeignKey(c => c.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Location)
            .WithMany(l => l.Sessions)
            .HasForeignKey(s => s.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.Location)
            .WithMany()
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invitation>()
            .HasIndex(i => i.Token)
            .IsUnique();
        modelBuilder.Entity<Client>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CalendarIntegration>()
            .HasOne(ci => ci.User)
            .WithMany()
            .HasForeignKey(ci => ci.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CalendarIntegration>()
            .HasIndex(ci => new { ci.UserId, ci.Provider })
            .IsUnique();

        modelBuilder.Entity<CalendarEventLink>()
            .HasOne(cel => cel.Session)
            .WithMany()
            .HasForeignKey(cel => cel.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CalendarEventLink>()
            .HasOne(cel => cel.CalendarIntegration)
            .WithMany()
            .HasForeignKey(cel => cel.CalendarIntegrationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CalendarEventLink>()
            .HasIndex(cel => new { cel.SessionId, cel.Provider })
            .IsUnique();

        modelBuilder.Entity<CalendarSubscription>()
            .HasOne(x => x.CalendarIntegration)
            .WithMany()
            .HasForeignKey(x => x.CalendarIntegrationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CalendarSubscription>()
            .HasIndex(x => x.SubscriptionId)
            .IsUnique();

        modelBuilder.Entity<ExternalCalendarEvent>()
            .HasOne(x => x.CalendarIntegration)
            .WithMany()
            .HasForeignKey(x => x.CalendarIntegrationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExternalCalendarEvent>()
            .HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ExternalCalendarEvent>()
            .HasIndex(x => new { x.CalendarIntegrationId, x.ExternalEventId })
            .IsUnique();
    }
}