using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.ClientPackages.Services;

public partial class ClientPackageService
{
    // Owner-only correction of the client's settlement balance (the CurrentBalance shown in CRM: every balance
    // transaction except PaymentCredit/PaymentReversal). Sets a target value through one ManualAdjustment entry,
    // so history stays complete. Positive = client credit, negative = amount the client still owes (added to the
    // next package like any negative balance). Retrying with the same target is a no-op.
    public async Task<BalanceAdjustmentResultDto> AdjustBalanceAsync(int clientId, BalanceAdjustmentRequest request)
    {
        RequireOwner(); RequireReason(request.Reason);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74101}, {clientId})");
        if (!await _context.Clients.AnyAsync(c => c.Id == clientId && !c.IsDeleted))
            throw new KeyNotFoundException("Client not found or archived.");

        var current = await CurrentBalanceAsync(clientId);
        var target = decimal.Round(request.TargetBalance, 2);
        if (current == target)
            return new BalanceAdjustmentResultDto { PreviousBalance = current, Balance = current, Adjustment = 0 };
        if (current != decimal.Round(request.ExpectedCurrentBalance, 2))
            throw new DbUpdateConcurrencyException("Client balance changed. Refresh before adjusting it.");

        var adjustment = target - current;
        _context.ClientBalanceTransactions.Add(new ClientBalanceTransaction
        {
            ClientId = clientId, Amount = adjustment, Type = BalanceTransactionType.ManualAdjustment,
            Description = "Korekta salda: " + request.Reason.Trim(), CreatedAt = DateTime.UtcNow
        });
        _context.ClientAuditEntries.Add(new ClientAuditEntry
        {
            ClientId = clientId, ActorUserId = _currentUser.UserId, Action = "BalanceAdjusted",
            BeforeJson = JsonSerializer.Serialize(new { Balance = current }),
            AfterJson = JsonSerializer.Serialize(new { Balance = target, Adjustment = adjustment }),
            Reason = request.Reason.Trim()
        });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return new BalanceAdjustmentResultDto { PreviousBalance = current, Balance = target, Adjustment = adjustment };
    }

    private Task<decimal> CurrentBalanceAsync(int clientId) => _context.ClientBalanceTransactions
        .Where(t => t.ClientId == clientId && t.Type != BalanceTransactionType.PaymentCredit && t.Type != BalanceTransactionType.PaymentReversal)
        .SumAsync(t => t.Amount);
}
