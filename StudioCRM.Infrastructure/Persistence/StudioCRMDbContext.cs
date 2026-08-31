using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Persistence;

public class StudioCRMDbContext : DbContext
{
    private const string WindowsStudioTimeZone = "Central European Standard Time";

    public StudioCRMDbContext(DbContextOptions<StudioCRMDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<TrainerContract> TrainerContracts => Set<TrainerContract>();
    public DbSet<TrainerContractLocation> TrainerContractLocations => Set<TrainerContractLocation>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<ClientPackage> ClientPackages => Set<ClientPackage>();
    public DbSet<ClientBalanceTransaction> ClientBalanceTransactions => Set<ClientBalanceTransaction>();
    public DbSet<ClientPayment> ClientPayments => Set<ClientPayment>();
    public DbSet<CompanyExpense> CompanyExpenses => Set<CompanyExpense>();
    public DbSet<ClientEmailChangeRequest> ClientEmailChangeRequests => Set<ClientEmailChangeRequest>();

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionParticipant> SessionParticipants => Set<SessionParticipant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<PaymentProviderAccount> PaymentProviderAccounts => Set<PaymentProviderAccount>();
    public DbSet<TrainerLocation> TrainerLocations => Set<TrainerLocation>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<CalendarIntegration> CalendarIntegrations => Set<CalendarIntegration>();
    public DbSet<CalendarEventLink> CalendarEventLinks => Set<CalendarEventLink>();
    public DbSet<CalendarSubscription> CalendarSubscriptions => Set<CalendarSubscription>();
    public DbSet<ExternalCalendarEvent> ExternalCalendarEvents => Set<ExternalCalendarEvent>();

    public DbSet<TrainerRate> TrainerRates => Set<TrainerRate>();
    public DbSet<TrainerMonthlySettlement> TrainerMonthlySettlements => Set<TrainerMonthlySettlement>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<MilestoneDefinition> MilestoneDefinitions => Set<MilestoneDefinition>();
    public DbSet<ClientMilestone> ClientMilestones => Set<ClientMilestone>();

    public override int SaveChanges()
    {
        NormalizeDateTimesToUtc();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================
        // USER / ROLES
        // =========================

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.AvatarUrl)
            .HasMaxLength(1000);

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

        modelBuilder.Entity<Trainer>()
            .Property(t => t.OutlookCategoryName)
            .HasMaxLength(100);

        modelBuilder.Entity<Trainer>()
            .Property(t => t.OutlookCategoryColor)
            .HasMaxLength(32);

        // =========================
        // TRAINER CONTRACTS
        // =========================

        modelBuilder.Entity<TrainerContract>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.ContractNumber)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(c => c.Notes)
                .HasMaxLength(1000);

