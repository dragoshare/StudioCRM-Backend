namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientPackageSettlementDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public ClientActivePackageDto? ActivePackage { get; set; }

    public List<ClientCountedSessionDto> CountedSessions { get; set; } = new();

    public decimal CurrentBalance { get; set; }

    public List<ClientBalanceTransactionDto> BalanceTransactions { get; set; } = new();
}