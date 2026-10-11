using System.ComponentModel.DataAnnotations;
namespace StudioCRM.Application.DTOs.Billing;
public class UpdatePaymentDateRequest
{
    [Required] public DateTimeOffset? PaymentDate { get; set; }
    [Required] public DateTimeOffset? ExpectedPaymentDate { get; set; }
    [Required, StringLength(1000, MinimumLength = 1)] public string Reason { get; set; } = "";
}
