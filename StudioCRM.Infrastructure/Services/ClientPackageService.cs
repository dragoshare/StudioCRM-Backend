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

        var hasActivePackage = await _context.ClientPackages
            .AnyAsync(cp => cp.ClientId == request.ClientId && cp.IsActive);

        if (hasActivePackage)
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
            RenewalSource = "Manual",
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

        client.ActivePackageId = package.Id;
        client.BillingStatus = clientPackage.PaymentStatus.ToString();
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

        var activePackages = await _context.ClientPackages
            .Where(cp => cp.ClientId == clientId && cp.IsActive)
            .ToListAsync();

        foreach (var activePackage in activePackages)
            activePackage.IsActive = false;

        clientPackage.IsActive = true;
        clientPackage.ActivatedAt = DateTime.UtcNow;
        clientPackage.ActivatedByUserId = _currentUser.UserId;

        var client = await _context.Clients.FirstAsync(c => c.Id == clientId);
        client.ActivePackageId = clientPackage.PackageId;
        client.Status = "Active";
        client.UpdatedAt = DateTime.UtcNow;

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
