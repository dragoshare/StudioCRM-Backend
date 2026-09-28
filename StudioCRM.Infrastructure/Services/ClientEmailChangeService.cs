using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Mail;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class ClientEmailChangeService(StudioCRMDbContext db, ICurrentUserService current,
    IEmailService email, IOptions<AppSettings> settings) : IClientEmailChangeService
{
    public async Task<List<ClientEmailChangeDto>> GetAsync(int? clientId)
    {
        if (!current.IsOwner && !current.IsClient) throw new UnauthorizedAccessException();
        var query = db.ClientEmailChangeRequests.IgnoreQueryFilters().AsQueryable();
        if (!current.IsOwner) query = query.Where(x => x.Client.UserId == current.UserId && !x.Client.IsDeleted);
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId);
        return await query.OrderByDescending(x => x.Id).Take(200).Select(x => new ClientEmailChangeDto
        {
            Id = x.Id, ClientId = x.ClientId, CurrentEmail = x.CurrentEmail, RequestedEmail = x.RequestedEmail,
            Status = x.Status == "AwaitingVerification" && x.VerificationExpiresAt <= DateTime.UtcNow ? "Expired" : x.Status,
            CreatedAt = x.CreatedAt, VerificationExpiresAt = x.VerificationExpiresAt, ReviewReason = x.ReviewReason
        }).ToListAsync();
    }

    public async Task ReviewAsync(int id, ReviewClientEmailChangeRequest request)
    {
        if (!current.IsOwner) throw new UnauthorizedAccessException();
        if (request.Reason?.Length > 1000) throw new InvalidOperationException("Reason is too long.");
        string? token = null;
        string? target = null;
        if (request.Approve && (!Uri.TryCreate(settings.Value.FrontendBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new InvalidOperationException("Configure an HTTPS FrontendBaseUrl before approving email changes.");
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var change = await db.ClientEmailChangeRequests.Include(x => x.Client).ThenInclude(x => x.User)
                .SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Request not found.");
            if (change.Status is not ("Pending" or "AwaitingVerification" or "DeliveryFailed"))
                throw new InvalidOperationException("Request is already closed.");
            if (change.Client.User == null || change.Client.PortalAccessBlocked || !change.Client.User.IsActive)
                throw new InvalidOperationException("Client account is unavailable.");
            if (request.Approve && await db.ClientEmailChangeRequests.AnyAsync(x => x.ClientId == change.ClientId && x.Id != id &&
                    (x.Status == "Pending" || x.Status == "DeliveryFailed" ||
                     (x.Status == "AwaitingVerification" && x.VerificationExpiresAt > DateTime.UtcNow))))
                throw new InvalidOperationException("Another active request exists for this client. Review the latest request first.");
            change.ReviewedAt = DateTime.UtcNow;
            change.ReviewedByUserId = current.UserId;
            change.ReviewReason = request.Reason?.Trim();
            if (!request.Approve)
            {
                change.Status = "Rejected";
                change.VerificationTokenHash = null;
            }
            else
            {
                target = change.RequestedEmail.Trim().ToLowerInvariant();
                if (!System.Net.Mail.MailAddress.TryCreate(target, out var address) || address.Address != target || target.Length > 320)
                    throw new InvalidOperationException("Email is invalid.");
                if (await db.Users.AnyAsync(x => x.Email.ToLower() == target)) throw new InvalidOperationException("Email is already in use.");
                change.CurrentEmail = change.Client.User.Email;
                change.RequestedEmail = target;
                token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                change.VerificationTokenHash = Hash(token);
                change.VerificationExpiresAt = DateTime.UtcNow.AddHours(24);
                change.Status = "AwaitingVerification";
            }
            db.ClientAuditEntries.Add(new ClientAuditEntry { ClientId = change.ClientId, ActorUserId = current.UserId,
                Action = request.Approve ? "LoginEmailChangeApproved" : "LoginEmailChangeRejected", Reason = request.Reason,
                AfterJson = JsonSerializer.Serialize(new { change.Id, change.Status, change.RequestedEmail }) });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        if (token != null)
        {
            try
            {
                var link = settings.Value.FrontendBaseUrl.TrimEnd('/') + "/verify-email-change?requestId=" + id + "&token=" + token;
                await email.SendLoginEmailChangeVerificationAsync(target!, link);
            }
            catch
            {
                // Do not invalidate a newer resend or a token already verified concurrently.
                var tokenHash = Hash(token);
                await db.ClientEmailChangeRequests.Where(x => x.Id == id && x.VerificationTokenHash == tokenHash && x.Status == "AwaitingVerification")
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "DeliveryFailed").SetProperty(x => x.VerificationTokenHash, (string?)null));
                throw new InvalidOperationException("Verification email could not be sent. Retry approval to send a new link.");
            }
        }
    }

    public async Task VerifyAsync(VerifyClientEmailChangeRequest request)
    {
        if (request.Token?.Length != 64) throw new InvalidOperationException("Invalid or expired verification link.");
        var hash = Hash(request.Token);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var change = await db.ClientEmailChangeRequests.Include(x => x.Client).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == request.RequestId && x.VerificationTokenHash == hash &&
                x.Status == "AwaitingVerification" && x.VerificationExpiresAt > DateTime.UtcNow)
            ?? throw new InvalidOperationException("Invalid or expired verification link.");
        var user = change.Client.User;
        if (user == null || !user.IsActive || change.Client.PortalAccessBlocked || user.Email != change.CurrentEmail)
            throw new InvalidOperationException("Client account changed or is unavailable.");
        if (await db.Users.AnyAsync(x => x.Id != user.Id && x.Email.ToLower() == change.RequestedEmail.ToLower()))
            throw new InvalidOperationException("Email is already in use.");
        var oldEmail = user.Email;
        user.Email = change.RequestedEmail;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        change.Status = "Completed";
        change.VerifiedAt = DateTime.UtcNow;
        change.VerificationTokenHash = null;
        foreach (var token in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync()) token.RevokedAt = DateTime.UtcNow;
        foreach (var token in await db.PasswordResetTokens.Where(t => t.UserId == user.Id && !t.IsUsed).ToListAsync()) token.IsUsed = true;
        foreach (var token in await db.EmailVerificationTokens.Where(t => t.UserId == user.Id && t.UsedAt == null).ToListAsync()) token.UsedAt = DateTime.UtcNow;
        db.ClientAuditEntries.Add(new ClientAuditEntry { ClientId = change.ClientId, ActorUserId = user.Id, Action = "LoginEmailChanged",
            BeforeJson = JsonSerializer.Serialize(new { LoginEmail = oldEmail }), AfterJson = JsonSerializer.Serialize(new { LoginEmail = user.Email }) });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task CancelOwnAsync(int id)
    {
        if (!current.IsClient) throw new UnauthorizedAccessException();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var change = await db.ClientEmailChangeRequests.SingleOrDefaultAsync(x => x.Id == id && x.Client.UserId == current.UserId)
            ?? throw new KeyNotFoundException("Request not found.");
        if (change.Status is "Completed" or "Rejected" or "Cancelled") throw new InvalidOperationException("Request is already closed.");
        change.Status = "Cancelled";
        change.VerificationTokenHash = null;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
