using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize(Roles = "Owner")]
public class ExpensesController : ControllerBase
{
    private readonly ICompanyExpenseService _companyExpenseService;

    public ExpensesController(ICompanyExpenseService companyExpenseService)
    {
        _companyExpenseService = companyExpenseService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<CompanyExpenseDto>>> GetExpenses(
        [FromQuery] CompanyExpenseFilterDto filter)
    {
        return await HandleAsync<PagedResultDto<CompanyExpenseDto>>(async () =>
            Ok(await _companyExpenseService.GetExpensesAsync(filter)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompanyExpenseDto>> GetExpense(int id)
    {
        return await HandleAsync<CompanyExpenseDto>(async () =>
        {
            var result = await _companyExpenseService.GetExpenseAsync(id);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<ExpenseStatisticsDto>> GetStatistics(
        [FromQuery] CompanyExpenseFilterDto filter)
    {
        return await HandleAsync<ExpenseStatisticsDto>(async () =>
            Ok(await _companyExpenseService.GetStatisticsAsync(filter)));
    }

    [HttpGet("categories")]
    public ActionResult<List<EnumOptionDto>> GetCategories()
    {
        return Ok(Enum.GetValues<ExpenseCategory>()
            .Select(x => new EnumOptionDto
            {
                Value = (int)x,
                Name = x.ToString()
            })
            .ToList());
    }

    [HttpGet("payment-statuses")]
    public ActionResult<List<EnumOptionDto>> GetPaymentStatuses()
    {
        return Ok(Enum.GetValues<ExpensePaymentStatus>()
            .Select(x => new EnumOptionDto
            {
                Value = (int)x,
                Name = x.ToString()
            })
            .ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CompanyExpenseDto>> CreateExpense(
        CreateCompanyExpenseRequest request)
    {
        return await HandleAsync<CompanyExpenseDto>(async () =>
        {
            var result = await _companyExpenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetExpense), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CompanyExpenseDto>> UpdateExpense(
        int id,
        UpdateCompanyExpenseRequest request)
    {
        return await HandleAsync<CompanyExpenseDto>(async () =>
        {
            var result = await _companyExpenseService.UpdateExpenseAsync(id, request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("{id:int}/mark-paid")]
    public async Task<ActionResult<CompanyExpenseDto>> MarkPaid(
        int id,
        [FromBody] MarkCompanyExpensePaidRequest? request)
    {
        return await HandleAsync<CompanyExpenseDto>(async () =>
        {
            var result = await _companyExpenseService.MarkPaidAsync(id, request?.PaidAt);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("{id:int}/attachment")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CompanyExpenseDto>> UploadAttachment(
        int id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Expense attachment file is required." });

        return await HandleAsync<CompanyExpenseDto>(async () =>
        {
            await using var stream = file.OpenReadStream();
            var result = await _companyExpenseService.UploadAttachmentAsync(
                id,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpGet("{id:int}/attachment")]
    public async Task<IActionResult> DownloadAttachment(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _companyExpenseService.DownloadAttachmentAsync(id, cancellationToken);
            return result is null
                ? NotFound()
                : File(result.Content, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpDelete("{id:int}/attachment")]
    public async Task<ActionResult<CompanyExpenseDto>> DeleteAttachment(
        int id,
        CancellationToken cancellationToken)
    {
        return await HandleAsync<CompanyExpenseDto>(async () =>
        {
            var result = await _companyExpenseService.DeleteAttachmentAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        try
        {
            var deleted = await _companyExpenseService.DeleteExpenseAsync(id);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    private async Task<ActionResult<T>> HandleAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}
