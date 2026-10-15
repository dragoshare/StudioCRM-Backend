using System.ComponentModel.DataAnnotations;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class ClientPaymentFilterDto : IValidatableObject
{
    public const string SortValues = "paymentDateDesc|paymentDate|amountDesc|amount";

    [StringLength(100)]
    public string? Search { get; set; }
    public int? ClientId { get; set; }
    public int? LocationId { get; set; }
    public int? LegalEntityId { get; set; }
    public ClientPaymentStatus? Status { get; set; }
    public ClientPaymentSource? Source { get; set; }
    public ReceiptStatus? ReceiptStatus { get; set; }
    public PaymentMethod? Method { get; set; }
    public string? PaymentProvider { get; set; }
    // From/To and the date sorts use PaymentDate (date of the payment), not CreatedAt.
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? AmountMin { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? AmountMax { get; set; }
    public bool? HasOverpayment { get; set; }
    [RegularExpression("^(" + SortValues + ")$")]
    public string? Sort { get; set; }
    // Out-of-range values are clamped by the service (Page 1..100000, PageSize 1..100).
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AmountMin > AmountMax)
            yield return new ValidationResult("AmountMin cannot be greater than AmountMax.", new[] { nameof(AmountMin), nameof(AmountMax) });
        if (From > To)
            yield return new ValidationResult("From cannot be later than To.", new[] { nameof(From), nameof(To) });
        if (Method.HasValue && !Enum.IsDefined(Method.Value))
            yield return new ValidationResult("Unknown payment method.", new[] { nameof(Method) });
    }
}
