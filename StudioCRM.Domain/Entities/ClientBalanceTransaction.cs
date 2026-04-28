using StudioCRM.Domain.Enums;

namespace StudioCRM.Domain.Entities;

public class ClientBalanceTransaction
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int? ClientPackageId { get; set; }
    public ClientPackage? ClientPackage { get; set; }

    public int? SessionId { get; set; }
    public Session? Session { get; set; }

    public decimal Amount { get; set; }

    public BalanceTransactionType Type { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}