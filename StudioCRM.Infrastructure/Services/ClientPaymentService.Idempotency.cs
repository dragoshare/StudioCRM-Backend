using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Domain.Enums;
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
        var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{clientId}:{_currentUser.UserId}:{source}:{request.IdempotencyKey}"));
        var lockId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lockBytes);
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(74203, {lockId})");
        var existing = await _context.ClientPayments.AsNoTracking().FirstOrDefaultAsync(p => p.ClientId == clientId &&
            p.CreatedByUserId == _currentUser.UserId && p.Source == source && p.IdempotencyKey == request.IdempotencyKey);
        if (existing == null) return null;
        if (existing.RequestFingerprint != Fingerprint(request))
            throw new DbUpdateConcurrencyException("Idempotency key has already been used for a different payment.");
        return await GetPaymentDtoAsync(existing.Id);
    }
}
