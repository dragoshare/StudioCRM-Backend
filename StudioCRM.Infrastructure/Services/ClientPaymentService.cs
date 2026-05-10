using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class ClientPaymentService : IClientPaymentService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ClientPaymentService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ClientBillingSummaryDto> GetCurrentClientSummaryAsync()
    {
        var client = await GetCurrentClientAsync();
        return await BuildSummaryAsync(client.Id);
    }

    public async Task<ClientBillingSummaryDto> GetClientSummaryAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId);
        return await BuildSummaryAsync(clientId);
    }

    public async Task<List<ClientPaymentDto>> GetPendingConfirmationsAsync()
    {
        var query = _context.ClientPayments
            .Include(p => p.Client)
            .Include(p => p.ClientPackage)
            .Where(p => p.Status == ClientPaymentStatus.PendingConfirmation);

        if (_currentUser.IsTrainer && !_currentUser.IsOwner)
        {
            var trainer = await GetCurrentTrainerAsync();
            query = query.Where(p => p.Client.TrainerId == trainer.Id);
        }

        var payments = await query
            .OrderBy(p => p.PaymentDate)
            .ToListAsync();

        return payments.Select(MapPayment).ToList();
    }

    public async Task<ClientPaymentDto> RequestPaymentAsClientAsync(CreateClientPaymentRequest request)
    {
        var client = await GetCurrentClientAsync();
        var clientPackage = await ResolveClientPackageAsync(client.Id, request.ClientPackageId);

        var payment = new ClientPayment
        {
            ClientId = client.Id,
            ClientPackageId = clientPackage?.Id,
            Amount = NormalizeAmount(request.Amount),
            Currency = clientPackage?.Currency ?? "PLN",
            Method = request.Method,
            Status = ClientPaymentStatus.PendingConfirmation,
            Source = ClientPaymentSource.ClientRequest,
            PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId,
            Note = request.Note
        };

        await _context.ClientPayments.AddAsync(payment);

        if (clientPackage is not null && clientPackage.PaymentStatus == PaymentStatus.Unpaid)
            clientPackage.PaymentStatus = PaymentStatus.PendingConfirmation;

        await _context.SaveChangesAsync();

        return await GetPaymentDtoAsync(payment.Id);
    }

    public async Task<ClientPackagePurchaseResultDto> RequestPackageRenewalAsClientAsync(RequestClientPackageRenewalDto request)
    {
        var client = await GetCurrentClientAsync();

        var activePackage = await _context.ClientPackages
            .Where(cp => cp.ClientId == client.Id && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();

        if (activePackage is null)
            throw new InvalidOperationException("Client does not have an active package to renew.");

        if (activePackage.TotalSessions <= 0)
            throw new InvalidOperationException("Active package sessions limit must be greater than zero.");

        var paymentAmount = request.Amount.HasValue && request.Amount.Value > 0
            ? NormalizeAmount(request.Amount.Value)
            : activePackage.TotalPrice;

        var clientPackage = new ClientPackage
        {
            ClientId = client.Id,
            PackageId = activePackage.PackageId,
            Name = activePackage.Name,
            TotalSessions = activePackage.TotalSessions,
            SessionsPerWeek = activePackage.SessionsPerWeek,
            UsedSessions = 0,
            TotalPrice = activePackage.TotalPrice,
            OriginalPrice = activePackage.OriginalPrice > 0 ? activePackage.OriginalPrice : activePackage.TotalPrice,
            BalanceApplied = 0,
            AmountPaid = 0,
            ExpectedUnitPrice = activePackage.ExpectedUnitPrice,
            Currency = activePackage.Currency,
            LocationId = activePackage.LocationId ?? client.LocationId,
            ExpectedBillingType = activePackage.ExpectedBillingType,
            PaymentStatus = PaymentStatus.PendingConfirmation,
            PurchaseDate = DateTime.UtcNow,
            ValidUntil = activePackage.ValidUntil,
            PaymentDueDate = DateTime.UtcNow.Date.AddDays(7),
            ActivationMode = ClientPackageActivationMode.AfterCurrentPackage,
            RenewalSource = "ClientRenewalRequest",
            RequestedByUserId = _currentUser.UserId,
            IsActive = false
        };

        await _context.ClientPackages.AddAsync(clientPackage);
        await _context.SaveChangesAsync();

        var payment = new ClientPayment
        {
            ClientId = client.Id,
            ClientPackageId = clientPackage.Id,
            Amount = paymentAmount,
            Currency = clientPackage.Currency,
            Method = request.Method,
            Status = ClientPaymentStatus.PendingConfirmation,
            Source = ClientPaymentSource.ClientRequest,
            PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId,
            Note = request.Note
        };

        await _context.ClientPayments.AddAsync(payment);
        await _context.SaveChangesAsync();

        return new ClientPackagePurchaseResultDto
        {
            ClientPackageId = clientPackage.Id,
            PaymentId = payment.Id,
            PackageName = clientPackage.Name,
            PackagePrice = clientPackage.TotalPrice,
            PaymentAmount = payment.Amount,
            PaymentStatus = payment.Status.ToString(),
            ActivationMode = clientPackage.ActivationMode.ToString()
        };
    }

    public async Task<ClientPaymentDto> CreatePaymentAsStaffAsync(CreateClientPaymentRequest request)
    {
        if (!request.ClientId.HasValue)
            throw new InvalidOperationException("ClientId is required for staff payment entry.");

        await EnsureStaffAccessToClientAsync(request.ClientId.Value);
        var clientPackage = await ResolveClientPackageAsync(request.ClientId.Value, request.ClientPackageId);

        var payment = new ClientPayment
        {
            ClientId = request.ClientId.Value,
            ClientPackageId = clientPackage?.Id,
            Amount = NormalizeAmount(request.Amount),
            Currency = clientPackage?.Currency ?? "PLN",
            Method = request.Method,
            Status = ClientPaymentStatus.Confirmed,
            Source = ClientPaymentSource.StaffEntry,
            PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ConfirmedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId,
            ConfirmedByUserId = _currentUser.UserId,
            Note = request.Note
        };

        await _context.ClientPayments.AddAsync(payment);
        await ApplyConfirmedPaymentAsync(payment, clientPackage);
        await _context.SaveChangesAsync();

        return await GetPaymentDtoAsync(payment.Id);
    }

    public async Task<ClientPaymentDto> ConfirmAsync(int paymentId)
    {
        var payment = await _context.ClientPayments
            .Include(p => p.ClientPackage)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            throw new InvalidOperationException("Payment not found.");

        await EnsureStaffAccessToClientAsync(payment.ClientId);

        if (payment.Status != ClientPaymentStatus.PendingConfirmation)
            throw new InvalidOperationException("Only pending payments can be confirmed.");

        payment.Status = ClientPaymentStatus.Confirmed;
        payment.ConfirmedAt = DateTime.UtcNow;
        payment.ConfirmedByUserId = _currentUser.UserId;

        await ApplyConfirmedPaymentAsync(payment, payment.ClientPackage);
        await _context.SaveChangesAsync();

        return await GetPaymentDtoAsync(payment.Id);
    }

    public async Task<ClientPaymentDto> RejectAsync(int paymentId, RejectClientPaymentRequest request)
    {
        var payment = await _context.ClientPayments
            .Include(p => p.ClientPackage)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            throw new InvalidOperationException("Payment not found.");

        await EnsureStaffAccessToClientAsync(payment.ClientId);

        if (payment.Status != ClientPaymentStatus.PendingConfirmation)
            throw new InvalidOperationException("Only pending payments can be rejected.");

        payment.Status = ClientPaymentStatus.Rejected;
        payment.RejectedAt = DateTime.UtcNow;
        payment.RejectedByUserId = _currentUser.UserId;
        payment.RejectionReason = request.Reason;

        if (payment.ClientPackage is not null)
            await RefreshPackagePaymentStatusAsync(payment.ClientPackage);

        await _context.SaveChangesAsync();

        return await GetPaymentDtoAsync(payment.Id);
    }

    private async Task ApplyConfirmedPaymentAsync(ClientPayment payment, ClientPackage? clientPackage)
    {
        await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
        {
            ClientId = payment.ClientId,
            ClientPackageId = payment.ClientPackageId,
            Amount = payment.Amount,
            Type = BalanceTransactionType.PaymentCredit,
            Description = $"Potwierdzona płatność: {payment.Method}",
            CreatedAt = DateTime.UtcNow
        });

        if (clientPackage is null)
            return;

        clientPackage.AmountPaid += payment.Amount;
        clientPackage.PaidAt = clientPackage.AmountPaid >= clientPackage.TotalPrice
            ? DateTime.UtcNow
            : clientPackage.PaidAt;

        await RefreshPackagePaymentStatusAsync(clientPackage);
        await ActivatePackageAfterPaymentIfNeededAsync(clientPackage);
    }

    private async Task ActivatePackageAfterPaymentIfNeededAsync(ClientPackage clientPackage)
    {
        if (clientPackage.PaymentStatus != PaymentStatus.Paid || clientPackage.IsActive)
            return;

        var hasActivePackage = await _context.ClientPackages
            .AnyAsync(cp => cp.ClientId == clientPackage.ClientId && cp.IsActive);

        if (hasActivePackage && clientPackage.ActivationMode != ClientPackageActivationMode.Immediately)
            return;

        var activePackages = await _context.ClientPackages
            .Where(cp => cp.ClientId == clientPackage.ClientId && cp.IsActive)
            .ToListAsync();

        foreach (var activePackage in activePackages)
            activePackage.IsActive = false;

        clientPackage.IsActive = true;
        clientPackage.ActivatedAt = DateTime.UtcNow;
        clientPackage.ActivatedByUserId = _currentUser.UserId;

        var client = await _context.Clients.FirstAsync(c => c.Id == clientPackage.ClientId);
        client.ActivePackageId = clientPackage.PackageId;
        client.UpdatedAt = DateTime.UtcNow;
    }

    private async Task RefreshPackagePaymentStatusAsync(ClientPackage clientPackage)
    {
        var pendingAmount = await _context.ClientPayments
            .Where(p =>
                p.ClientPackageId == clientPackage.Id &&
                p.Status == ClientPaymentStatus.PendingConfirmation)
            .SumAsync(p => p.Amount);

        if (clientPackage.AmountPaid >= clientPackage.TotalPrice)
        {
            clientPackage.PaymentStatus = PaymentStatus.Paid;
            return;
        }

        if (clientPackage.AmountPaid > 0)
        {
            clientPackage.PaymentStatus = PaymentStatus.PartiallyPaid;
            return;
        }

        if (pendingAmount > 0)
        {
            clientPackage.PaymentStatus = PaymentStatus.PendingConfirmation;
            return;
        }

        clientPackage.PaymentStatus =
            clientPackage.PaymentDueDate < DateTime.UtcNow
                ? PaymentStatus.Overdue
                : PaymentStatus.Unpaid;
    }

    private async Task<ClientBillingSummaryDto> BuildSummaryAsync(int clientId)
    {
        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted);

        if (client is null)
            throw new InvalidOperationException("Client not found.");

        var activePackage = await _context.ClientPackages
            .Where(cp => cp.ClientId == client.Id && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();

        var clientPackageEntities = await _context.ClientPackages
            .Include(cp => cp.Location)
            .Where(cp => cp.ClientId == client.Id)
            .OrderByDescending(cp => cp.IsActive)
            .ThenByDescending(cp => cp.PurchaseDate)
            .ToListAsync();

        var clientPackages = clientPackageEntities
            .Select(cp => new ClientPackageBillingDto
            {
                ClientPackageId = cp.Id,
                PackageId = cp.PackageId,
                PackageName = cp.Name,
                IsActive = cp.IsActive,
                ActivationMode = cp.ActivationMode.ToString(),
                TotalSessions = cp.TotalSessions,
                SessionsPerWeek = cp.SessionsPerWeek,
                UsedSessions = cp.UsedSessions,
                RemainingSessions = Math.Max(0, cp.TotalSessions - cp.UsedSessions),
                TotalPrice = cp.TotalPrice,
                OriginalPrice = cp.OriginalPrice > 0 ? cp.OriginalPrice : cp.TotalPrice,
                BalanceApplied = cp.BalanceApplied,
                ExpectedUnitPrice = cp.ExpectedUnitPrice,
                AmountPaid = cp.AmountPaid,
                AmountDue = Math.Max(0, cp.TotalPrice - cp.AmountPaid),
                Currency = cp.Currency,
                ExpectedBillingType = cp.ExpectedBillingType.ToString(),
                LocationId = cp.LocationId,
                LocationName = cp.Location != null ? cp.Location.Name : null,
                PaymentStatus = cp.PaymentStatus.ToString(),
                PurchaseDate = cp.PurchaseDate,
                ValidUntil = cp.ValidUntil,
                PaymentDueDate = cp.PaymentDueDate,
                ActivatedAt = cp.ActivatedAt
            })
            .ToList();

        var paymentEntities = await _context.ClientPayments
            .Include(p => p.Client)
            .Include(p => p.ClientPackage)
            .Where(p => p.ClientId == client.Id)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var currentBalance = await _context.ClientBalanceTransactions
            .Where(t =>
                t.ClientId == client.Id &&
                t.Type != BalanceTransactionType.PaymentCredit)
            .SumAsync(t => t.Amount);

        return new ClientBillingSummaryDto
        {
            ClientId = client.Id,
            ClientName = $"{client.FirstName} {client.LastName}".Trim(),
            CurrentBalance = currentBalance,
            ActiveClientPackageId = activePackage?.Id,
            ActivePackageName = activePackage?.Name,
            ActivePackageTotalPrice = activePackage?.TotalPrice ?? 0,
            ActivePackageAmountPaid = activePackage?.AmountPaid ?? 0,
            ActivePackageAmountDue = activePackage is null
                ? 0
                : Math.Max(0, activePackage.TotalPrice - activePackage.AmountPaid),
            ActivePackagePaymentStatus = activePackage?.PaymentStatus.ToString() ?? string.Empty,
            Packages = clientPackages,
            Payments = paymentEntities.Select(MapPayment).ToList()
        };
    }

    private async Task<ClientPackage?> ResolveClientPackageAsync(int clientId, int? clientPackageId)
    {
        if (clientPackageId.HasValue)
        {
            var selectedPackage = await _context.ClientPackages
                .FirstOrDefaultAsync(cp => cp.Id == clientPackageId.Value && cp.ClientId == clientId);

            if (selectedPackage is null)
            {
                throw new InvalidOperationException(
                    "Client package not found for this client. Check activeClientPackageId from GET /api/billing/clients/{clientId}, or omit clientPackageId to use the active package.");
            }

            return selectedPackage;
        }

        return await _context.ClientPackages
            .Where(cp => cp.ClientId == clientId && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();
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
            throw new InvalidOperationException("Current user cannot manage client payments.");

        var trainer = await GetCurrentTrainerAsync();

        var hasAccess = await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainer.Id && !c.IsDeleted);

        if (!hasAccess)
            throw new InvalidOperationException("Trainer does not have access to this client.");
    }

    private async Task<ClientPaymentDto> GetPaymentDtoAsync(int paymentId)
    {
        var payment = await _context.ClientPayments
            .Include(p => p.Client)
            .Include(p => p.ClientPackage)
            .Where(p => p.Id == paymentId)
            .FirstAsync();

        return MapPayment(payment);
    }

    private static ClientPaymentDto MapPayment(ClientPayment payment)
    {
        return new ClientPaymentDto
        {
            Id = payment.Id,
            ClientId = payment.ClientId,
            ClientName = $"{payment.Client.FirstName} {payment.Client.LastName}".Trim(),
            ClientPackageId = payment.ClientPackageId,
            PackageName = payment.ClientPackage != null ? payment.ClientPackage.Name : null,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Method = payment.Method,
            Status = payment.Status,
            Source = payment.Source,
            PaymentDate = payment.PaymentDate,
            CreatedAt = payment.CreatedAt,
            ConfirmedAt = payment.ConfirmedAt,
            RejectedAt = payment.RejectedAt,
            CreatedByUserId = payment.CreatedByUserId,
            ConfirmedByUserId = payment.ConfirmedByUserId,
            RejectedByUserId = payment.RejectedByUserId,
            Note = payment.Note,
            RejectionReason = payment.RejectionReason
        };
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero.");

        return decimal.Round(amount, 2);
    }
}
