using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
namespace StudioCRM.Infrastructure.Services;
public partial class ClientPaymentService
{
    public async Task<ClientPaymentDto> UpdatePaymentDateAsync(int paymentId, UpdatePaymentDateRequest request)
    {
        if (!_currentUser.IsOwner && !_currentUser.IsTrainer) throw new UnauthorizedAccessException("Staff access required.");
        if (!request.PaymentDate.HasValue || !request.ExpectedPaymentDate.HasValue || request.PaymentDate.Value == DateTimeOffset.MinValue ||
            request.PaymentDate.Value > DateTimeOffset.UtcNow || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            throw new InvalidOperationException("Provide a past or current payment date, previous payment date and reason (1..1000 characters).");
        // PostgreSQL persists microseconds; normalize before concurrency checks and writes.
        var date = request.PaymentDate.Value.UtcDateTime;
        date = new DateTime(date.Ticks - date.Ticks % 10, DateTimeKind.Utc);
        var expected = request.ExpectedPaymentDate.Value.UtcDateTime;
        expected = new DateTime(expected.Ticks - expected.Ticks % 10, DateTimeKind.Utc);
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74202}, {paymentId})");
        var payment = await _context.ClientPayments.SingleOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new KeyNotFoundException("Payment not found.");
        await _context.Entry(payment).ReloadAsync();
        await EnsureStaffAccessToClientAsync(payment.ClientId);
        if (payment.Source == ClientPaymentSource.PaymentGateway || payment.Method == PaymentMethod.PaymentGateway)
            throw new InvalidOperationException("Gateway payment dates are supplied by the provider and cannot be edited manually.");
        if (payment.Status is not (ClientPaymentStatus.Confirmed or ClientPaymentStatus.PendingConfirmation))
            throw new InvalidOperationException("Only pending or confirmed payments can have their date corrected.");
        if (payment.PaymentDate != expected && payment.PaymentDate != date)
            throw new DbUpdateConcurrencyException("Payment date changed. Refresh before retrying.");
        if (payment.PaymentDate != date)
        {
            var before = payment.PaymentDate;
            payment.PaymentDate = date;
            _context.ClientAuditEntries.Add(new ClientAuditEntry { ClientId = payment.ClientId, ActorUserId = _currentUser.UserId,
                Action = "PaymentDateCorrected", Reason = request.Reason.Trim(),
                BeforeJson = JsonSerializer.Serialize(new { PaymentId = payment.Id, PaymentDate = before }),
                AfterJson = JsonSerializer.Serialize(new { PaymentId = payment.Id, PaymentDate = date }) });
            await _context.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return await GetPaymentDtoAsync(payment.Id);
    }
}
