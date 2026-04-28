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
    public DbSet<ClientPackage> ClientPackages => Set<ClientPackage>();
    public DbSet<ClientBalanceTransaction> ClientBalanceTransactions => Set<ClientBalanceTransaction>();

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionParticipant> SessionParticipants => Set<SessionParticipant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<TrainerLocation> TrainerLocations => Set<TrainerLocation>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<CalendarIntegration> CalendarIntegrations => Set<CalendarIntegration>();
    public DbSet<CalendarEventLink> CalendarEventLinks => Set<CalendarEventLink>();
    public DbSet<CalendarSubscription> CalendarSubscriptions => Set<CalendarSubscription>();
    public DbSet<ExternalCalendarEvent> ExternalCalendarEvents => Set<ExternalCalendarEvent>();

    public DbSet<TrainerRate> TrainerRates => Set<TrainerRate>();
    public DbSet<TrainerMonthlySettlement> TrainerMonthlySettlements => Set<TrainerMonthlySettlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================
        // USER / ROLES
        // =========================

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

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

        // =========================
        // TRAINERS
        // =========================

        modelBuilder.Entity<Trainer>()
            .HasOne(t => t.User)
            .WithOne(u => u.TrainerProfile)
            .HasForeignKey<Trainer>(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Trainer>()
            .HasQueryFilter(t => !t.IsDeleted);

        // =========================
        // TRAINER RATES
        // =========================

        modelBuilder.Entity<TrainerRate>()
            .HasOne(tr => tr.Trainer)
            .WithMany(t => t.Rates)
            .HasForeignKey(tr => tr.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainerRate>()
            .Property(tr => tr.Rate)
            .HasPrecision(10, 2);

        modelBuilder.Entity<TrainerRate>()
            .HasIndex(tr => new { tr.TrainerId, tr.SessionType, tr.IsActive });
        modelBuilder.Entity<Client>()
            .HasOne(c => c.Trainer)
            .WithMany()
            .HasForeignKey(c => c.TrainerId)
            .OnDelete(DeleteBehavior.SetNull);

        // =========================
        // TRAINER MONTHLY SETTLEMENTS
        // =========================

        modelBuilder.Entity<TrainerMonthlySettlement>()
            .HasOne(s => s.Trainer)
            .WithMany(t => t.MonthlySettlements)
            .HasForeignKey(s => s.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainerMonthlySettlement>()
            .Property(s => s.TotalAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<TrainerMonthlySettlement>()
            .Property(s => s.TotalHours)
            .HasPrecision(10, 2);

        modelBuilder.Entity<TrainerMonthlySettlement>()
            .HasIndex(s => new { s.TrainerId, s.Year, s.Month })
            .IsUnique();

        // =========================
        // CLIENTS
        // =========================

        modelBuilder.Entity<Client>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);

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

        modelBuilder.Entity<Client>()
            .HasOne(c => c.Location)
            .WithMany(l => l.Clients)
            .HasForeignKey(c => c.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => !c.IsDeleted);

        // =========================
        // PACKAGES
        // =========================

        modelBuilder.Entity<Package>()
            .Property(p => p.Price)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Package>()
            .HasQueryFilter(p => !p.IsDeleted);

        // =========================
        // CLIENT PACKAGES
        // =========================

        modelBuilder.Entity<ClientPackage>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.TotalPrice)
                .HasPrecision(18, 2);

            entity.Property(x => x.ExpectedUnitPrice)
                .HasPrecision(18, 2);

            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.ClientId, x.IsActive });
        });

        // =========================
        // CLIENT BALANCE TRANSACTIONS
        // =========================

        modelBuilder.Entity<ClientBalanceTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ClientPackage)
                .WithMany()
                .HasForeignKey(x => x.ClientPackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ClientId);
            entity.HasIndex(x => x.ClientPackageId);
            entity.HasIndex(x => x.SessionId);
        });

        // =========================
        // LOCATIONS
        // =========================

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

        // =========================
        // SESSIONS
        // =========================

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Trainer)
            .WithMany(t => t.Sessions)
            .HasForeignKey(s => s.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Location)
            .WithMany(l => l.Sessions)
            .HasForeignKey(s => s.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasQueryFilter(s => !s.IsDeleted);

        // =========================
        // SESSION PARTICIPANTS
        // =========================

        modelBuilder.Entity<SessionParticipant>(entity =>
        {
            entity.HasOne(sp => sp.Session)
                .WithMany(s => s.Participants)
                .HasForeignKey(sp => sp.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sp => sp.Client)
                .WithMany()
                .HasForeignKey(sp => sp.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sp => sp.Package)
                .WithMany()
                .HasForeignKey(sp => sp.PackageId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(sp => sp.ClientPackage)
                .WithMany()
                .HasForeignKey(sp => sp.ClientPackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(sp => sp.ExpectedUnitPrice)
                .HasPrecision(18, 2);

            entity.Property(sp => sp.ActualUnitPrice)
                .HasPrecision(18, 2);

            entity.Property(sp => sp.BalanceDifference)
                .HasPrecision(18, 2);

            entity.HasIndex(sp => new { sp.SessionId, sp.ClientId })
                .IsUnique();

            entity.HasIndex(sp => sp.ClientPackageId);
        });

        // =========================
        // AUTH TOKENS
        // =========================

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        // =========================
        // INVITATIONS
        // =========================

        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.Location)
            .WithMany()
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invitation>()
            .HasIndex(i => i.Token)
            .IsUnique();

        // =========================
        // CALENDAR
        // =========================

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