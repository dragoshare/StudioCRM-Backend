using StudioCRM.Domain.Enums;

namespace StudioCRM.Domain.Entities;

public class Location
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public int? LegalEntityId { get; set; }

    public string? PaymentRecipientName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BlikPhoneNumber { get; set; }

    public string? TransferTitleTemplate { get; set; }

    public string? PaymentDescription { get; set; }

    public FiscalReceiptMode FiscalReceiptMode { get; set; } = FiscalReceiptMode.Manual;

    public string? FiscalRegisterName { get; set; }

    public string? FiscalRegisterNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public LegalEntity? LegalEntity { get; set; }

    public ICollection<TrainerLocation> TrainerLocations { get; set; } = new List<TrainerLocation>();

    public ICollection<Client> Clients { get; set; } = new List<Client>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();

    public ICollection<TrainerContractLocation> TrainerContractLocations { get; set; } = new List<TrainerContractLocation>();

    public ICollection<PaymentProviderAccount> PaymentProviderAccounts { get; set; } = new List<PaymentProviderAccount>();

    public string? CalendarEmail { get; set; }
}
