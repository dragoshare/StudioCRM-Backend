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

    public async Task<PagedResultDto<ClientPaymentDto>> GetPaymentsAsync(ClientPaymentFilterDto filter)
    {
        var query = BasePaymentQuery();

        if (_currentUser.IsTrainer && !_currentUser.IsOwner)
        {
            var trainer = await GetCurrentTrainerAsync();
            query = query.Where(p => p.Client.TrainerId == trainer.Id);
        }

        query = ApplyPaymentFilters(query, filter);

        return await ToPagedPaymentResultAsync(query, filter);
    }

    public async Task<PagedResultDto<ClientPaymentDto>> GetClientPaymentsAsync(
        int clientId,
        ClientPaymentFilterDto filter)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        filter.ClientId = clientId;
        var query = ApplyPaymentFilters(BasePaymentQuery(), filter);

        return await ToPagedPaymentResultAsync(query, filter);
    }

    public async Task<PagedResultDto<ClientBalanceTransactionDto>> GetClientBalanceTransactionsAsync(
        int clientId,
        int page,
        int pageSize)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _context.ClientBalanceTransactions
            .Where(t => t.ClientId == clientId)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new ClientBalanceTransactionDto
            {
                Id = t.Id,
                ClientId = t.ClientId,
                ClientPackageId = t.ClientPackageId,
                SessionId = t.SessionId,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                Description = t.Description,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PagedResultDto<ClientBalanceTransactionDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = ResolveTotalPages(totalCount, pageSize),
            Items = items
        };
    }

    public async Task<ClientPackageBillingDto?> GetActivePackageAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId);

        var activePackage = await _context.ClientPackages
            .Include(cp => cp.Location)
            .Where(cp => cp.ClientId == clientId && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();

        return activePackage is null ? null : MapPackage(activePackage);
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
            PaymentDate = NormalizeNullableDateTime(request.PaymentDate) ?? DateTime.UtcNow,
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
            PaymentDate = NormalizeNullableDateTime(request.PaymentDate) ?? DateTime.UtcNow,
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

    public async Task<ClientPaymentDto> IssueReceiptAsync(int paymentId, IssueReceiptRequest request)
    {
        var payment = await _context.ClientPayments
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            throw new InvalidOperationException("Payment not found.");

        await EnsureStaffAccessToClientAsync(payment.ClientId);

        if (payment.Status != ClientPaymentStatus.Confirmed)
            throw new InvalidOperationException("Only confirmed payments can receive a receipt.");

        if (payment.ReceiptStatus == ReceiptStatus.Issued)
            throw new InvalidOperationException("Receipt has already been issued for this payment.");

        payment.ReceiptStatus = ReceiptStatus.Issued;
        payment.ReceiptNumber = string.IsNullOrWhiteSpace(request.ReceiptNumber)
            ? GenerateReceiptNumber(payment)
            : request.ReceiptNumber.Trim();
        payment.ReceiptIssuedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetPaymentDtoAsync(payment.Id);
    }

    public async Task<ClientPaymentDto> CancelReceiptAsync(int paymentId)
    {
        var payment = await _context.ClientPayments
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            throw new InvalidOperationException("Payment not found.");

        await EnsureStaffAccessToClientAsync(payment.ClientId);

        if (payment.ReceiptStatus != ReceiptStatus.Issued)
            throw new InvalidOperationException("Only issued receipts can be cancelled.");

        payment.ReceiptStatus = ReceiptStatus.Cancelled;

        await _context.SaveChangesAsync();

        return await GetPaymentDtoAsync(payment.Id);
    }

    public async Task<ClientPaymentDto> ReverseAsync(int paymentId, ReverseClientPaymentRequest request)
    {
        var payment = await _context.ClientPayments
            .Include(p => p.ClientPackage)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            throw new InvalidOperationException("Payment not found.");

        await EnsureStaffAccessToClientAsync(payment.ClientId);

        if (payment.Status != ClientPaymentStatus.Confirmed)
            throw new InvalidOperationException("Only confirmed payments can be reversed.");

        if (payment.ReceiptStatus == ReceiptStatus.Issued)
            throw new InvalidOperationException("Cancel the receipt before reversing this payment.");

        await ReverseConfirmedPaymentAsync(payment, request);

        await _context.SaveChangesAsync();

        return await GetPaymentDtoAsync(payment.Id);
    }

    private async Task ApplyConfirmedPaymentAsync(ClientPayment payment, ClientPackage? clientPackage)
    {
        var amountDue = clientPackage is null
            ? 0
            : Math.Max(0, clientPackage.TotalPrice - clientPackage.AmountPaid);

        var appliedToPackageAmount = clientPackage is null
            ? 0
            : Math.Min(payment.Amount, amountDue);

        var balanceCreditAmount = payment.Amount - appliedToPackageAmount;

        payment.AppliedToPackageAmount = appliedToPackageAmount;
        payment.BalanceCreditAmount = balanceCreditAmount;

        await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
        {
            ClientId = payment.ClientId,
            ClientPackageId = payment.ClientPackageId,
            Amount = payment.Amount,
            Type = BalanceTransactionType.PaymentCredit,
            Description = $"Potwierdzona płatność: {payment.Method}",
            CreatedAt = DateTime.UtcNow
        });

        if (balanceCreditAmount > 0)
        {
            await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
            {
                ClientId = payment.ClientId,
                ClientPackageId = payment.ClientPackageId,
                Amount = balanceCreditAmount,
                Type = BalanceTransactionType.PaymentOverpayment,
                Description = clientPackage is null
                    ? "Wpłata bez przypisanego pakietu przeniesiona na saldo klienta."
                    : "Nadpłata za pakiet przeniesiona na saldo klienta.",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (clientPackage is null)
            return;

        clientPackage.AmountPaid += appliedToPackageAmount;
        clientPackage.PaidAt = clientPackage.AmountPaid >= clientPackage.TotalPrice
            ? DateTime.UtcNow
            : clientPackage.PaidAt;

        await RefreshPackagePaymentStatusAsync(clientPackage);
        await ActivatePackageAfterPaymentIfNeededAsync(clientPackage);
    }

    private async Task ReverseConfirmedPaymentAsync(
        ClientPayment payment,
        ReverseClientPaymentRequest request)
    {
        await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
        {
            ClientId = payment.ClientId,
            ClientPackageId = payment.ClientPackageId,
            Amount = -payment.Amount,
            Type = BalanceTransactionType.PaymentReversal,
            Description = string.IsNullOrWhiteSpace(request.Reason)
                ? "Cofnięcie zaksięgowanej wpłaty."
                : $"Cofnięcie zaksięgowanej wpłaty: {request.Reason}",
            CreatedAt = DateTime.UtcNow
        });

        if (payment.BalanceCreditAmount > 0)
        {
            await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
            {
                ClientId = payment.ClientId,
                ClientPackageId = payment.ClientPackageId,
                Amount = -payment.BalanceCreditAmount,
                Type = BalanceTransactionType.PaymentOverpayment,
                Description = string.IsNullOrWhiteSpace(request.Reason)
                    ? "Cofnięcie nadpłaty z salda klienta."
                    : $"Cofnięcie nadpłaty z salda klienta: {request.Reason}",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (payment.ClientPackage is not null && payment.AppliedToPackageAmount > 0)
        {
            payment.ClientPackage.AmountPaid = Math.Max(
                0,
                payment.ClientPackage.AmountPaid - payment.AppliedToPackageAmount);

            if (payment.ClientPackage.AmountPaid < payment.ClientPackage.TotalPrice)
                payment.ClientPackage.PaidAt = null;

            await RefreshPackagePaymentStatusAsync(payment.ClientPackage);
        }

        payment.Status = ClientPaymentStatus.Reversed;
        payment.ReversedAt = DateTime.UtcNow;
        payment.ReversedByUserId = _currentUser.UserId;
        payment.ReversalReason = request.Reason;
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
            .Select(MapPackage)
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
                t.Type != BalanceTransactionType.PaymentCredit &&
                t.Type != BalanceTransactionType.PaymentReversal)
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

    private IQueryable<ClientPayment> BasePaymentQuery()
    {
        return _context.ClientPayments
            .Include(p => p.Client)
            .Include(p => p.ClientPackage)
            .AsQueryable();
    }

    private static IQueryable<ClientPayment> ApplyPaymentFilters(
        IQueryable<ClientPayment> query,
        ClientPaymentFilterDto filter)
    {
        if (filter.ClientId.HasValue)
            query = query.Where(p => p.ClientId == filter.ClientId.Value);

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.Source.HasValue)
            query = query.Where(p => p.Source == filter.Source.Value);

        if (filter.From.HasValue)
        {
            var from = NormalizeNullableDateTime(filter.From)!.Value;
            query = query.Where(p => p.PaymentDate >= from);
        }

        if (filter.To.HasValue)
        {
            var to = NormalizeNullableDateTime(filter.To)!.Value;
            query = query.Where(p => p.PaymentDate <= to);
        }

        if (filter.AmountMin.HasValue)
            query = query.Where(p => p.Amount >= filter.AmountMin.Value);

        if (filter.AmountMax.HasValue)
            query = query.Where(p => p.Amount <= filter.AmountMax.Value);

        if (filter.HasOverpayment.HasValue)
        {
            query = filter.HasOverpayment.Value
                ? query.Where(p => p.BalanceCreditAmount > 0)
                : query.Where(p => p.BalanceCreditAmount <= 0);
        }

        return query;
    }

    private static async Task<PagedResultDto<ClientPaymentDto>> ToPagedPaymentResultAsync(
        IQueryable<ClientPayment> query,
        ClientPaymentFilterDto filter)
    {
        var page = NormalizePage(filter.Page);
        var pageSize = NormalizePageSize(filter.PageSize);

        var totalCount = await query.CountAsync();
        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<ClientPaymentDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = ResolveTotalPages(totalCount, pageSize),
            Items = payments.Select(MapPayment).ToList()
        };
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
            AppliedToPackageAmount = payment.AppliedToPackageAmount,
            BalanceCreditAmount = payment.BalanceCreditAmount,
            Currency = payment.Currency,
            Method = payment.Method,
            Status = payment.Status,
            Source = payment.Source,
            PaymentDate = payment.PaymentDate,
            CreatedAt = payment.CreatedAt,
            ConfirmedAt = payment.ConfirmedAt,
            RejectedAt = payment.RejectedAt,
            ReversedAt = payment.ReversedAt,
            CreatedByUserId = payment.CreatedByUserId,
            ConfirmedByUserId = payment.ConfirmedByUserId,
            RejectedByUserId = payment.RejectedByUserId,
            ReversedByUserId = payment.ReversedByUserId,
            Note = payment.Note,
            RejectionReason = payment.RejectionReason,
            ReversalReason = payment.ReversalReason,
            ReceiptStatus = payment.ReceiptStatus.ToString(),
            ReceiptNumber = payment.ReceiptNumber,
            ReceiptIssuedAt = payment.ReceiptIssuedAt
        };
    }

    private static ClientPackageBillingDto MapPackage(ClientPackage clientPackage)
    {
        return new ClientPackageBillingDto
        {
            ClientPackageId = clientPackage.Id,
            PackageId = clientPackage.PackageId,
            PackageName = clientPackage.Name,
            IsActive = clientPackage.IsActive,
            ActivationMode = clientPackage.ActivationMode.ToString(),
            TotalSessions = clientPackage.TotalSessions,
            SessionsPerWeek = clientPackage.SessionsPerWeek,
            UsedSessions = clientPackage.UsedSessions,
            RemainingSessions = Math.Max(0, clientPackage.TotalSessions - clientPackage.UsedSessions),
            TotalPrice = clientPackage.TotalPrice,
            OriginalPrice = clientPackage.OriginalPrice > 0 ? clientPackage.OriginalPrice : clientPackage.TotalPrice,
            BalanceApplied = clientPackage.BalanceApplied,
            ExpectedUnitPrice = clientPackage.ExpectedUnitPrice,
            AmountPaid = clientPackage.AmountPaid,
            AmountDue = Math.Max(0, clientPackage.TotalPrice - clientPackage.AmountPaid),
            Currency = clientPackage.Currency,
            ExpectedBillingType = clientPackage.ExpectedBillingType.ToString(),
            LocationId = clientPackage.LocationId,
            LocationName = clientPackage.Location != null ? clientPackage.Location.Name : null,
            PaymentStatus = clientPackage.PaymentStatus.ToString(),
            PurchaseDate = clientPackage.PurchaseDate,
            ValidUntil = clientPackage.ValidUntil,
            PaymentDueDate = clientPackage.PaymentDueDate,
            ActivatedAt = clientPackage.ActivatedAt
        };
    }

    private static string GenerateReceiptNumber(ClientPayment payment)
    {
        var year = DateTime.UtcNow.Year;
        return $"RCPT/{year}/{payment.Id:D6}";
    }

    private static int NormalizePage(int page)
    {
        return Math.Max(1, page);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, 100);
    }

    private static int ResolveTotalPages(int totalCount, int pageSize)
    {
        if (totalCount == 0)
            return 0;

        return (int)Math.Ceiling(totalCount / (decimal)pageSize);
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero.");

        return decimal.Round(amount, 2);
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
