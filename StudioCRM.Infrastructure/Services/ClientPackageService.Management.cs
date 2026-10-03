using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.ClientPackages.Services;

public partial class ClientPackageService
{
    private void RequireOwner()
    {
        if (!_currentUser.IsOwner) throw new UnauthorizedAccessException("Only owner can reconcile packages.");
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new InvalidOperationException("Provide a reason of 1 to 1000 characters.");
    }

    private static string State(ClientPackage p) => JsonSerializer.Serialize(new
    {
        p.Id, p.ClientId, p.PackageId, p.TotalSessions, p.UsedSessions, p.TotalPrice,
        p.OriginalPrice, p.AmountPaid, p.BalanceApplied, p.ExpectedUnitPrice, p.IsActive,
        p.ClosureDisposition, p.ValidUntil, p.PaymentDueDate, p.ClosedAt, p.RenewalSource,
        p.RefundAmount, p.RefundConfirmedAt, p.RefundReference
    });

    private static string Version(ClientPackage p) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(State(p))));

    private void Audit(ClientPackage p, string action, string before, string reason) =>
        _context.ClientAuditEntries.Add(new ClientAuditEntry
        {
            ClientId = p.ClientId, ActorUserId = _currentUser.UserId, Action = action,
            BeforeJson = before, AfterJson = State(p), Reason = reason.Trim()
        });

    private async Task<ClientPackage> LoadManagedAsync(int clientId, int id)
    {
        RequireOwner();
        await EnsureStaffAccessToClientAsync(clientId);
        return await _context.ClientPackages.Include(p => p.Client)
            .SingleOrDefaultAsync(p => p.ClientId == clientId && p.Id == id)
            ?? throw new InvalidOperationException("Package not found.");
    }

    private async Task<PackageManagementPreviewDto> PreviewAsync(ClientPackage p)
    {
        var payments = await _context.ClientPayments.AnyAsync(x => x.ClientPackageId == p.Id);
        var balance = await _context.ClientBalanceTransactions.AnyAsync(x => x.ClientPackageId == p.Id);
        var counted = p.UsedSessions > 0 || await _context.SessionParticipants.AnyAsync(x => x.ClientPackageId == p.Id && x.IsCountedFromPackage);
        var blockers = new List<string>();
        if (p.ClosureDisposition != null) blockers.Add("PackageClosed");
        if (await _context.ClientPayments.AnyAsync(x => x.ClientPackageId == p.Id && x.Status == ClientPaymentStatus.PendingConfirmation)) blockers.Add("PendingPayment");
        if (await _context.SessionParticipants.AnyAsync(x => x.ClientPackageId == p.Id && !x.Session.IsDeleted && x.Session.Status != "Cancelled" && x.Session.Status != "Completed")) blockers.Add("UnfinishedSessions");
        if (await _context.ClientPackages.AnyAsync(x => x.PreviousClientPackageId == p.Id)) blockers.Add("HasRenewal");
        var deleteReason = p.ClosureDisposition != null ? "PackageClosed" : p.RenewalSource == "OpeningBalance" ? "OpeningBalance" : payments || p.AmountPaid > 0 ? "PaymentHistory" : balance ? "BalanceHistory" : counted ? "UsedSessions" : blockers.FirstOrDefault();
        return new PackageManagementPreviewDto
        {
            ClientPackageId = p.Id, Version = Version(p), AmountDue = Math.Max(0, p.TotalPrice - p.AmountPaid),
            RemainingSessions = Math.Max(0, p.TotalSessions - p.UsedSessions),
            CanDelete = deleteReason == null, DeleteBlockReason = deleteReason,
            CanEdit = blockers.Count == 0 && !payments && !balance && !counted && p.AmountPaid == 0,
            CanCorrect = blockers.Count == 0, CanClose = blockers.Count == 0, Blockers = blockers
        };
    }

    public async Task<PackageManagementPreviewDto> GetManagementPreviewAsync(int clientId, int clientPackageId)
        => await PreviewAsync(await LoadManagedAsync(clientId, clientPackageId));

    private async Task EnsureChangeAsync(ClientPackage p, string expectedVersion)
    {
        if (expectedVersion != Version(p)) throw new InvalidOperationException("Package changed. Refresh the preview before confirming.");
        var preview = await PreviewAsync(p);
        if (preview.Blockers.Count > 0) throw new InvalidOperationException(string.Join("; ", preview.Blockers));
    }

    internal static void ValidateCorrection(ClientPackage p, PackageChangeRequest r)
    {
        if (r.TotalSessions.HasValue && (r.TotalSessions <= 0 || r.TotalSessions < p.UsedSessions))
            throw new InvalidOperationException("Total sessions must be positive and not less than used sessions.");
        if (p.IsActive && r.TotalSessions.HasValue && r.TotalSessions <= p.UsedSessions)
            throw new InvalidOperationException("Use package closure to remove all remaining entries.");
        if (r.TotalPrice.HasValue && (r.TotalPrice < 0 || r.TotalPrice < p.AmountPaid))
            throw new InvalidOperationException("Price cannot be lower than the amount already paid. Reconcile a refund separately.");
        if (r.ValidUntil.HasValue && r.ValidUntil < p.PurchaseDate)
            throw new InvalidOperationException("Validity cannot end before purchase.");
    }

    public async Task CorrectAsync(int clientId, int clientPackageId, PackageChangeRequest request)
    {
        RequireOwner(); RequireReason(request.Reason);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p = await LoadManagedAsync(clientId, clientPackageId);
        await EnsureChangeAsync(p, request.ExpectedVersion);
        ValidateCorrection(p, request);
        var before = State(p);
        p.TotalSessions = request.TotalSessions ?? p.TotalSessions;
        if (request.TotalPrice.HasValue)
        {
            p.TotalPrice = request.TotalPrice.Value;
            p.OriginalPrice = p.TotalPrice + p.BalanceApplied;
        }
        // Consumed entries keep their original valuation; do not rewrite past session accounting.
        if (p.UsedSessions == 0) p.ExpectedUnitPrice = (p.OriginalPrice > 0 ? p.OriginalPrice : p.TotalPrice + p.BalanceApplied) / p.TotalSessions;
        p.ValidUntil = NormalizeNullableDateTime(request.ValidUntil) ?? p.ValidUntil;
        p.PaymentDueDate = NormalizeNullableDateTime(request.PaymentDueDate) ?? p.PaymentDueDate;
        RefreshStatus(p);
        Audit(p, "PackageCorrected", before, request.Reason);
        await _context.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task<int?> CloseAsync(int clientId, int clientPackageId, ClosePackageRequest request)
    {
        RequireOwner(); RequireReason(request.Reason);
        if (request.DebtDisposition is not ("KeepDue" or "WaiveDue"))
            throw new InvalidOperationException("Choose KeepDue or WaiveDue.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p = await LoadManagedAsync(clientId, clientPackageId);
        await EnsureChangeAsync(p, request.ExpectedVersion);
        var before = State(p);
        if (request.FundsDisposition is not ("KeepFunds" or "Refund" or "Balance"))
            throw new InvalidOperationException("Choose KeepFunds, Refund or Balance.");
        if (request.SettlementAmount < 0 || request.SettlementAmount > p.AmountPaid ||
            (request.FundsDisposition == "KeepFunds" && request.SettlementAmount != 0) ||
            (request.FundsDisposition != "KeepFunds" && request.SettlementAmount <= 0))
            throw new InvalidOperationException("Settlement amount must match the decision and cannot exceed payments allocated to this package.");
        if (request.FundsDisposition != "KeepFunds" && request.DebtDisposition != "WaiveDue")
            throw new InvalidOperationException("Resolve outstanding debt with WaiveDue before refunding or transferring funds.");
        if (request.DebtDisposition == "WaiveDue") p.TotalPrice = p.AmountPaid;
        var wasActive = p.IsActive;
        p.IsActive = false; p.ClosureDisposition = request.DebtDisposition == "KeepDue" ? "ClosedWithDebt" : "Closed"; p.ClosedAt = DateTime.UtcNow; p.ClosureReason = request.Reason.Trim();
        if (request.FundsDisposition == "Refund")
        {
            p.ClosureDisposition = "RefundPending";
            p.RefundAmount = request.SettlementAmount;
        }
        if (request.FundsDisposition == "Balance")
        {
            p.ClosureDisposition = "TransferredToBalance";
            _context.ClientBalanceTransactions.Add(new ClientBalanceTransaction
            {
                ClientId = clientId, ClientPackageId = p.Id, Amount = request.SettlementAmount,
                Type = BalanceTransactionType.ManualAdjustment,
                Description = "Środki z zamkniętego pakietu: " + request.Reason.Trim(), CreatedAt = DateTime.UtcNow
            });
        }
        RefreshStatus(p);
        if (wasActive && p.ExpectedBillingType != SessionBillingType.Group)
        {
            p.Client.ActivePackageId = null;
            p.Client.NextPackageId = null;
            p.Client.SubscriptionAutoRenewEnabled = false;
        }
        Audit(p, "PackageClosed", before, request.Reason);
        await _context.SaveChangesAsync();
        int? replacementId = null;
        if (request.Replacement != null)
        {
            if (request.Replacement.ClientId != clientId) throw new InvalidOperationException("Replacement must belong to the same client.");
            replacementId = await CreateCoreAsync(request.Replacement, false);
            _context.ClientAuditEntries.Add(new ClientAuditEntry
            {
                ClientId = clientId, ActorUserId = _currentUser.UserId, Action = "PackageReplaced",
                BeforeJson = JsonSerializer.Serialize(new { ClientPackageId = p.Id }),
                AfterJson = JsonSerializer.Serialize(new { ClientPackageId = replacementId }), Reason = request.Reason.Trim()
            });
            await _context.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return replacementId;
    }

    public async Task<int> ImportAsync(ImportClientPackageRequest request)
    {
        RequireOwner(); RequireReason(request.Reason);
        if (request.RequestId == Guid.Empty) throw new InvalidOperationException("Provide a unique RequestId for import.");
        if (request.UsedSessions < 0 || request.AmountPaid < 0) throw new InvalidOperationException("Opening values cannot be negative.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var action = "PackageOpeningBalance:" + request.RequestId.ToString("N");
        var payload = JsonSerializer.Serialize(request);
        var existing = await _context.ClientAuditEntries.SingleOrDefaultAsync(x => x.Action == action);
        if (existing != null)
        {
            if (existing.BeforeJson != payload) throw new InvalidOperationException("RequestId was already used for another import.");
            return JsonDocument.Parse(existing.AfterJson).RootElement.GetProperty("Id").GetInt32();
        }
        var id = await CreateCoreAsync(request.Package, true);
        var p = await _context.ClientPackages.Include(x => x.Client).SingleAsync(x => x.Id == id);
        if (request.UsedSessions >= p.TotalSessions || request.AmountPaid > p.TotalPrice)
            throw new InvalidOperationException("Import a running package with remaining entries and payment not exceeding its price.");
        p.UsedSessions = request.UsedSessions; p.AmountPaid = request.AmountPaid; p.RenewalSource = "OpeningBalance";
        RefreshStatus(p); Audit(p, action, payload, request.Reason);
        await _context.SaveChangesAsync(); await tx.CommitAsync(); return id;
    }

    private static void RefreshStatus(ClientPackage p)
    {
        p.PaymentStatus = p.AmountPaid >= p.TotalPrice ? PaymentStatus.Paid :
            p.PaymentDueDate < DateTime.UtcNow ? PaymentStatus.Overdue :
            p.AmountPaid > 0 ? PaymentStatus.PartiallyPaid : PaymentStatus.Unpaid;
        p.PaidAt = p.PaymentStatus == PaymentStatus.Paid ? p.PaidAt ?? DateTime.UtcNow : null;
        if (p.IsActive && p.ExpectedBillingType != SessionBillingType.Group) p.Client.BillingStatus = p.PaymentStatus.ToString();
    }
}
