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

    public ClientPackageService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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

        var totalSessions = request.TotalSessions ?? package.SessionsLimit;
        if (totalSessions <= 0)
            throw new InvalidOperationException("Total sessions must be greater than zero.");

        var totalPrice = request.TotalPrice ?? package.Price;
        var expectedUnitPrice = package.Price / totalSessions;
        var sessionsPerWeek = package.SessionsPerWeek > 0
            ? package.SessionsPerWeek
            : InferSessionsPerWeek(totalSessions);

        var activePackages = await _context.ClientPackages
            .Where(cp => cp.ClientId == request.ClientId && cp.IsActive)
            .ToListAsync();

        foreach (var activePackage in activePackages)
            activePackage.IsActive = false;

        var clientPackage = new ClientPackage
        {
            ClientId = request.ClientId,
            PackageId = request.PackageId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? package.Name : request.Name,
            TotalSessions = totalSessions,
            SessionsPerWeek = sessionsPerWeek,
            TotalPrice = totalPrice,
            OriginalPrice = package.Price,
            BalanceApplied = 0,
            AmountPaid = 0,
            ExpectedUnitPrice = expectedUnitPrice,
            Currency = package.Currency,
            LocationId = package.LocationId ?? client.LocationId,
            ExpectedBillingType = request.ExpectedBillingType ?? package.BillingType,
            PaymentStatus = PaymentStatus.Unpaid,
            PurchaseDate = request.PurchaseDate,
            ValidUntil = request.ValidUntil ?? DateTime.UtcNow.Date.AddDays(45),
            PaymentDueDate = request.PaymentDueDate,
            ActivationMode = ClientPackageActivationMode.Immediately,
            RenewalSource = "Manual",
            ActivatedAt = DateTime.UtcNow,
            ActivatedByUserId = _currentUser.UserId,
            IsActive = true
        };

        _context.ClientPackages.Add(clientPackage);
        client.ActivePackageId = package.Id;
        client.BillingStatus = "Pending";
        client.UpdatedAt = DateTime.UtcNow;

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
}
