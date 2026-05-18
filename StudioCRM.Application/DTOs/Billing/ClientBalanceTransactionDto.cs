namespace StudioCRM.Application.DTOs.Billing;

public class ClientBalanceTransactionDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? ClientPackageId { get; set; }
    public int? SessionId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
