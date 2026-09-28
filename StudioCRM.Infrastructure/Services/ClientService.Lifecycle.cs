using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientService
{
    private void EnsureOwner()
    {
        if (!_currentUser.IsOwner) throw new UnauthorizedAccessException("Only owner can manage client lifecycle.");
    }

    private static string NormalizeContactEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static void ValidateClientIdentity(string firstName, string lastName, string? email)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new InvalidOperationException("First name and last name are required.");
        var normalized = NormalizeContactEmail(email);
        if (normalized.Length > 320 || (normalized.Length > 0 &&
            (!MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)))
            throw new InvalidOperationException("Contact email is invalid.");
    }

    public async Task<ClientArchiveCheckDto> CheckArchiveAsync(int id)
    {
        EnsureOwner();
        if (!await _context.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == id))
            throw new KeyNotFoundException("Client does not exist.");
        var result = new ClientArchiveCheckDto { ClientId = id };
        if (await _context.SessionParticipants.AnyAsync(p => p.ClientId == id &&
                p.Session.EndAt > DateTime.UtcNow && p.Session.Status != "Cancelled"))
            result.Blockers.Add("FutureSessions");
        if (await _context.SessionParticipants.AnyAsync(p => p.ClientId == id &&
                p.Session.EndAt <= DateTime.UtcNow && p.Session.Status != "Cancelled" && p.Session.Status != "Completed"))
            result.Blockers.Add("UnfinishedSessions");
        if (await _context.ClientPackages.AnyAsync(p => p.ClientId == id && p.IsActive))
            result.Blockers.Add("ActivePackages");
        if (await _context.ClientPackages.AnyAsync(p => p.ClientId == id && p.ClosureDisposition == "RefundPending"))
            result.Blockers.Add("PendingRefunds");
        if (await _context.ClientPayments.AnyAsync(p => p.ClientId == id && p.Status == ClientPaymentStatus.PendingConfirmation))
            result.Blockers.Add("PendingPayments");
        if (await _context.ClientPackages.AnyAsync(p => p.ClientId == id && p.TotalPrice > p.AmountPaid))
            result.Blockers.Add("UnpaidPackages");
        if ((await _context.ClientBalanceTransactions.Where(t => t.ClientId == id &&
                t.Type != BalanceTransactionType.PaymentCredit && t.Type != BalanceTransactionType.PaymentReversal)
            .SumAsync(t => (decimal?)t.Amount) ?? 0) != 0)
            result.Blockers.Add("OutstandingBalance");
        return result;
    }

    public async Task<List<ClientArchiveResultDto>> ArchiveManyAsync(BulkArchiveClientsRequest request)
    {
        EnsureOwner();
        if (request.ClientIds == null || request.ClientIds.Count == 0 || request.ClientIds.Count > 100)
            throw new InvalidOperationException("Provide between 1 and 100 client IDs.");
        var result = new List<ClientArchiveResultDto>();
        foreach (var id in request.ClientIds.Distinct())
        {
            try
            {
                var archived = await DeleteAsync(id);
                result.Add(new() { ClientId = id, Archived = archived, Error = archived ? null : "NotFoundOrArchived" });
            }
            catch (InvalidOperationException ex)
            {
                result.Add(new() { ClientId = id, Error = ex.Message });
            }
        }
        return result;
    }

    public async Task<bool> SetPortalAccessAsync(int id, bool blocked)
    {
        EnsureOwner();
        var client = await _context.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return false;
        var before = ClientAuditState(client);
        client.PortalAccessBlocked = blocked;
        client.UpdatedAt = DateTime.UtcNow;
        if (blocked)
        {
            await CancelClientInvitationsAsync(id);
            await RevokeClientAccessTokensAsync(client);
        }
        AuditClient(client, blocked ? "PortalBlocked" : "PortalUnblocked", before);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task RevokeClientAccessTokensAsync(StudioCRM.Domain.Entities.Client client)
    {
        if (client.UserId.HasValue)
            foreach (var token in await _context.RefreshTokens.Where(t => t.UserId == client.UserId && t.RevokedAt == null).ToListAsync())
                token.RevokedAt = DateTime.UtcNow;
        foreach (var change in await _context.ClientEmailChangeRequests.Where(x => x.ClientId == client.Id &&
                     (x.Status == "Pending" || x.Status == "AwaitingVerification" || x.Status == "DeliveryFailed")).ToListAsync())
        {
            change.Status = "Cancelled";
            change.VerificationTokenHash = null;
        }
    }

    private async Task CancelClientInvitationsAsync(int id)
    {
        var invitations = await _context.Invitations.Where(i => i.ClientId == id && !i.IsAccepted && i.CancelledAt == null).ToListAsync();
        foreach (var invitation in invitations) invitation.CancelledAt = DateTime.UtcNow;
    }

    public async Task<bool> DeletePermanentlyAsync(int id)
    {
        EnsureOwner();
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var client = await _context.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return false;
        if (!client.IsDeleted) throw new InvalidOperationException("Archive the client first.");
        if (client.UserId.HasValue || client.ActivePackageId.HasValue || client.NextPackageId.HasValue ||
            !string.IsNullOrWhiteSpace(client.TrainingPlanFileId) || !string.IsNullOrWhiteSpace(client.TrainingPlanUrl) ||
            !string.IsNullOrWhiteSpace(client.GoogleDriveFolderId) ||
            await _context.SessionParticipants.IgnoreQueryFilters().AnyAsync(x => x.ClientId == id) ||
            await _context.ClientPackages.IgnoreQueryFilters().AnyAsync(x => x.ClientId == id) ||
            await _context.ClientPayments.IgnoreQueryFilters().AnyAsync(x => x.ClientId == id) ||
            await _context.ClientBalanceTransactions.IgnoreQueryFilters().AnyAsync(x => x.ClientId == id) ||
            await _context.ClientMilestones.IgnoreQueryFilters().AnyAsync(x => x.ClientId == id) ||
            await _context.ClientEmailChangeRequests.IgnoreQueryFilters().AnyAsync(x => x.ClientId == id) ||
            await _context.Invitations.AnyAsync(x => x.ClientId == id))
            throw new InvalidOperationException("Client has account or history. Keep this client archived.");
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
}
