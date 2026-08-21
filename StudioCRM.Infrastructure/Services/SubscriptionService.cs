using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudioSettingsService _settingsService;

    public SubscriptionService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IStudioSettingsService settingsService)
    {
        _context = context;
        _currentUser = currentUser;
        _settingsService = settingsService;
    }

    public async Task<SubscriptionDto> GetCurrentClientSubscriptionAsync()
    {
        var client = await GetCurrentClientAsync();
        return await BuildSubscriptionAsync(client.Id);
    }

    public async Task<SubscriptionDto> GetClientSubscriptionAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId);
        return await BuildSubscriptionAsync(clientId);
    }

    public async Task<SubscriptionDto> SetNextPackageAsync(int clientId, SetNextPackageRequest request)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
        if (client is null)
            throw new InvalidOperationException("Client not found.");

        var nextPackage = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId && !p.IsDeleted && p.IsActive);

        if (nextPackage is null)
            throw new InvalidOperationException("Package does not exist or is inactive.");

        if (nextPackage.LocationId.HasValue && nextPackage.LocationId.Value != client.LocationId)
            throw new InvalidOperationException("Package is not available for this client's location.");

        client.NextPackageId = request.PackageId;
        client.SubscriptionAutoRenewEnabled = true;
        client.RenewalCancelledAt = null;
        client.RenewalCancelledByUserId = null;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await BuildSubscriptionAsync(clientId);
    }

    public async Task<SubscriptionDto> CancelRenewalAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
        if (client is null)
            throw new InvalidOperationException("Client not found.");

        client.SubscriptionAutoRenewEnabled = false;
        client.NextPackageId = null;
        client.RenewalCancelledAt = DateTime.UtcNow;
        client.RenewalCancelledByUserId = _currentUser.UserId;
        client.RenewalCancellationRequestedAt = null;
        client.RenewalCancellationRequestedByUserId = null;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await BuildSubscriptionAsync(clientId);
    }

    public async Task<SubscriptionDto> ResumeRenewalAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
        if (client is null)
            throw new InvalidOperationException("Client not found.");

        client.SubscriptionAutoRenewEnabled = true;
        client.RenewalCancelledAt = null;
        client.RenewalCancelledByUserId = null;
        client.RenewalCancellationRequestedAt = null;
        client.RenewalCancellationRequestedByUserId = null;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await BuildSubscriptionAsync(clientId);
    }

    public async Task<SubscriptionDto> RequestCancelRenewalAsClientAsync()
    {
        var client = await GetCurrentClientAsync();

        client.RenewalCancellationRequestedAt = DateTime.UtcNow;
        client.RenewalCancellationRequestedByUserId = _currentUser.UserId;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await BuildSubscriptionAsync(client.Id);
    }

    public async Task<SubscriptionDto> WithdrawCancelRenewalRequestAsClientAsync()
    {
        var client = await GetCurrentClientAsync();

        client.RenewalCancellationRequestedAt = null;
        client.RenewalCancellationRequestedByUserId = null;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await BuildSubscriptionAsync(client.Id);
    }

    public async Task<SubscriptionUsageDto> GetCurrentClientUsageAsync()
    {
        var client = await GetCurrentClientAsync();
        return await BuildUsageAsync(client.Id);
    }

    public async Task<SubscriptionUsageDto> GetClientUsageAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId);
        return await BuildUsageAsync(clientId);
    }

    public async Task<TrainingPlanDto> GetCurrentClientTrainingPlanAsync()
    {
        var client = await GetCurrentClientAsync();
        return MapTrainingPlan(client);
    }

    public async Task<TrainingPlanDto> GetClientTrainingPlanAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
        if (client is null)
            throw new InvalidOperationException("Client not found.");

        return MapTrainingPlan(client);
    }

    public async Task<TrainingPlanDto> UpdateTrainingPlanAsync(int clientId, UpdateTrainingPlanRequest request)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
        if (client is null)
            throw new InvalidOperationException("Client not found.");

        client.GoogleDriveFolderId = request.GoogleDriveFolderId;
        client.TrainingPlanFileId = request.FileId;
        client.TrainingPlanFileName = request.FileName;
        client.TrainingPlanUrl = request.Url;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapTrainingPlan(client);
    }

    public async Task RenewAfterCompletedCycleAsync(int clientPackageId)
    {
        var completedPackage = await _context.ClientPackages
            .Include(cp => cp.Client)
            .FirstOrDefaultAsync(cp => cp.Id == clientPackageId);

        if (completedPackage is null || !completedPackage.IsActive)
            return;

        if (completedPackage.UsedSessions < completedPackage.TotalSessions)
            return;

        var client = completedPackage.Client;

        if (!client.SubscriptionAutoRenewEnabled || client.RenewalCancellationRequestedAt is not null)
        {
            completedPackage.IsActive = false;
            client.Status = "Inactive";
            client.BillingStatus = "Cancelled";
            client.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return;
        }

        var nextPackageId = client.NextPackageId ?? completedPackage.PackageId;
        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == nextPackageId && !p.IsDeleted && p.IsActive);

        if (package is null)
            throw new InvalidOperationException("Next subscription package does not exist or is inactive.");

        var nextAlreadyExists = await _context.ClientPackages.AnyAsync(cp =>
            cp.ClientId == client.Id &&
            cp.PreviousClientPackageId == completedPackage.Id);

        if (nextAlreadyExists)
            return;

        completedPackage.IsActive = false;
        var nextCycle = await CreateRenewalCycleAsync(client, completedPackage, package);

        client.ActivePackageId = nextCycle.PackageId;
        client.NextPackageId = null;
        client.Status = "Active";
        client.BillingStatus = nextCycle.PaymentStatus.ToString();
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    private async Task<ClientPackage> CreateRenewalCycleAsync(
        Client client,
        ClientPackage previousCycle,
        Package package)
    {
        var originalPrice = package.Price;
        var carryOverBalance = await GetCarryOverBalanceAsync(client.Id);
        var balanceApplied = ResolveAppliedBalance(carryOverBalance, originalPrice);
        var amountToPay = Math.Max(0, originalPrice - balanceApplied);
        var now = DateTime.UtcNow;
        var settings = await _settingsService.GetOwnerSettingsAsync();

        var nextCycle = new ClientPackage
        {
            ClientId = client.Id,
            PackageId = package.Id,
            Name = package.Name,
            TotalSessions = package.SessionsLimit,
            SessionsPerWeek = package.SessionsPerWeek,
            UsedSessions = 0,
            OriginalPrice = originalPrice,
            BalanceApplied = balanceApplied,
            TotalPrice = amountToPay,
            AmountPaid = 0,
            ExpectedUnitPrice = package.SessionsLimit > 0 ? decimal.Round(package.Price / package.SessionsLimit, 2) : 0,
            Currency = package.Currency,
            LocationId = package.LocationId ?? client.LocationId,
            ExpectedBillingType = package.BillingType,
            PaymentStatus = amountToPay <= 0 ? PaymentStatus.Paid : PaymentStatus.Unpaid,
            PurchaseDate = now,
            ValidUntil = now.Date.AddDays(settings.DefaultPackageValidityDays),
            PaymentDueDate = amountToPay <= 0 ? null : now.Date.AddDays(settings.DefaultPaymentDueDays),
            PaidAt = amountToPay <= 0 ? now : null,
            ActivatedAt = now,
            ActivatedByUserId = _currentUser.UserId,
            ActivationMode = ClientPackageActivationMode.Immediately,
            PreviousClientPackageId = previousCycle.Id,
            RenewalSource = client.NextPackageId.HasValue ? "StaffPackageChange" : "AutoRenewal",
            IsActive = true
        };

        await _context.ClientPackages.AddAsync(nextCycle);

        if (balanceApplied != 0)
        {
            await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
            {
                ClientId = client.Id,
                ClientPackage = nextCycle,
                Amount = -balanceApplied,
                Type = BalanceTransactionType.UsedInNextPackage,
                Description = balanceApplied > 0
                    ? "Nadpłata wykorzystana w kolejnym cyklu subskrypcji."
                    : "Dopłata doliczona do kolejnego cyklu subskrypcji.",
                CreatedAt = now
            });
        }

        return nextCycle;
    }

    private async Task<SubscriptionDto> BuildSubscriptionAsync(int clientId)
    {
        var client = await _context.Clients
            .Include(c => c.Location)
            .FirstOrDefaultAsync(c => c.Id == clientId);

        if (client is null)
            throw new InvalidOperationException("Client not found.");

        var currentCycle = await _context.ClientPackages
            .Where(cp => cp.ClientId == client.Id && cp.IsActive)
            .OrderByDescending(cp => cp.ActivatedAt ?? cp.PurchaseDate)
            .FirstOrDefaultAsync();

        var nextPackageId = client.NextPackageId ?? currentCycle?.PackageId;
        var nextPackage = nextPackageId.HasValue
            ? await _context.Packages.FirstOrDefaultAsync(p => p.Id == nextPackageId.Value)
            : null;

        var balance = await GetCarryOverBalanceAsync(client.Id);

        return new SubscriptionDto
        {
            ClientId = client.Id,
            ClientName = $"{client.FirstName} {client.LastName}".Trim(),
            Status = ResolveSubscriptionStatus(client, currentCycle),
            AutoRenewEnabled = client.SubscriptionAutoRenewEnabled,
            CancelRenewalRequested = client.RenewalCancellationRequestedAt.HasValue,
            RenewalCancellationRequestedAt = client.RenewalCancellationRequestedAt,
            CurrentCycle = currentCycle is null ? null : MapCycle(currentCycle),
            NextPackage = nextPackage is null ? null : MapNextPackage(nextPackage),
            CarryOverBalance = balance
        };
    }

    private async Task<SubscriptionUsageDto> BuildUsageAsync(int clientId)
    {
        var currentCycle = await _context.ClientPackages
            .Where(cp => cp.ClientId == clientId && cp.IsActive)
            .OrderByDescending(cp => cp.ActivatedAt ?? cp.PurchaseDate)
            .FirstOrDefaultAsync();

        if (currentCycle is null)
        {
            return new SubscriptionUsageDto
            {
                ClientId = clientId
            };
        }

        var sessions = await _context.SessionParticipants
            .Include(sp => sp.Session)
                .ThenInclude(s => s.Trainer)
                    .ThenInclude(t => t.User)
            .Where(sp =>
                sp.ClientId == clientId &&
                sp.ClientPackageId == currentCycle.Id &&
                sp.IsCountedFromPackage)
            .OrderByDescending(sp => sp.Session.StartAt)
            .Select(sp => new SubscriptionUsageSessionDto
            {
                SessionId = sp.SessionId,
                Date = sp.Session.StartAt,
                TrainerName = sp.Session.Trainer.User.FirstName + " " + sp.Session.Trainer.User.LastName,
                Status = sp.Session.Status,
                PlannedBillingType = sp.PlannedBillingType != null ? sp.PlannedBillingType.ToString()! : string.Empty,
                ActualBillingType = sp.ActualBillingType != null ? sp.ActualBillingType.ToString()! : string.Empty,
                ExpectedUnitPrice = sp.ExpectedUnitPrice ?? 0,
                ActualUnitPrice = sp.ActualUnitPrice ?? 0,
                BalanceDifference = sp.BalanceDifference ?? 0
            })
            .ToListAsync();

        var expectedType = currentCycle.ExpectedBillingType.ToString();

        return new SubscriptionUsageDto
        {
            ClientId = clientId,
            ClientPackageId = currentCycle.Id,
            ExpectedBillingType = expectedType,
            TotalSessions = currentCycle.TotalSessions,
            UsedSessions = currentCycle.UsedSessions,
            RemainingSessions = Math.Max(0, currentCycle.TotalSessions - currentCycle.UsedSessions),
            AdjustmentsTotal = sessions.Sum(s => s.BalanceDifference),
            DifferentThanExpectedCount = sessions.Count(s => s.ActualBillingType != string.Empty && s.ActualBillingType != expectedType),
            ActualBreakdown = sessions
                .Where(s => !string.IsNullOrWhiteSpace(s.ActualBillingType))
                .GroupBy(s => s.ActualBillingType)
                .Select(g => new SubscriptionUsageBreakdownDto
                {
                    BillingType = g.Key,
                    Count = g.Count()
                })
                .OrderBy(b => b.BillingType)
                .ToList(),
            Sessions = sessions
        };
    }

    private async Task<decimal> GetCarryOverBalanceAsync(int clientId)
    {
        return await _context.ClientBalanceTransactions
            .Where(t =>
                t.ClientId == clientId &&
                t.Type != BalanceTransactionType.PaymentCredit &&
                t.Type != BalanceTransactionType.PaymentReversal)
            .SumAsync(t => t.Amount);
    }

    private static decimal ResolveAppliedBalance(decimal balance, decimal originalPrice)
    {
        if (balance > 0)
            return Math.Min(balance, originalPrice);

        return balance;
    }

    private static string ResolveSubscriptionStatus(Client client, ClientPackage? currentCycle)
    {
        if (!client.SubscriptionAutoRenewEnabled)
            return "Cancelled";

        if (client.RenewalCancellationRequestedAt.HasValue)
            return "CancelRequested";

        if (currentCycle is null)
            return client.NextPackageId.HasValue ? "PendingActivation" : "Inactive";

        if (currentCycle.PaymentStatus == PaymentStatus.Unpaid ||
            currentCycle.PaymentStatus == PaymentStatus.PendingConfirmation ||
            currentCycle.PaymentStatus == PaymentStatus.Overdue)
            return "PendingPayment";

        return currentCycle.UsedSessions >= currentCycle.TotalSessions ? "Completed" : "Active";
    }

    private static SubscriptionCycleDto MapCycle(ClientPackage cycle)
    {
        var originalPrice = cycle.OriginalPrice > 0 ? cycle.OriginalPrice : cycle.TotalPrice;

        return new SubscriptionCycleDto
        {
            ClientPackageId = cycle.Id,
            PackageId = cycle.PackageId,
            PackageName = cycle.Name,
            IsActive = cycle.IsActive,
            TotalSessions = cycle.TotalSessions,
            UsedSessions = cycle.UsedSessions,
            RemainingSessions = Math.Max(0, cycle.TotalSessions - cycle.UsedSessions),
            OriginalPrice = originalPrice,
            BalanceApplied = cycle.BalanceApplied,
            AmountToPay = cycle.TotalPrice,
            AmountPaid = cycle.AmountPaid,
            AmountDue = Math.Max(0, cycle.TotalPrice - cycle.AmountPaid),
            Currency = cycle.Currency,
            ExpectedBillingType = cycle.ExpectedBillingType.ToString(),
            PaymentStatus = cycle.PaymentStatus.ToString(),
            PurchaseDate = cycle.PurchaseDate,
            ValidUntil = cycle.ValidUntil,
            ActivatedAt = cycle.ActivatedAt
        };
    }

    private static SubscriptionNextPackageDto MapNextPackage(Package package)
    {
        return new SubscriptionNextPackageDto
        {
            PackageId = package.Id,
            PackageName = package.Name,
            SessionsLimit = package.SessionsLimit,
            SessionsPerWeek = package.SessionsPerWeek,
            Price = package.Price,
            Currency = package.Currency,
            BillingType = package.BillingType.ToString()
        };
    }

    private static TrainingPlanDto MapTrainingPlan(Client client)
    {
        return new TrainingPlanDto
        {
            ClientId = client.Id,
            GoogleDriveFolderId = client.GoogleDriveFolderId,
            GoogleDriveFolderUrl = BuildDriveFolderUrl(client.GoogleDriveFolderId),
            FileId = client.TrainingPlanFileId,
            FileName = client.TrainingPlanFileName,
            Url = client.TrainingPlanUrl
        };
    }

    private static string? BuildDriveFolderUrl(string? folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            return null;

        if (folderId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            folderId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return folderId;

        return $"https://drive.google.com/drive/folders/{Uri.EscapeDataString(folderId)}";
    }

    private async Task<Client> GetCurrentClientAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.UserId == _currentUser.UserId.Value && !c.IsDeleted);

        return client ?? throw new InvalidOperationException("Client profile not found.");
    }

    private async Task<Trainer> GetCurrentTrainerAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

        return trainer ?? throw new InvalidOperationException("Trainer profile not found.");
    }

    private async Task EnsureStaffAccessToClientAsync(int clientId)
    {
        if (_currentUser.IsOwner)
            return;

        if (!_currentUser.IsTrainer)
            throw new InvalidOperationException("Current user cannot manage this subscription.");

        var trainer = await GetCurrentTrainerAsync();

        var hasAccess = await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainer.Id && !c.IsDeleted);

        if (!hasAccess)
            throw new InvalidOperationException("Trainer does not have access to this client.");
    }
}
