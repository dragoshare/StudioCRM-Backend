using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Application.Common;

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
        p.Id, p.ClientId, p.PackageId, p.BillingMode, p.PeriodStartsAt, p.PeriodDurationDays, p.PeriodAutoRenewEnabled, p.TotalSessions, p.UsedSessions, p.TotalPrice,
        p.OriginalPrice, p.AmountPaid, p.BalanceApplied, p.ExpectedUnitPrice, p.IsActive,
        p.ClosureDisposition, p.ValidUntil, p.PaymentDueDate, p.ClosedAt, p.RenewalSource,
        p.RefundAmount, p.RefundConfirmedAt, p.RefundReference, p.LocationId
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
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74101}, {clientId})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74121}, {id})");
        }
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
            AmountPaid = p.AmountPaid, TotalPrice = p.TotalPrice,
            RemainingSessions = Math.Max(0, p.TotalSessions - p.UsedSessions),
            CanDelete = deleteReason == null, DeleteBlockReason = deleteReason,
            CanEdit = p.BillingMode == PackageBillingMode.SessionBased && blockers.Count == 0 && !payments && !balance && !counted && p.AmountPaid == 0,
            CanCorrect = p.BillingMode == PackageBillingMode.SessionBased && blockers.Count == 0,
            CanCorrectPrice = p.BillingMode == PackageBillingMode.SessionBased && blockers.All(PriceCorrectionAllows),
            CanCorrectSessions = p.BillingMode == PackageBillingMode.SessionBased && blockers.All(SessionCountCorrectionAllows),
            CanClose = blockers.Count == 0, Blockers = blockers
        };
    }

    public async Task<PackageManagementPreviewDto> GetManagementPreviewAsync(int clientId, int clientPackageId)
        => await PreviewAsync(await LoadManagedAsync(clientId, clientPackageId));

    private async Task EnsureChangeAsync(ClientPackage p, string expectedVersion, Func<string, bool>? allows = null)
    {
        if (expectedVersion != Version(p)) throw new InvalidOperationException("Package changed. Refresh the preview before confirming.");
        var preview = await PreviewAsync(p);
        var blockers = allows == null ? preview.Blockers : preview.Blockers.Where(b => !allows(b)).ToList();
        if (blockers.Count > 0) throw new InvalidOperationException(string.Join("; ", blockers));
    }

    // A renewal or planned sessions do not depend on this cycle's price: any surplus or debt goes through the balance.
    private static bool PriceCorrectionAllows(string blocker) => blocker is "HasRenewal" or "UnfinishedSessions";

    // A planned session only points at the cycle; its entry is taken when it is completed, from the cycle active then
    // and within its limit. Fewer entries (never below the used ones) leave later sessions to the next cycle.
    private static bool SessionCountCorrectionAllows(string blocker) => blocker is "UnfinishedSessions";

    internal const string SurplusToBalance = "ToBalance";
    internal const string SurplusKeptByStudio = "KeepByStudio";

    internal static void ValidateCorrection(ClientPackage p, PackageChangeRequest r)
    {
        if (p.BillingMode == PackageBillingMode.PeriodBased)
            throw new InvalidOperationException("Period service terms cannot be corrected through session-package management. Close and assign a new period.");
        if (r.TotalSessions.HasValue && (r.TotalSessions <= 0 || r.TotalSessions < p.UsedSessions))
            throw new InvalidOperationException("Total sessions must be positive and not less than used sessions.");
        if (p.IsActive && r.TotalSessions.HasValue && r.TotalSessions <= p.UsedSessions)
            throw new InvalidOperationException("Use package closure to remove all remaining entries.");
        if (r.TotalPrice.HasValue && r.TotalPrice < 0)
            throw new InvalidOperationException("Price cannot be negative.");
        if (r.SurplusDisposition is not (null or SurplusToBalance or SurplusKeptByStudio))
            throw new InvalidOperationException("Surplus disposition must be ToBalance or KeepByStudio.");
        if (r.TotalPrice.HasValue && r.TotalPrice < p.AmountPaid && r.SurplusDisposition == null)
            throw new InvalidOperationException("Price is lower than the amount already paid. Choose whether the surplus goes to the client balance (ToBalance) or stays with the studio (KeepByStudio).");
        if (r.ValidUntil.HasValue && r.ValidUntil < p.PurchaseDate)
            throw new InvalidOperationException("Validity cannot end before purchase.");
    }

    public async Task<PackageCorrectionResultDto> CorrectAsync(int clientId, int clientPackageId, PackageChangeRequest request)
    {
        RequireOwner(); RequireReason(request.Reason);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p = await LoadManagedAsync(clientId, clientPackageId);
        var priceOnly = !request.TotalSessions.HasValue && !request.ValidUntil.HasValue && !request.LocationId.HasValue;
        await EnsureChangeAsync(p, request.ExpectedVersion,
            priceOnly ? PriceCorrectionAllows : !request.ValidUntil.HasValue ? SessionCountCorrectionAllows : null);
        ValidateCorrection(p, request);
        // 30.10: a client moving to another studio keeps her running cycle; a cycle bound to the old location would
        // not settle sessions at the new one. Locations are filtered to the current organization.
        if (request.LocationId.HasValue && !await _context.Locations.AnyAsync(l => l.Id == request.LocationId && l.IsActive))
            throw new InvalidOperationException("Location not found.");
        var before = State(p);
        var surplus = request.TotalPrice.HasValue ? Math.Max(0, p.AmountPaid - request.TotalPrice.Value) : 0;
        if (surplus > 0) await DisposeSurplusAsync(p, surplus, request.SurplusDisposition!, request.Reason);
        p.TotalSessions = request.TotalSessions ?? p.TotalSessions;
        if (request.TotalPrice.HasValue)
        {
            p.TotalPrice = request.TotalPrice.Value;
            p.OriginalPrice = p.TotalPrice + p.BalanceApplied;
        }
        // Consumed entries keep their original valuation; do not rewrite past session accounting.
        if (p.UsedSessions == 0) p.ExpectedUnitPrice = (p.OriginalPrice > 0 ? p.OriginalPrice : p.TotalPrice + p.BalanceApplied) / p.TotalSessions;
        p.ValidUntil = (p.BillingMode == PackageBillingMode.SessionBased
            ? PackageValidity.EndOfStudioDay(NormalizeNullableDateTime(request.ValidUntil))
            : NormalizeNullableDateTime(request.ValidUntil)) ?? p.ValidUntil;
        p.PaymentDueDate = NormalizeNullableDateTime(request.PaymentDueDate) ?? p.PaymentDueDate;
        p.LocationId = request.LocationId ?? p.LocationId;
        RefreshStatus(p);
        Audit(p, "PackageCorrected", before, request.Reason);
        if (surplus > 0)
            _context.ClientAuditEntries.Add(new ClientAuditEntry
            {
                ClientId = p.ClientId, ActorUserId = _currentUser.UserId,
                Action = request.SurplusDisposition == SurplusToBalance ? "PackageSurplusToBalance" : "PackageSurplusKeptByStudio",
                BeforeJson = before,
                AfterJson = JsonSerializer.Serialize(new { ClientPackageId = p.Id, Surplus = surplus, p.TotalPrice, p.AmountPaid }),
                Reason = request.Reason.Trim()
            });
        await _context.SaveChangesAsync(); await tx.CommitAsync();
        return new PackageCorrectionResultDto
        {
            Surplus = surplus, SurplusDisposition = surplus > 0 ? request.SurplusDisposition : null,
            AmountPaid = p.AmountPaid, AmountDue = Math.Max(0, p.TotalPrice - p.AmountPaid)
        };
    }

    // Moves money paid above the corrected price out of the package. It is taken from the newest confirmed payments,
    // so a later payment reversal still undoes exactly what that payment contributed (package part and balance part).
    private async Task DisposeSurplusAsync(ClientPackage p, decimal surplus, string disposition, string reason)
    {
        var payments = await _context.ClientPayments
            .Where(x => x.ClientPackageId == p.Id && x.Status == ClientPaymentStatus.Confirmed && x.AppliedToPackageAmount > 0)
            .OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.Id)
            .ToListAsync();
        var remaining = surplus;
        foreach (var payment in payments)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, payment.AppliedToPackageAmount);
            payment.AppliedToPackageAmount -= take;
            if (disposition == SurplusToBalance) payment.BalanceCreditAmount += take;
            remaining -= take;
        }
        p.AmountPaid -= surplus;
        if (disposition != SurplusToBalance) return;
        var fromPayments = surplus - remaining;
        if (fromPayments > 0)
            _context.ClientBalanceTransactions.Add(new ClientBalanceTransaction
            {
                ClientId = p.ClientId, ClientPackageId = p.Id, Amount = fromPayments,
                Type = BalanceTransactionType.PaymentOverpayment,
                Description = "Nadwyżka z korekty ceny pakietu przeniesiona na saldo: " + reason.Trim(), CreatedAt = DateTime.UtcNow
            });
        // Amounts imported as already paid have no payment record, so they move to the balance as a manual adjustment.
        if (remaining > 0)
            _context.ClientBalanceTransactions.Add(new ClientBalanceTransaction
            {
                ClientId = p.ClientId, ClientPackageId = p.Id, Amount = remaining,
                Type = BalanceTransactionType.ManualAdjustment,
                Description = "Nadwyżka z korekty ceny pakietu (wpłata sprzed CRM) przeniesiona na saldo: " + reason.Trim(), CreatedAt = DateTime.UtcNow
            });
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
        p.PeriodAutoRenewEnabled = false; p.IsActive = false; p.ClosureDisposition = request.DebtDisposition == "KeepDue" ? "ClosedWithDebt" : "Closed"; p.ClosedAt = DateTime.UtcNow; p.ClosureReason = request.Reason.Trim();
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
        if (wasActive && (p.BillingMode == StudioCRM.Domain.Enums.PackageBillingMode.SessionBased && p.ExpectedBillingType != SessionBillingType.Group))
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
        if (p.IsActive && (p.BillingMode == StudioCRM.Domain.Enums.PackageBillingMode.SessionBased && p.ExpectedBillingType != SessionBillingType.Group)) p.Client.BillingStatus = p.PaymentStatus.ToString();
    }
}
