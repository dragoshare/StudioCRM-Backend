using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientService
{
    public async Task<ClientClosurePreviewDto> GetClosurePreviewAsync(int clientId)
    {
        EnsureOwner();
        await EnsureReadableClientAsync(clientId);
        var check = await CheckArchiveAsync(clientId);
        var packages = await _context.ClientPackages.Where(p => p.ClientId == clientId &&
            (p.IsActive || p.ClosureDisposition != null)).OrderBy(p => p.Id).ToListAsync();
        return new ClientClosurePreviewDto
        {
            ClientId = clientId,
            Blockers = check.Blockers.Where(b => b != "ActivePackages").ToList(),
            FutureSessionIds = await _context.SessionParticipants.Where(p => p.ClientId == clientId &&
                p.Session.EndAt > DateTime.UtcNow && p.Session.Status != "Cancelled")
                .Select(p => p.SessionId).Distinct().ToListAsync(),
            Balance = await _context.ClientBalanceTransactions.Where(t => t.ClientId == clientId &&
                t.Type != BalanceTransactionType.PaymentCredit && t.Type != BalanceTransactionType.PaymentReversal)
                .SumAsync(t => (decimal?)t.Amount) ?? 0,
            Packages = packages.Select(p => new ClientClosurePackageDto
            {
                ClientPackageId = p.Id, Name = p.Name, RequiresDecision = p.IsActive, RemainingSessions = Math.Max(0, p.TotalSessions - p.UsedSessions),
                AmountPaid = p.AmountPaid, AmountDue = Math.Max(0, p.TotalPrice - p.AmountPaid), Currency = p.Currency,
                Disposition = p.ClosureDisposition, RefundAmount = p.RefundAmount, RefundConfirmedAt = p.RefundConfirmedAt
            }).ToList()
        };
    }

    public async Task<ClientClosurePreviewDto> CloseCooperationAsync(int clientId, CloseClientRequest request)
    {
        EnsureOwner();
        ValidateReason(request.Reason);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId)
            ?? throw new KeyNotFoundException("Client not found or archived.");
        var preview = await GetClosurePreviewAsync(clientId);
        if (preview.Blockers.Count > 0)
            throw new InvalidOperationException(string.Join("; ", preview.Blockers));
        var packages = await _context.ClientPackages.Where(p => p.ClientId == clientId && p.IsActive).ToListAsync();
        if (request.Packages == null || request.Packages.Select(x => x.ClientPackageId).Distinct().Count() != request.Packages.Count ||
            !packages.Select(p => p.Id).Order().SequenceEqual(request.Packages.Select(p => p.ClientPackageId).Order()))
            throw new InvalidOperationException("Provide exactly one decision for each currently active package. Refresh the preview.");
        foreach (var decision in request.Packages)
        {
            var package = packages.Single(p => p.Id == decision.ClientPackageId);
            ValidateClosureDecision(package, decision);
        }
        var before = ClientAuditState(client);
        foreach (var decision in request.Packages)
        {
            var package = packages.Single(p => p.Id == decision.ClientPackageId);
            var packageBefore = PackageAuditState(package);
            package.IsActive = false;
            package.ClosureDisposition = decision.Disposition == "Retain" ? "Retained" : "RefundPending";
            package.ClosedAt = DateTime.UtcNow;
            package.ClosureReason = request.Reason.Trim();
            package.RefundAmount = decision.RefundAmount;
            AuditPackage(clientId, package, "PackageClosed", packageBefore, request.Reason);
        }
        client.SubscriptionAutoRenewEnabled = false;
        client.NextPackageId = null;
        client.ActivePackageId = null;
        client.RenewalCancelledAt = DateTime.UtcNow;
        client.RenewalCancelledByUserId = _currentUser.UserId;
        client.RenewalCancellationRequestedAt = null;
        client.RenewalCancellationRequestedByUserId = null;
        client.Status = "Inactive";
        client.UpdatedAt = DateTime.UtcNow;
        AuditClient(client, "CooperationClosed", before, request.Reason.Trim());
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return await GetClosurePreviewAsync(clientId);
    }

    public static void ValidateClosureDecision(ClientPackage package, CloseClientPackageRequest decision)
    {
        if (decision.Disposition is not ("Retain" or "Refund"))
            throw new InvalidOperationException("Disposition must be Retain or Refund.");
        if (decision.Disposition == "Retain" && decision.RefundAmount != 0)
            throw new InvalidOperationException("Retained package cannot have a refund amount.");
        if (decision.Disposition == "Refund" && (decision.RefundAmount <= 0 || decision.RefundAmount > package.AmountPaid ||
            decimal.Round(decision.RefundAmount, 2) != decision.RefundAmount))
            throw new InvalidOperationException("Refund must be a positive amount with at most two decimal places, no greater than the amount paid for this package.");
        if (package.ClosureDisposition != null)
            throw new InvalidOperationException("Package already has a closure decision.");
    }

    public async Task ConfirmRefundAsync(int clientId, int packageId, ConfirmClientRefundRequest request)
    {
        EnsureOwner();
        ValidateReason(request.Reference);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await EnsureReadableClientAsync(clientId);
        var package = await _context.ClientPackages.SingleOrDefaultAsync(p => p.Id == packageId && p.ClientId == clientId)
            ?? throw new KeyNotFoundException("Package not found.");
        if (package.ClosureDisposition == "Refunded" && package.RefundReference == request.Reference.Trim() && package.RefundAmount == request.Amount)
            return;
        if (package.ClosureDisposition != "RefundPending" || package.RefundAmount != request.Amount)
            throw new InvalidOperationException("Refund decision or amount does not match. Refresh the preview.");
        var before = PackageAuditState(package);
        package.ClosureDisposition = "Refunded";
        package.RefundConfirmedAt = DateTime.UtcNow;
        package.RefundReference = request.Reference.Trim();
        AuditPackage(clientId, package, "RefundConfirmedExternally", before, request.Reference.Trim());
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ResumeRetainedPackageAsync(int clientId, int packageId, ResumeRetainedPackageRequest request)
    {
        EnsureOwner();
        ValidateReason(request.Reason);
        if (request.ValidUntil.Kind != DateTimeKind.Utc || request.ValidUntil <= DateTime.UtcNow)
            throw new InvalidOperationException("Provide a future UTC ValidUntil date.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var client = await _context.Clients.SingleOrDefaultAsync(c => c.Id == clientId)
            ?? throw new InvalidOperationException("Restore the client first.");
        var package = await _context.ClientPackages.SingleOrDefaultAsync(p => p.Id == packageId && p.ClientId == clientId)
            ?? throw new KeyNotFoundException("Package not found.");
        if (package.ClosureDisposition != "Retained" || package.UsedSessions >= package.TotalSessions)
            throw new InvalidOperationException("Package has no retained entries to resume.");
        if (package.LocationId.HasValue && package.LocationId != client.LocationId)
            throw new InvalidOperationException("Package belongs to a different location.");
        if (package.ExpectedBillingType != SessionBillingType.Group && await _context.ClientPackages.AnyAsync(p =>
                p.ClientId == clientId && p.IsActive && p.ExpectedBillingType != SessionBillingType.Group))
            throw new InvalidOperationException("Client already has an active individual package.");
        var before = PackageAuditState(package);
        package.IsActive = true;
        package.ClosureDisposition = null;
        package.ClosedAt = null;
        package.ClosureReason = null;
        package.ValidUntil = request.ValidUntil;
        if (package.ExpectedBillingType != SessionBillingType.Group) client.ActivePackageId = package.PackageId;
        client.Status = "Active";
        client.UpdatedAt = DateTime.UtcNow;
        AuditPackage(clientId, package, "RetainedPackageResumed", before, request.Reason.Trim());
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static void ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new InvalidOperationException("A reason/reference of 1..1000 characters is required.");
    }

    private static string PackageAuditState(ClientPackage p) => JsonSerializer.Serialize(new
    { p.Id, p.IsActive, p.TotalSessions, p.UsedSessions, p.AmountPaid, p.ValidUntil, p.ClosureDisposition, p.RefundAmount, p.RefundConfirmedAt, p.RefundReference });

    private void AuditPackage(int clientId, ClientPackage package, string action, string before, string reason)
        => _context.ClientAuditEntries.Add(new ClientAuditEntry { ClientId = clientId, ActorUserId = _currentUser.UserId,
            Action = action, BeforeJson = before, AfterJson = PackageAuditState(package), Reason = reason.Trim() });
}
