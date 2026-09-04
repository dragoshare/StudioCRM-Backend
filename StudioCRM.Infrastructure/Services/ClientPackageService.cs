using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Application.ClientPackages.Services;

public class ClientPackageService : IClientPackageService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudioSettingsService _settingsService;

    public ClientPackageService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IStudioSettingsService settingsService)
    {
        _context = context;
        _currentUser = currentUser;
        _settingsService = settingsService;
    }

    public async Task<int> CreateAsync(CreateClientPackageRequest request)
    {
        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == request.ClientId && !c.IsDeleted);

        if (client is null)
            throw new InvalidOperationException("Client does not exist.");

        if (_currentUser.IsTrainer && !_currentUser.IsOwner)
        {
            var trainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);

            if (trainer is null || client.TrainerId != trainer.Id)
                throw new InvalidOperationException("Trainer does not have access to this client.");
        }

        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId && !p.IsDeleted);

        if (package is null)
            throw new InvalidOperationException("Package does not exist.");

        if (package.LocationId.HasValue && package.LocationId.Value != client.LocationId)
            throw new InvalidOperationException("Package does not belong to the client's location.");

        var totalSessions = request.TotalSessions ?? package.SessionsLimit;
        if (totalSessions <= 0)
            throw new InvalidOperationException("Total sessions must be greater than zero.");

        var originalPrice = request.TotalPrice ?? package.Price;
        var carryOverBalance = await GetCarryOverBalanceAsync(client.Id);
        var balanceApplied = ResolveAppliedBalance(carryOverBalance, originalPrice);
        var totalPrice = Math.Max(0, originalPrice - balanceApplied);
        var expectedUnitPrice = originalPrice / totalSessions;
        var sessionsPerWeek = package.SessionsPerWeek > 0
            ? package.SessionsPerWeek
            : InferSessionsPerWeek(totalSessions);

        var isGroupPackage = (request.ExpectedBillingType ?? package.BillingType) == SessionBillingType.Group;
        var hasActivePackage = await _context.ClientPackages
            .AnyAsync(cp =>
                cp.ClientId == request.ClientId &&
                cp.IsActive &&
                cp.ExpectedBillingType != SessionBillingType.Group);

        if (!isGroupPackage && hasActivePackage)
            throw new InvalidOperationException("Client already has an active subscription cycle. Use subscription next-package endpoint to schedule package changes.");

        var now = DateTime.UtcNow;
        var settings = await _settingsService.GetOwnerSettingsAsync();

        var clientPackage = new ClientPackage
        {
            ClientId = request.ClientId,
            PackageId = request.PackageId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? package.Name : request.Name,
            TotalSessions = totalSessions,
            SessionsPerWeek = sessionsPerWeek,
            TotalPrice = totalPrice,
            OriginalPrice = originalPrice,
            BalanceApplied = balanceApplied,
            AmountPaid = 0,
            ExpectedUnitPrice = expectedUnitPrice,
            Currency = package.Currency,
            LocationId = package.LocationId ?? client.LocationId,
            ExpectedBillingType = request.ExpectedBillingType ?? package.BillingType,
            PaymentStatus = totalPrice <= 0 ? PaymentStatus.Paid : PaymentStatus.Unpaid,
            PurchaseDate = NormalizeDateTime(request.PurchaseDate, now),
            ValidUntil = NormalizeNullableDateTime(request.ValidUntil)
                ?? now.Date.AddDays(settings.DefaultPackageValidityDays),
            PaymentDueDate = totalPrice <= 0
                ? null
                : NormalizeNullableDateTime(request.PaymentDueDate)
                    ?? now.Date.AddDays(settings.DefaultPaymentDueDays),
            PaidAt = totalPrice <= 0 ? now : null,
            ActivationMode = ClientPackageActivationMode.Immediately,
            RenewalSource = isGroupPackage ? "GroupManual" : "Manual",
            ActivatedAt = now,
            ActivatedByUserId = _currentUser.UserId,
            IsActive = true
        };

        _context.ClientPackages.Add(clientPackage);

        if (balanceApplied != 0)
        {
            await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
            {
                ClientId = client.Id,
                ClientPackage = clientPackage,
                Amount = -balanceApplied,
                Type = BalanceTransactionType.UsedInNextPackage,
                Description = balanceApplied > 0
                    ? "Nadpłata wykorzystana w nowym pakiecie."
                    : "Dopłata doliczona do nowego pakietu.",
                CreatedAt = now
            });
        }

        if (!isGroupPackage)
        {
            client.ActivePackageId = package.Id;
            client.BillingStatus = clientPackage.PaymentStatus.ToString();
        }

        client.Status = "Active";
        client.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return clientPackage.Id;
    }

    public async Task<bool> ActivateAsync(int clientId, int clientPackageId)
    {
        var clientPackage = await _context.ClientPackages
            .FirstOrDefaultAsync(cp => cp.Id == clientPackageId && cp.ClientId == clientId);

        if (clientPackage is null)
            return false;

        await EnsureStaffAccessToClientAsync(clientId);

        var isGroupPackage = clientPackage.ExpectedBillingType == SessionBillingType.Group;
        var activePackages = isGroupPackage
            ? new List<ClientPackage>()
            : await _context.ClientPackages
                .Where(cp =>
                    cp.ClientId == clientId &&
                    cp.IsActive &&
                    cp.ExpectedBillingType != SessionBillingType.Group)
                .ToListAsync();

        foreach (var activePackage in activePackages)
            activePackage.IsActive = false;

        clientPackage.IsActive = true;
        clientPackage.ActivatedAt = DateTime.UtcNow;
        clientPackage.ActivatedByUserId = _currentUser.UserId;

        var client = await _context.Clients.FirstAsync(c => c.Id == clientId);
        if (!isGroupPackage)
        {
            client.ActivePackageId = clientPackage.PackageId;
        }

        client.Status = "Active";
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int clientId, int clientPackageId)
    {
        var clientPackage = await _context.ClientPackages
            .Include(cp => cp.Client)
            .FirstOrDefaultAsync(cp => cp.Id == clientPackageId && cp.ClientId == clientId);

        if (clientPackage is null)
            return false;

        await EnsureStaffAccessToClientAsync(clientId);

        var hasPayments = await _context.ClientPayments
            .AnyAsync(p => p.ClientPackageId == clientPackageId);

        if (hasPayments || clientPackage.AmountPaid > 0)
            throw new InvalidOperationException("Client package cannot be deleted because it has payment history.");

        var hasBalanceTransactions = await _context.ClientBalanceTransactions
            .AnyAsync(t => t.ClientPackageId == clientPackageId);

        if (hasBalanceTransactions)
            throw new InvalidOperationException("Client package cannot be deleted because it has balance transaction history.");

        var hasCountedSessions = clientPackage.UsedSessions > 0 ||
            await _context.SessionParticipants.AnyAsync(p =>
                p.ClientPackageId == clientPackageId &&
                p.IsCountedFromPackage);

        if (hasCountedSessions)
            throw new InvalidOperationException("Client package cannot be deleted because sessions have already been counted from it.");

        var linkedParticipants = await _context.SessionParticipants
            .Where(p => p.ClientPackageId == clientPackageId)
            .ToListAsync();

        foreach (var participant in linkedParticipants)
        {
            participant.ClientPackageId = null;
            participant.PackageId = null;
            participant.PlannedBillingType = null;
            participant.ExpectedUnitPrice = null;
            participant.UpdatedAt = DateTime.UtcNow;
        }

        var client = clientPackage.Client;
        if (clientPackage.IsActive && clientPackage.ExpectedBillingType != SessionBillingType.Group)
        {
            client.ActivePackageId = null;
            client.BillingStatus = "Pending";
            client.Status = "Inactive";
            client.UpdatedAt = DateTime.UtcNow;
        }

        _context.ClientPackages.Remove(clientPackage);
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task EnsureStaffAccessToClientAsync(int clientId)
    {
        if (_currentUser.IsOwner)
            return;

        if (!_currentUser.IsTrainer)
            throw new InvalidOperationException("Current user cannot manage client packages.");

        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);

        var hasAccess = trainer is not null && await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainer.Id && !c.IsDeleted);

        if (!hasAccess)
            throw new InvalidOperationException("Trainer does not have access to this client.");
    }

    private static int InferSessionsPerWeek(int totalSessions)
    {
        return Math.Max(1, (int)Math.Ceiling(totalSessions / 4m));
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

    private static DateTime NormalizeDateTime(DateTime value, DateTime fallback)
    {
        if (value == default)
            return fallback;

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? NormalizeNullableDateTime(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
