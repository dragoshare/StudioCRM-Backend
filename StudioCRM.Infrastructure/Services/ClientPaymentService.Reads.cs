using StudioCRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Domain.Enums;
namespace StudioCRM.Infrastructure.Services;

public partial class ClientPaymentService
{
    public async Task<ClientBillingTotalsDto> GetClientTotalsAsync(int clientId)
    {
        await EnsureStaffAccessToClientAsync(clientId, readOnly: true);
        var result = await _context.Clients.IncludingOrganizationArchive(_context.OrganizationId).AsNoTracking().Where(c => c.Id == clientId)
            .Select(c => new ClientBillingTotalsDto
            {
                ClientId = c.Id, ClientName = c.FirstName + " " + c.LastName,
                TotalAmountDue = _context.ClientPackages.Where(p => p.ClientId == c.Id)
                    .Sum(p => Math.Max(0, p.TotalPrice - p.AmountPaid)),
                CurrentBalance = _context.ClientBalanceTransactions.Where(t => t.ClientId == c.Id &&
                    t.Type != BalanceTransactionType.PaymentCredit && t.Type != BalanceTransactionType.PaymentReversal).Sum(t => t.Amount)
            }).SingleAsync();
        var active = await _context.ClientPackages.AsNoTracking().Where(p => p.ClientId == clientId && p.IsActive &&
            p.ClosureDisposition == null && (p.BillingMode == StudioCRM.Domain.Enums.PackageBillingMode.SessionBased && p.ExpectedBillingType != SessionBillingType.Group))
            .OrderByDescending(p => p.ActivatedAt ?? p.PurchaseDate).ThenByDescending(p => p.Id)
            .Select(p => new { p.Id, p.Name, p.TotalPrice, p.AmountPaid, p.PaymentStatus }).FirstOrDefaultAsync();
        result.ActiveClientPackageId = active?.Id;
        result.ActivePackageName = active?.Name;
        result.ActivePackageTotalPrice = active?.TotalPrice ?? 0;
        result.ActivePackageAmountPaid = active?.AmountPaid ?? 0;
        result.ActivePackageAmountDue = active == null ? 0 : Math.Max(0, active.TotalPrice - active.AmountPaid);
        result.ActivePackagePaymentStatus = active?.PaymentStatus.ToString() ?? "";
        return result;
    }

    public async Task<PagedResultDto<ClientPaymentDto>> GetPendingPageAsync(ClientPaymentFilterDto filter)
    {
        if (!_currentUser.IsOwner && !_currentUser.IsTrainer) throw new UnauthorizedAccessException();
        var query = BasePaymentQuery().AsNoTracking().Where(p => p.Status == ClientPaymentStatus.PendingConfirmation &&
            p.Source != ClientPaymentSource.PaymentGateway);
        if (!_currentUser.IsOwner)
        {
            query = query.Where(p => !p.Client.IsDeleted && p.Client.Trainer != null && p.Client.Trainer.UserId == _currentUser.UserId);
        }
        return await ToPagedPaymentResultAsync(ApplyPaymentFilters(query, filter), filter);
    }
}
