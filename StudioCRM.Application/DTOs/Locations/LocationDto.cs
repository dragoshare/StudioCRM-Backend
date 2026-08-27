using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Locations;

public class LocationDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; }

    public int? LegalEntityId { get; set; }

    public string? LegalEntityName { get; set; }

    public string? PaymentRecipientName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BlikPhoneNumber { get; set; }

    public string? TransferTitleTemplate { get; set; }

    public string? PaymentDescription { get; set; }

    public FiscalReceiptMode FiscalReceiptMode { get; set; } = FiscalReceiptMode.Manual;

    public string? FiscalRegisterName { get; set; }

    public string? FiscalRegisterNumber { get; set; }

    public DateTime CreatedAt { get; set; }
}
