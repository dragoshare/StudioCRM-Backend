namespace StudioCRM.Domain.Entities;

public class LegalEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Nip { get; set; }

    public string? Address { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? PaymentRecipientName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BlikPhoneNumber { get; set; }

    public string? TransferTitleTemplate { get; set; }

    public string? PaymentDescription { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Location> Locations { get; set; } = new List<Location>();

    public ICollection<PaymentProviderAccount> PaymentProviderAccounts { get; set; } = new List<PaymentProviderAccount>();

    public ICollection<CompanyExpense> Expenses { get; set; } = new List<CompanyExpense>();
}
