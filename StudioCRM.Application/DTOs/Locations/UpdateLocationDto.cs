namespace StudioCRM.Application.DTOs.Locations;

public class UpdateLocationDto
{
    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public string? PaymentRecipientName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BlikPhoneNumber { get; set; }

    public string? TransferTitleTemplate { get; set; }

    public string? PaymentDescription { get; set; }
}
