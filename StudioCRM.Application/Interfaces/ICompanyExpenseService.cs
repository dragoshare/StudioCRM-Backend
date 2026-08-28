using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface ICompanyExpenseService
{
    Task<PagedResultDto<CompanyExpenseDto>> GetExpensesAsync(CompanyExpenseFilterDto filter);
    Task<CompanyExpenseDto?> GetExpenseAsync(int id);
    Task<CompanyExpenseDto> CreateExpenseAsync(CreateCompanyExpenseRequest request);
    Task<CompanyExpenseDto?> UpdateExpenseAsync(int id, UpdateCompanyExpenseRequest request);
    Task<CompanyExpenseDto?> MarkPaidAsync(int id, DateTime? paidAt = null);
    Task<bool> DeleteExpenseAsync(int id);
    Task<ExpenseStatisticsDto> GetStatisticsAsync(CompanyExpenseFilterDto filter);
}
