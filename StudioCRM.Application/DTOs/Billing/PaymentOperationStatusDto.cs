namespace StudioCRM.Application.DTOs.Billing;

// GET /api/owner/operations/{key}. Only staff payment creation is tracked (ClientPayment.IdempotencyKey).
public class PaymentOperationStatusDto
{
    public Guid Key { get; set; }
    public string Operation { get; set; } = string.Empty;
    // Completed: the operation was committed and Result holds its current state.
    public string State { get; set; } = string.Empty;
    public ClientPaymentDto? Result { get; set; }
    public DateTime UpdatedAt { get; set; }
}
