using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces.Storage;

namespace StudioCRM.Application.Interfaces;

public interface ICompanyExpenseService
{
    Task<PagedResultDto<CompanyExpenseDto>> GetExpensesAsync(CompanyExpenseFilterDto filter);
    Task<CompanyExpenseDto?> GetExpenseAsync(int id);
    Task<CompanyExpenseDto> CreateExpenseAsync(CreateCompanyExpenseRequest request);
    Task<CompanyExpenseDto?> UpdateExpenseAsync(int id, UpdateCompanyExpenseRequest request);
    Task<CompanyExpenseDto?> MarkPaidAsync(int id, DateTime? paidAt = null);
    Task<CompanyExpenseDto?> UploadAttachmentAsync(
        int id,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default);
    Task<StoredObjectDownloadDto?> DownloadAttachmentAsync(
        int id,
        CancellationToken cancellationToken = default);
    Task<CompanyExpenseDto?> DeleteAttachmentAsync(
        int id,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteExpenseAsync(int id, StudioCRM.Domain.Enums.ExpenseRecurrenceEditScope recurrenceEditScope = StudioCRM.Domain.Enums.ExpenseRecurrenceEditScope.ThisOnly);
    Task<ExpenseStatisticsDto> GetStatisticsAsync(CompanyExpenseFilterDto filter);
}
