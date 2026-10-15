using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services;

public partial class ClientPaymentService
{
    private static string Fingerprint(CreateClientPaymentRequest request) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { request.ClientPackageId, Amount = NormalizeAmount(request.Amount),
            request.Method, PaymentDate = NormalizeNullableDateTime(request.PaymentDate), request.Note }))));

    private async Task<ClientPaymentDto?> ReplayPaymentAsync(int clientId, CreateClientPaymentRequest request, ClientPaymentSource source)
    {
        if (!request.IdempotencyKey.HasValue) return null;
        if (request.IdempotencyKey == Guid.Empty) throw new InvalidOperationException("IdempotencyKey must be a non-empty UUID.");
        // The key is bound to user + source (+ organization via query filter), not to the client: reusing it for another client is a conflict.
        var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{_currentUser.UserId}:{source}:{request.IdempotencyKey}"));
        var lockId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lockBytes);
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(74203, {lockId})");
        var existing = await _context.ClientPayments.IncludingOrganizationArchive(_context.OrganizationId).AsNoTracking()
            .Where(p => p.CreatedByUserId == _currentUser.UserId && p.Source == source && p.IdempotencyKey == request.IdempotencyKey)
            .OrderBy(p => p.ClientId == clientId ? 0 : 1).ThenBy(p => p.Id)
            .FirstOrDefaultAsync();
        if (existing == null) return null;
        if (existing.ClientId != clientId || existing.RequestFingerprint != Fingerprint(request))
            throw new DbUpdateConcurrencyException("Idempotency key has already been used for a different payment.");
        return await GetPaymentDtoAsync(existing.Id);
    }

    public async Task<PaymentOperationStatusDto?> GetStaffPaymentOperationAsync(Guid key)
    {
        if (!_currentUser.IsOwner) throw new UnauthorizedAccessException("Owner required.");
        var payment = await _context.ClientPayments.IncludingOrganizationArchive(_context.OrganizationId).AsNoTracking()
            .Where(p => p.IdempotencyKey == key && p.CreatedByUserId == _currentUser.UserId && p.Source == ClientPaymentSource.StaffEntry)
            .OrderBy(p => p.Id).Select(p => new { p.Id }).FirstOrDefaultAsync();
        if (payment == null) return null;
        var dto = await GetPaymentDtoAsync(payment.Id);
        return new PaymentOperationStatusDto
        {
            Key = key,
            Operation = "CreateStaffPayment",
            State = "Completed",
            Result = dto,
            UpdatedAt = new[] { dto.CreatedAt, dto.ConfirmedAt, dto.RejectedAt, dto.ReversedAt }.Max() ?? dto.CreatedAt
        };
    }
}
