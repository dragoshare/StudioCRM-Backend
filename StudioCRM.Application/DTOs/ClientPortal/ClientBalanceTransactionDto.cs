namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientBalanceTransactionDto
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}