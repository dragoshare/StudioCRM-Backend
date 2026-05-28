namespace StudioCRM.Application.DTOs.Payments;

public class PaymentInstructionsDto
{
    public string? RecipientName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BlikPhoneNumber { get; set; }

    public string? TransferTitle { get; set; }

    public string? Description { get; set; }
}