            entity.HasOne(c => c.Trainer)
                .WithMany(t => t.Contracts)
                .HasForeignKey(c => c.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.TrainerId, c.ValidFrom, c.ValidTo });
            entity.HasIndex(c => new { c.TrainerId, c.IsActive });
        });

        modelBuilder.Entity<TrainerContractLocation>(entity =>
        {
            entity.HasKey(cl => new { cl.TrainerContractId, cl.LocationId });

            entity.HasOne(cl => cl.TrainerContract)
                .WithMany(c => c.ContractLocations)
                .HasForeignKey(cl => cl.TrainerContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cl => cl.Location)
                .WithMany(l => l.TrainerContractLocations)
                .HasForeignKey(cl => cl.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(cl => cl.LocationId);
        });

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
        // SYSTEM SETTINGS
        // =========================

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Key)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(s => s.Value)
                .IsRequired()
                .HasMaxLength(500);

            entity.HasIndex(s => s.Key)
                .IsUnique();

            entity.HasData(
                new SystemSetting
                {
                    Id = 1,
                    Key = "DefaultPackageValidityDays",
                    Value = "45",
                    CreatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
                },
                new SystemSetting
                {
                    Id = 2,
                    Key = "DefaultSessionDurationMinutes",
                    Value = "60",
                    CreatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
                },
                new SystemSetting
                {
                    Id = 3,
                    Key = "DefaultPaymentDueDays",
                    Value = "7",
                    CreatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
                });
        });

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
            .Property(c => c.GoogleDriveFolderId)
            .HasMaxLength(300);

        modelBuilder.Entity<Client>()
            .Property(c => c.TrainingPlanFileId)
            .HasMaxLength(300);

        modelBuilder.Entity<Client>()
            .Property(c => c.TrainingPlanFileName)
            .HasMaxLength(300);

        modelBuilder.Entity<Client>()
            .Property(c => c.TrainingPlanUrl)
            .HasMaxLength(1000);

        modelBuilder.Entity<Client>()
            .Property(c => c.SubscriptionAutoRenewEnabled)
            .HasDefaultValue(true);

        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => !c.IsDeleted);

        // =========================
        // PACKAGES
        // =========================

        modelBuilder.Entity<Package>()
            .Property(p => p.Price)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Package>()
            .HasOne(p => p.Location)
            .WithMany()
            .HasForeignKey(p => p.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Package>()
            .HasIndex(p => new { p.LocationId, p.BillingType, p.SessionsPerWeek, p.SessionsLimit });

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

            entity.Property(x => x.OriginalPrice)
                .HasPrecision(18, 2);

            entity.Property(x => x.BalanceApplied)
                .HasPrecision(18, 2);

            entity.Property(x => x.SessionsPerWeek)
                .HasDefaultValue(1);

            entity.Property(x => x.AmountPaid)
                .HasPrecision(18, 2);

            entity.Property(x => x.ExpectedUnitPrice)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.ActivationMode)
                .HasDefaultValue(StudioCRM.Domain.Enums.ClientPackageActivationMode.Immediately);

            entity.Property(x => x.RenewalSource)
                .HasMaxLength(50)
                .HasDefaultValue("Manual");

            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.ClientId, x.IsActive });
            entity.HasIndex(x => x.ClientId)
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE")
                .HasDatabaseName("IX_ClientPackages_OneActivePerClient");
        });

        modelBuilder.Entity<ClientEmailChangeRequest>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CurrentEmail)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(x => x.RequestedEmail)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.ClientId, x.Status });
        });

        // =========================
        // CLIENT PAYMENTS
        // =========================

        modelBuilder.Entity<ClientPayment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.AppliedToPackageAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.BalanceCreditAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.RejectionReason)
                .HasMaxLength(500);

            entity.Property(x => x.ReversalReason)
                .HasMaxLength(500);

            entity.Property(x => x.ExternalPaymentId)
                .HasMaxLength(200);

            entity.Property(x => x.PaymentProvider)
                .HasMaxLength(50);

            entity.Property(x => x.ProviderPaymentId)
                .HasMaxLength(200);

            entity.Property(x => x.ProviderStatus)
                .HasMaxLength(100);

            entity.Property(x => x.ProviderFeeAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            entity.Property(x => x.ProviderNetAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.ProviderSettlementId)
                .HasMaxLength(200);

            entity.Property(x => x.CheckoutUrl)
                .HasMaxLength(1000);

            entity.Property(x => x.ReceiptNumber)
                .HasMaxLength(100);

            entity.Property(x => x.ReceiptRequired)
                .HasDefaultValue(true);

            entity.Property(x => x.ReceiptNote)
                .HasMaxLength(500);

            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ClientPackage)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.ClientPackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LegalEntity)
                .WithMany()
                .HasForeignKey(x => x.LegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PaymentProviderAccount)
                .WithMany()
                .HasForeignKey(x => x.PaymentProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ClientId);
            entity.HasIndex(x => x.ClientPackageId);
            entity.HasIndex(x => x.LocationId);
            entity.HasIndex(x => x.LegalEntityId);
            entity.HasIndex(x => x.PaymentProviderAccountId);
            entity.HasIndex(x => x.ProviderPaymentId);
            entity.HasIndex(x => x.ProviderPayoutDate);
            entity.HasIndex(x => x.ProviderSettlementId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ReceiptStatus);
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
                .WithMany(x => x.BalanceTransactions)
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
        // COMPANY EXPENSES
        // =========================

        modelBuilder.Entity<CompanyExpense>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.VendorName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.VendorNip)
                .HasMaxLength(20);

            entity.Property(x => x.InvoiceNumber)
                .HasMaxLength(100);

            entity.Property(x => x.NetAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.VatAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.GrossAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.Property(x => x.AttachmentUrl)
                .HasMaxLength(1000);

            entity.Property(x => x.AttachmentStorageKey)
                .HasMaxLength(500);

            entity.Property(x => x.AttachmentFileName)
                .HasMaxLength(255);

            entity.Property(x => x.AttachmentContentType)
                .HasMaxLength(100);

            entity.Property(x => x.RecurringGroupId)
                .HasMaxLength(100);

            entity.HasOne(x => x.LegalEntity)
                .WithMany(x => x.Expenses)
                .HasForeignKey(x => x.LegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany(x => x.Expenses)
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.LegalEntityId);
            entity.HasIndex(x => x.LocationId);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.PaymentStatus);
            entity.HasIndex(x => x.IssueDate);
            entity.HasIndex(x => x.DueDate);
            entity.HasIndex(x => x.InvoiceNumber);
        });

        // =========================
        // LOCATIONS
        // =========================

        modelBuilder.Entity<LegalEntity>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Nip)
                .HasMaxLength(20);

            entity.Property(x => x.Address)
                .HasMaxLength(300);

            entity.Property(x => x.Email)
                .HasMaxLength(250);

            entity.Property(x => x.Phone)
                .HasMaxLength(50);

            entity.Property(x => x.PaymentRecipientName)
                .HasMaxLength(200);

            entity.Property(x => x.BankAccountNumber)
                .HasMaxLength(64);

            entity.Property(x => x.BlikPhoneNumber)
                .HasMaxLength(32);

            entity.Property(x => x.TransferTitleTemplate)
                .HasMaxLength(300);

            entity.Property(x => x.PaymentDescription)
                .HasMaxLength(1000);

            entity.HasIndex(x => x.Nip);
        });

        modelBuilder.Entity<PaymentProviderAccount>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Provider)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.DisplayName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.MerchantId)
                .HasMaxLength(200);

            entity.Property(x => x.PosId)
                .HasMaxLength(200);

            entity.Property(x => x.AccountKey)
                .HasMaxLength(100);

            entity.HasOne(x => x.LegalEntity)
                .WithMany(x => x.PaymentProviderAccounts)
                .HasForeignKey(x => x.LegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany(x => x.PaymentProviderAccounts)
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.LegalEntityId, x.Provider, x.IsActive });
            entity.HasIndex(x => new { x.LocationId, x.Provider, x.IsActive });
        });

        modelBuilder.Entity<Location>()
            .HasOne(l => l.LegalEntity)
            .WithMany(le => le.Locations)
            .HasForeignKey(l => l.LegalEntityId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Location>()
            .Property(l => l.PaymentRecipientName)
            .HasMaxLength(200);

        modelBuilder.Entity<Location>()
            .Property(l => l.BankAccountNumber)
            .HasMaxLength(64);

        modelBuilder.Entity<Location>()
            .Property(l => l.BlikPhoneNumber)
            .HasMaxLength(32);

        modelBuilder.Entity<Location>()
            .Property(l => l.TransferTitleTemplate)
            .HasMaxLength(300);

        modelBuilder.Entity<Location>()
            .Property(l => l.PaymentDescription)
            .HasMaxLength(1000);

        modelBuilder.Entity<Location>()
            .Property(l => l.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<Location>()
            .Property(l => l.FiscalRegisterName)
            .HasMaxLength(200);

        modelBuilder.Entity<Location>()
            .Property(l => l.FiscalRegisterNumber)
            .HasMaxLength(100);

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

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Type)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(n => n.SourceKey)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(n => n.Severity)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(n => n.Message)
                .HasMaxLength(1000);

            entity.Property(n => n.RelatedEntityType)
                .HasMaxLength(100);

            entity.Property(n => n.ActionUrl)
                .HasMaxLength(500);

            entity.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            entity.HasIndex(n => new { n.UserId, n.SourceKey })
                .IsUnique();
        });

        // =========================
        // INVITATIONS
        // =========================

        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.Location)
            .WithMany()
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.Trainer)
            .WithMany()
            .HasForeignKey(i => i.TrainerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Invitation>()
            .HasIndex(i => i.Token)
            .IsUnique();

        modelBuilder.Entity<Invitation>()
            .HasIndex(i => i.TrainerId);

        modelBuilder.Entity<Invitation>()
            .Property(i => i.LastSendError)
            .HasMaxLength(2000);

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
        modelBuilder.Entity<Location>()
            .HasIndex(l => l.CalendarEmail)
            .IsUnique(false);
        // =========================
        // Milestones
        // =========================

        modelBuilder.Entity<ClientMilestone>(entity =>
        {
            entity.HasOne(x => x.RewardClaimedByUser)
                .WithMany()
                .HasForeignKey(x => x.RewardClaimedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MilestoneDefinition>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.RewardName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.HasData(
                new MilestoneDefinition
                {
                    Id = 1,
                    Name = "3 miesiące treningów",
                    RequiredMonths = 3,
                    RewardName = "Mały upominek",
                    Description = "Nagroda za regularne uczęszczanie przez 3 miesiące.",
                    IsActive = true
                },
                new MilestoneDefinition
                {
                    Id = 2,
                    Name = "6 miesięcy treningów",
                    RequiredMonths = 6,
                    RewardName = "Większy upominek",
                    Description = "Nagroda za regularne uczęszczanie przez 6 miesięcy.",
                    IsActive = true
                },
                new MilestoneDefinition
                {
                    Id = 3,
                    Name = "12 miesięcy treningów",
                    RequiredMonths = 12,
                    RewardName = "Koszulka z logo studia",
                    Description = "Nagroda za rok treningów w studio.",
                    IsActive = true
                }
            );
        });
    }

    private void NormalizeDateTimesToUtc()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            foreach (var property in entry.CurrentValues.Properties)
            {
                if (property.ClrType == typeof(DateTime))
                {
                    var value = entry.CurrentValues[property];

                    if (value is DateTime dateTime)
                        entry.CurrentValues[property] = NormalizeDateTimeToUtc(dateTime);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    var value = entry.CurrentValues[property];

                    if (value is DateTime dateTime)
                        entry.CurrentValues[property] = NormalizeDateTimeToUtc(dateTime);
                }
            }
        }
    }

    private static DateTime NormalizeDateTimeToUtc(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
                GetStudioTimeZone())
        };
    }

    private static TimeZoneInfo GetStudioTimeZone()
    {
        foreach (var id in new[] { "Europe/Warsaw", WindowsStudioTimeZone })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

}
