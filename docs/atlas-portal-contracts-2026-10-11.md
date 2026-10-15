# Additive Trainer Portal API contracts (2026-10-11)

Owner/Trainer bearer authorization; portal personal lists use the logged-in trainer profile. No organization selection is introduced. Existing routes retained.

| GET route | Parameters / result |
| --- | --- |
| /api/trainer-portal/sessions | from/to optional together, positive range <=93 days, overlap [from,to); legacy session DTO list |
| /api/trainer-portal/clients/page | page=1, pageSize=25, search<=100, locationId, status<=30, hasActivePackage, sort=name/nameDesc/created/createdDesc/balance/balanceDesc/packageUsage/packageUsageDesc; PagedResultDto<TrainerPortalClientDto>; details: FRONT-listy-klientow-i-wplat.md |
| /api/trainer-portal/group-clients | locationId required, page/pageSize/search/status; name order; GroupAccessEnabled membership and trainer location required; PagedResultDto<GroupClientOptionDto> |
| /api/trainer-portal/clients/{id}/sessions/recent | latest 3 completed sessions, totalCount; PagedResultDto<ClientSessionHistoryDto> |
| /api/trainer-portal/clients/{id}/payments | ClientPaymentFilterDto; PagedResultDto<ClientPaymentDto> |
| /api/trainer-portal/clients/{id}/packages/history | page=1/pageSize=25; existing package history |
| /api/trainer-portal/clients/{id}/billing/summary | ClientBillingTotalsDto, no histories |
| /api/trainer-portal/payments/pending/page | ClientPaymentFilterDto; pending non-gateway payments, own clients for trainer; PagedResultDto<ClientPaymentDto> |
| /api/Packages | optional locationId/isActive; existing list response |
| /api/Packages/options | page/pageSize/search/locationId/isActive=true; name order; PagedResultDto<PackageOptionDto>; status/sort not applied |
| /api/trainer-portal/dashboard/summary | TrainerDashboardSummaryDto, maximum 8 upcoming sessions |
| /api/trainer-portal/settlement/summary | year/month; TrainerSettlementSummaryDto without item lists |
| /api/Notifications/page | page=1/pageSize=25/category/isRead; PagedResultDto<NotificationDto> |
| /api/Notifications/{id} | current user's NotificationDto; 404 otherwise |

PortalPageFilter and notification paging reject page outside 1..100000 or pageSize outside 1..100. Payment pagination normalizes to safe bounds. See DTO definitions below for defaults/types/nullability. JSON property names use camelCase. Existing enum serialization is unchanged; string-typed fields remain strings. PagedResultDto contains items,totalCount,page,pageSize,totalPages.

Errors: 400 invalid parameters/business validation; 401 absent/invalid/revoked JWT; 403 role/access denied; 404 missing or inaccessible object where access is masked; 409 database/concurrency conflict (including conflicting idempotency payload); 502 upstream HTTP failure. Existing controller-specific error bodies remain; do not assume all errors have identical shape.

Payment DTO adds canConfirm. CreateClientPaymentRequest adds optional idempotencyKey. Eligibility is advisory; final write rechecks authorization and state.

## DTO source snapshot

The following definitions are copied from the implementation, including referenced DTOs and enums. C# nullable annotations show nullable fields. They are a source contract reference, not a change to serialization.

### ClientBalanceTransactionDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class ClientBalanceTransactionDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? ClientPackageId { get; set; }
    public int? SessionId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

```

### ClientBillingSummaryDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class ClientBillingSummaryDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public decimal TotalAmountDue { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal ActivePackageTotalPrice { get; set; }
    public decimal ActivePackageAmountPaid { get; set; }
    public decimal ActivePackageAmountDue { get; set; }

    public int? ActiveClientPackageId { get; set; }
    public string? ActivePackageName { get; set; }
    public string ActivePackagePaymentStatus { get; set; } = string.Empty;

    public List<ClientPackageBillingDto> Packages { get; set; } = new();
    public List<ClientPaymentDto> Payments { get; set; } = new();
}

```

### ClientBillingTotalsDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class ClientBillingTotalsDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public decimal TotalAmountDue { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal ActivePackageTotalPrice { get; set; }
    public decimal ActivePackageAmountPaid { get; set; }
    public decimal ActivePackageAmountDue { get; set; }

    public int? ActiveClientPackageId { get; set; }
    public string? ActivePackageName { get; set; }
    public string ActivePackagePaymentStatus { get; set; } = string.Empty;

}

```

### ClientPackageBillingDto.cs
```csharp
using StudioCRM.Application.DTOs.Payments;

namespace StudioCRM.Application.DTOs.Billing;

public class ClientPackageBillingDto
{
    public string PackageType { get; set; } = string.Empty;
    public DateTime? NextSessionAt { get; set; }
    public int ClientPackageId { get; set; }
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public string? ClosureDisposition { get; set; }
    public string? ClosureReason { get; set; }
    public string Origin { get; set; } = "";
    public DateTime? ClosedAt { get; set; }
    public decimal RefundAmount { get; set; }
    public DateTime? RefundConfirmedAt { get; set; }
    public string? RefundReference { get; set; }
    public string ActivationMode { get; set; } = string.Empty;

    public int TotalSessions { get; set; }
    public int SessionsPerWeek { get; set; }
    public int UsedSessions { get; set; }
    public int RemainingSessions { get; set; }

    public decimal TotalPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal BalanceApplied { get; set; }
    public decimal ExpectedUnitPrice { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "PLN";
    public string ExpectedBillingType { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaymentDueDate { get; set; }
    public DateTime? ActivatedAt { get; set; }

    public PaymentInstructionsDto? PaymentInstructions { get; set; }
}

```

### ClientPaymentDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class ClientPaymentDto
{
    public bool CanConfirm { get; set; }
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public int? ClientPackageId { get; set; }
    public string? PackageName { get; set; }

    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public int? LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }
    public int? PaymentProviderAccountId { get; set; }
    public string? PaymentProviderAccountName { get; set; }

    public decimal Amount { get; set; }
    public decimal AppliedToPackageAmount { get; set; }
    public decimal BalanceCreditAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public PaymentMethod Method { get; set; }
    public ClientPaymentStatus Status { get; set; }
    public ClientPaymentSource Source { get; set; }

    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ReversedAt { get; set; }

    public int? CreatedByUserId { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public int? RejectedByUserId { get; set; }
    public int? ReversedByUserId { get; set; }

    public string? Note { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReversalReason { get; set; }
    public string? ExternalPaymentId { get; set; }
    public string? PaymentProvider { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? ProviderStatus { get; set; }
    public decimal ProviderFeeAmount { get; set; }
    public decimal ProviderNetAmount { get; set; }
    public DateTime? ProviderPayoutDate { get; set; }
    public DateTime? ProviderSettledAt { get; set; }
    public string? ProviderSettlementId { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTime? CheckoutExpiresAt { get; set; }
    public DateTime? WebhookReceivedAt { get; set; }

    public bool ReceiptRequired { get; set; }
    public string ReceiptStatus { get; set; } = string.Empty;
    public string? ReceiptNumber { get; set; }
    public DateTime? ReceiptIssuedAt { get; set; }
    public DateTime? ReceiptSentAt { get; set; }
    public int? ReceiptIssuedByUserId { get; set; }
    public string? ReceiptNote { get; set; }
}

```

### ClientPaymentFilterDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class ClientPaymentFilterDto
{
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? Search { get; set; }
    public int? ClientId { get; set; }
    public int? LocationId { get; set; }
    public int? LegalEntityId { get; set; }
    public ClientPaymentStatus? Status { get; set; }
    public ClientPaymentSource? Source { get; set; }
    public ReceiptStatus? ReceiptStatus { get; set; }
    public string? PaymentProvider { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public bool? HasOverpayment { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

```

### CompanyExpenseDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CompanyExpenseDto
{
    public int Id { get; set; }

    public int LegalEntityId { get; set; }
    public string LegalEntityName { get; set; } = string.Empty;

    public int? LocationId { get; set; }
    public string? LocationName { get; set; }

    public ExpenseCategory Category { get; set; }
    public ExpensePaymentStatus PaymentStatus { get; set; }

    public string VendorName { get; set; } = string.Empty;
    public string? VendorNip { get; set; }
    public string? InvoiceNumber { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }

    public bool IsRecurring { get; set; }
    public string? RecurringGroupId { get; set; }
    public int? RecurrenceIntervalMonths { get; set; }
    public DateTime? RecurrenceStartDate { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceDayOfMonth { get; set; }
    public int? RecurrenceInstanceNumber { get; set; }

    public int? CreatedByUserId { get; set; }
    public int? PaidByUserId { get; set; }

    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

```

### CompanyExpenseFilterDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CompanyExpenseFilterDto
{
    public int? LegalEntityId { get; set; }
    public int? LocationId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public ExpensePaymentStatus? PaymentStatus { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public DateTime? PaidFrom { get; set; }
    public DateTime? PaidTo { get; set; }
    public string? Search { get; set; }
    public bool? IsRecurring { get; set; }
    public string? RecurringGroupId { get; set; }
    public bool? IsOverdue { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

```

### CreateClientPaymentRequest.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CreateClientPaymentRequest
{
    public Guid? IdempotencyKey { get; set; }
    public int? ClientId { get; set; }
    public int? ClientPackageId { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }

    public DateTime? PaymentDate { get; set; }
    public string? Note { get; set; }
}

```

### CreateCompanyExpenseRequest.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CreateCompanyExpenseRequest
{
    public int LegalEntityId { get; set; }
    public int? LocationId { get; set; }

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public ExpensePaymentStatus PaymentStatus { get; set; } = ExpensePaymentStatus.Unpaid;

    public string VendorName { get; set; } = string.Empty;
    public string? VendorNip { get; set; }
    public string? InvoiceNumber { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }

    public bool IsRecurring { get; set; }
    public string? RecurringGroupId { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurringOccurrencesCount { get; set; }
}

```

### EnumOptionDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class EnumOptionDto
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}

```

### ExpenseStatisticsDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class ExpenseStatisticsDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int ExpenseCount { get; set; }
    public int PaidCount { get; set; }
    public int UnpaidCount { get; set; }
    public int OverdueCount { get; set; }

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal PaidGrossAmount { get; set; }
    public decimal UnpaidGrossAmount { get; set; }
    public decimal OverdueGrossAmount { get; set; }

    public decimal RevenueGrossAmount { get; set; }
    public decimal PaymentProviderFeeAmount { get; set; }
    public decimal RevenueNetAmount { get; set; }
    public decimal OperatingProfitGrossAmount { get; set; }
    public decimal OperatingProfitNetAmount { get; set; }

    public List<ExpenseBreakdownDto> ByLegalEntity { get; set; } = new();
    public List<ExpenseBreakdownDto> ByLocation { get; set; } = new();
    public List<ExpenseBreakdownDto> ByCategory { get; set; } = new();
    public List<ExpenseBreakdownDto> ByPaymentStatus { get; set; } = new();
    public List<ExpenseBreakdownDto> ByMonth { get; set; } = new();
}

public class ExpenseBreakdownDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal PaidGrossAmount { get; set; }
    public decimal UnpaidGrossAmount { get; set; }
}

```

### IssueReceiptRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class IssueReceiptRequest
{
    public string? ReceiptNumber { get; set; }

    public string? ReceiptNote { get; set; }
}

```

### LegalEntityDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class LegalEntityDto
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

    public string? TermsVersion { get; set; }

    public string? TermsUrl { get; set; }

    public DateTime? TermsPublishedAt { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

```

### MarkCompanyExpensePaidRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class MarkCompanyExpensePaidRequest
{
    public DateTime? PaidAt { get; set; }
}

```

### PagedResultDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class PagedResultDto<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new();
}

```

### PaymentConfigurationDto.cs
```csharp
using StudioCRM.Application.DTOs.Locations;

namespace StudioCRM.Application.DTOs.Billing;

public class PaymentConfigurationDto
{
    public List<LegalEntityDto> LegalEntities { get; set; } = new();

    public List<PaymentProviderAccountDto> PaymentProviderAccounts { get; set; } = new();

    public List<LocationDto> Locations { get; set; } = new();
}

```

### PaymentProviderAccountDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class PaymentProviderAccountDto
{
    public int Id { get; set; }

    public int LegalEntityId { get; set; }

    public string LegalEntityName { get; set; } = string.Empty;

    public int? LocationId { get; set; }

    public string? LocationName { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? MerchantId { get; set; }

    public string? PosId { get; set; }

    public string? AccountKey { get; set; }

    public bool IsActive { get; set; }

    public bool WebhookSecretConfigured { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

```

### RejectClientPaymentRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class RejectClientPaymentRequest
{
    public string? Reason { get; set; }
}

```

### RevenueAnalysisFilterDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class RevenueAnalysisFilterDto
{
    public int? LocationId { get; set; }
    public int? LegalEntityId { get; set; }
    public int? TrainerId { get; set; }
    public int? ClientId { get; set; }
    public int? ClientPackageId { get; set; }
    public PaymentMethod? Method { get; set; }
    public string? PaymentProvider { get; set; }
    public bool? IsRenewal { get; set; }
    public bool? HasProviderFee { get; set; }
    public bool? IsProviderSettled { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime? PayoutFrom { get; set; }
    public DateTime? PayoutTo { get; set; }
}

```

### RevenueStatisticsDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class RevenueStatisticsDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime? PayoutFrom { get; set; }
    public DateTime? PayoutTo { get; set; }

    public int PaymentCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ProviderFeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal AppliedToPackageAmount { get; set; }
    public decimal BalanceCreditAmount { get; set; }
    public decimal NewPaymentGrossAmount { get; set; }
    public decimal RenewalPaymentGrossAmount { get; set; }
    public decimal WithoutPackageGrossAmount { get; set; }

    public List<RevenueBreakdownDto> ByLocation { get; set; } = new();
    public List<RevenueBreakdownDto> ByLegalEntity { get; set; } = new();
    public List<RevenueBreakdownDto> ByTrainer { get; set; } = new();
    public List<RevenueBreakdownDto> ByPackageType { get; set; } = new();
    public List<RevenueBreakdownDto> ByPackage { get; set; } = new();
    public List<RevenueBreakdownDto> ByClient { get; set; } = new();
    public List<RevenueBreakdownDto> ByPaymentMethod { get; set; } = new();
    public List<RevenueBreakdownDto> ByPaymentProvider { get; set; } = new();
    public List<RevenueBreakdownDto> ByPaymentLifecycle { get; set; } = new();
    public List<RevenueBreakdownDto> ByMonth { get; set; } = new();
}

public class RevenueBreakdownDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int PaymentCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ProviderFeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal AppliedToPackageAmount { get; set; }
    public decimal BalanceCreditAmount { get; set; }
}

```

### ReverseClientPaymentRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class ReverseClientPaymentRequest
{
    public string? Reason { get; set; }
}

```

### TrainerCostAnalysisFilterDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class TrainerCostAnalysisFilterDto
{
    public int? TrainerId { get; set; }
    public int? LocationId { get; set; }
    public int? LegalEntityId { get; set; }
    public int? ClientId { get; set; }
    public int? ClientPackageId { get; set; }
    public bool? IsCoveredByContract { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

```

### TrainerCostBreakdownDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class TrainerCostBreakdownDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SessionsCount { get; set; }
    public int ParticipantsCount { get; set; }
    public decimal BillableHours { get; set; }
    public decimal RevenueAmount { get; set; }
    public decimal TrainerCostAmount { get; set; }
    public decimal PotentialTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }
}

```

### TrainerCostStatisticsDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class TrainerCostStatisticsDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int SessionsCount { get; set; }
    public int CoveredSessionsCount { get; set; }
    public int UncoveredSessionsCount { get; set; }
    public int ParticipantsCount { get; set; }
    public decimal BillableHours { get; set; }
    public decimal RevenueAmount { get; set; }
    public decimal TrainerCostAmount { get; set; }
    public decimal PotentialTrainerCostAmount { get; set; }
    public decimal UncoveredPotentialTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }

    public List<TrainerCostBreakdownDto> ByTrainer { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByLocation { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByLegalEntity { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByClient { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByPackage { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByMonth { get; set; } = new();
}

```

### TrainerSessionParticipantProfitabilityDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class TrainerSessionParticipantProfitabilityDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int? ClientPackageId { get; set; }
    public string? PackageName { get; set; }
    public string AttendanceStatus { get; set; } = string.Empty;
    public int SessionsCharged { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RevenueAmount { get; set; }
    public decimal AllocatedTrainerCostAmount { get; set; }
    public decimal PotentialAllocatedTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }
}

```

### TrainerSessionProfitabilityDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class TrainerSessionProfitabilityDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;

    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int? LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }

    public string SessionType { get; set; } = string.Empty;
    public int ParticipantsCount { get; set; }
    public decimal BillableHours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal? GroupSessionRate { get; set; }

    public bool IsCoveredByContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractNumber { get; set; }

    public decimal RevenueAmount { get; set; }
    public decimal TrainerCostAmount { get; set; }
    public decimal PotentialTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }

    public List<TrainerSessionParticipantProfitabilityDto> Participants { get; set; } = new();
}

```

### UpdateCompanyExpenseRequest.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class UpdateCompanyExpenseRequest
{
    public int LegalEntityId { get; set; }
    public int? LocationId { get; set; }

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public ExpensePaymentStatus PaymentStatus { get; set; } = ExpensePaymentStatus.Unpaid;

    public string VendorName { get; set; } = string.Empty;
    public string? VendorNip { get; set; }
    public string? InvoiceNumber { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }

    public bool IsRecurring { get; set; }
    public string? RecurringGroupId { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurringOccurrencesCount { get; set; }
    public ExpenseRecurrenceEditScope RecurrenceEditScope { get; set; } = ExpenseRecurrenceEditScope.ThisOnly;
}

```

### UpdatePaymentProviderSettlementRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class UpdatePaymentProviderSettlementRequest
{
    public decimal? ProviderFeeAmount { get; set; }
    public decimal? ProviderNetAmount { get; set; }
    public DateTime? ProviderPayoutDate { get; set; }
    public DateTime? ProviderSettledAt { get; set; }
    public string? ProviderSettlementId { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? ProviderStatus { get; set; }
}

```

### UpsertLegalEntityRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class UpsertLegalEntityRequest
{
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

    public string? TermsVersion { get; set; }

    public string? TermsUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

```

### UpsertPaymentProviderAccountRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Billing;

public class UpsertPaymentProviderAccountRequest
{
    public int LegalEntityId { get; set; }

    public int? LocationId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? MerchantId { get; set; }

    public string? PosId { get; set; }

    public string? AccountKey { get; set; }

    public bool IsActive { get; set; } = true;

    public bool WebhookSecretConfigured { get; set; }
}

```

### PortalReadFilters.cs
```csharp
using System.ComponentModel.DataAnnotations;
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class PortalPageFilter
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [StringLength(100)] public string? Search { get; set; }
    public int? LocationId { get; set; }
    [StringLength(30)] public string? Status { get; set; }
    [RegularExpression("^(name|nameDesc|created|createdDesc)$")]
    public string Sort { get; set; } = "name";
    public void Validate()
    {
        Validator.ValidateObject(this, new ValidationContext(this), true);
    }
}
public class GroupClientOptionDto
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = "";
}

```

### TrainerDashboardSummaryDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerPortal;
public class TrainerDashboardSummaryDto
{
    public int ActiveClientsCount { get; set; }
    public int TodaySessionsCount { get; set; }
    public int UpcomingSessionsCount { get; set; }
    public List<TrainerDashboardSessionDto> UpcomingSessions { get; set; } = new();
}
public class TrainerDashboardSessionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = "";
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public int BookedSeats { get; set; }
    public int? Capacity { get; set; }
}

```

### TrainerPortalClientDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalClientDto
{
    public int ClientId { get; set; }
    public int? UserId { get; set; }
    public string PortalAccessStatus { get; set; } = "NoAccount";

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string EmailContactUrl { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? PhoneContactUrl { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Goal { get; set; }

    public string Status { get; set; } = string.Empty;

    public string BillingStatus { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public DateTime? TrainingStartDate { get; set; }

    public DateTime CreatedAt { get; set; }
}

```

### TrainerPortalDashboardDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalDashboardDto
{
    public TrainerPortalMeDto Me { get; set; } = new();

    public int ActiveClientsCount { get; set; }

    public int TodaySessionsCount { get; set; }

    public int UpcomingSessionsCount { get; set; }

    public List<TrainerPortalSessionDto> TodaySessions { get; set; } = new();

    public List<TrainerPortalSessionDto> UpcomingSessions { get; set; } = new();

    public List<TrainerPortalClientDto> RecentClients { get; set; } = new();
}
```

### TrainerPortalMeDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalMeDto
{
    public int TrainerId { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? TeamJoinedDate { get; set; }

    public string? OutlookCategoryName { get; set; }

    public string? OutlookCategoryColor { get; set; }

    public List<int> LocationIds { get; set; } = new();

    public List<string> LocationNames { get; set; } = new();
}

```

### TrainerPortalSessionDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalSessionDto
{
    public string? EventRules { get; set; }
    public StudioCRM.Application.DTOs.Sessions.BookingRulesDto BookingRules { get; set; } = new();
    public bool IsGroupSession { get; set; }
    public int? Capacity { get; set; }
    public int BookedSeats { get; set; }
    public int? AvailableSeats { get; set; }
    public bool IsFullyBooked { get; set; }
    public int LocationId { get; set; }
    public string? PlannedSessionType { get; set; }
    public string? ActualSessionType { get; set; }
    public bool IsPubliclyBookable { get; set; }
    public List<TrainerSessionParticipantDto> Participants { get; set; } = new();

    public int SessionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public bool CanEdit { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

```

### TrainerSessionParticipantDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerSessionParticipantDto
{
    public int ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
}

// A session-scoped profile intentionally contains no financial or private client notes.
public class TrainerParticipantProfileDto
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public int LocationId { get; set; }
}

```

### TrainerMonthlySettlementDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerMonthlySettlementDto
{
    public int TrainerId { get; set; }
    public int? LocationId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalHours { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalAmount { get; set; }

    public decimal ContractedTotalHours { get; set; }
    public int ContractedTotalSessions { get; set; }
    public decimal ContractedTotalAmount { get; set; }

    public decimal NonContractedTotalHours { get; set; }
    public int NonContractedTotalSessions { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    public List<TrainerSettlementItemDto> Items { get; set; } = new();
    public List<TrainerSettlementItemDto> NonContractedItems { get; set; } = new();
}

```

### TrainerSettlementItemDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerSettlementItemDto
{
    public int SessionId { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public string Title { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;

    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;

    public bool IsCoveredByContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractNumber { get; set; }

    public decimal Hours { get; set; }
    public decimal Rate { get; set; }
    public string RateType { get; set; } = "Hourly";
    public decimal Amount { get; set; }

    public int ParticipantsCount { get; set; }
}

```

### TrainerSettlementSummaryDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerSettlementSummaryDto
{
    public int TrainerId { get; set; }
    public int? LocationId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalHours { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalAmount { get; set; }

    public decimal ContractedTotalHours { get; set; }
    public int ContractedTotalSessions { get; set; }
    public decimal ContractedTotalAmount { get; set; }

    public decimal NonContractedTotalHours { get; set; }
    public int NonContractedTotalSessions { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

}

```

### TrainerWorkHoursDocumentDto.cs
```csharp
namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerWorkHoursDocumentDto
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public byte[] Content { get; set; } = [];
}

```

### CreatePackageDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Packages;

public class CreatePackageDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PLN";
    public int SessionsLimit { get; set; }
    public int SessionsPerWeek { get; set; } = 1;
    public int DurationDays { get; set; }
    public SessionBillingType BillingType { get; set; } = SessionBillingType.OneToOne;
    public int? ParticipantsCount { get; set; }
    public int? LocationId { get; set; }
    public bool IsPubliclyAvailable { get; set; }
    public string? PublicSlug { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
}

```

### PackageDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Packages;

public class PackageDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int SessionsLimit { get; set; }
    public int SessionsPerWeek { get; set; }
    public int DurationDays { get; set; }
    public SessionBillingType BillingType { get; set; }
    public string PackageType => StudioCRM.Application.Common.PackageTypeMapper.FromBillingType(BillingType);
    public int ParticipantsCount { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public bool IsPubliclyAvailable { get; set; }
    public string? PublicSlug { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
}

```

### PackageOptionDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Packages;
public class PackageOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? LocationId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "";
    public string PackageType { get; set; } = "";
    public string BillingType { get; set; } = "";
    public int ParticipantsCount { get; set; }
    public int SessionsLimit { get; set; }
    public bool IsActive { get; set; }
}

```

### UpdatePackageDto.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Packages;

public class UpdatePackageDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PLN";
    public int SessionsLimit { get; set; }
    public int SessionsPerWeek { get; set; } = 1;
    public int DurationDays { get; set; }
    public SessionBillingType BillingType { get; set; } = SessionBillingType.OneToOne;
    public int? ParticipantsCount { get; set; }
    public int? LocationId { get; set; }
    public bool IsPubliclyAvailable { get; set; }
    public string? PublicSlug { get; set; }
    public bool IsActive { get; set; }
}

```

### NotificationCategoryDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Notifications;

public record NotificationCategoryDto(string Key, string Label);

```

### NotificationDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Notifications;

public class NotificationDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Category { get; set; } = "system";

    public string Severity { get; set; } = "Info";

    public string Title { get; set; } = string.Empty;

    public string? Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public string? ActionUrl { get; set; }
}

```

### NotificationReadAllResultDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Notifications;

public class NotificationReadAllResultDto
{
    public int MarkedAsRead { get; set; }
}

```

### NotificationUnreadCountDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Notifications;

public class NotificationUnreadCountDto
{
    public int UnreadCount { get; set; }
    public Dictionary<string, int> UnreadByCategory { get; set; } = new();
}

```

### ClientClosureDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientClosurePreviewDto
{
    public int ClientId { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<int> FutureSessionIds { get; set; } = new();
    public decimal Balance { get; set; }
    public List<ClientClosurePackageDto> Packages { get; set; } = new();
}

public class ClientClosurePackageDto
{
    public int ClientPackageId { get; set; }
    public string Name { get; set; } = "";
    public bool RequiresDecision { get; set; }
    public int RemainingSessions { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "PLN";
    public string? Disposition { get; set; }
    public decimal RefundAmount { get; set; }
    public DateTime? RefundConfirmedAt { get; set; }
}

public class CloseClientRequest
{
    public string Reason { get; set; } = "";
    public List<CloseClientPackageRequest> Packages { get; set; } = new();
}

public class CloseClientPackageRequest
{
    public int ClientPackageId { get; set; }
    public string Disposition { get; set; } = "Retain";
    public decimal RefundAmount { get; set; }
}

public class ConfirmClientRefundRequest
{
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
}

public class ResumeRetainedPackageRequest
{
    public DateTime ValidUntil { get; set; }
    public string Reason { get; set; } = "";
}

```

### ClientDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? LoginEmail { get; set; }
    public string PortalAccessStatus { get; set; } = "NoAccount";
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public int? TrainerId { get; set; }

    public int? ActivePackageId { get; set; }

    public int? ActiveClientPackageId { get; set; }

    public string? ActiveClientPackageName { get; set; }

    public int ActivePackageTotalSessions { get; set; }

    public int ActivePackageUsedSessions { get; set; }

    public int ActivePackageRemainingSessions { get; set; }

    public string ActivePackagePaymentStatus { get; set; } = string.Empty;

    public decimal CurrentBalance { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? CalendarEmail { get; set; }

    public string EmailContactUrl { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? PhoneContactUrl { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Goal { get; set; }

    public string? Notes { get; set; }

    public string BillingStatus { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string PortalAccessMode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime? NextSessionAt { get; set; }

    public DateTime? TrainingStartDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public string? TrainerFullName { get; set; }
    public int LocationId { get; set; }
    public string? LocationName { get; set; }
}

```

### ClientEmailChangeDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientEmailChangeDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string CurrentEmail { get; set; } = "";
    public string RequestedEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? VerificationExpiresAt { get; set; }
    public string? ReviewReason { get; set; }
}

public class ReviewClientEmailChangeRequest
{
    public bool Approve { get; set; }
    public string? Reason { get; set; }
}

public class VerifyClientEmailChangeRequest
{
    public int RequestId { get; set; }
    public string Token { get; set; } = "";
}

```

### ClientFilterDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientFilterDto
{
    public int? TrainerId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int? LocationId { get; set; }
}
```

### ClientHistoryDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientHistoryFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string Scope { get; set; } = "All";
}

public class ClientAuditDto
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public int? ActorUserId { get; set; }
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientSessionHistoryDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = "";
    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = "";
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string AttendanceStatus { get; set; } = "";
    public bool IsCountedFromPackage { get; set; }
    public int? ClientPackageId { get; set; }
}

```

### ClientLegalConsentDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientLegalConsentDto
{
    public int Id { get; set; }

    public int LegalEntityId { get; set; }

    public string LegalEntityName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentVersion { get; set; } = string.Empty;

    public string DocumentUrl { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTime AcceptedAt { get; set; }

    public bool IsCurrent { get; set; }
}

```

### ClientLifecycleDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientArchiveCheckDto
{
    public int ClientId { get; set; }
    public bool CanArchive => Blockers.Count == 0;
    public List<string> Blockers { get; set; } = new();
}

public class BulkArchiveClientsRequest
{
    public List<int> ClientIds { get; set; } = new();
}

public class ClientArchiveResultDto
{
    public int ClientId { get; set; }
    public bool Archived { get; set; }
    public string? Error { get; set; }
}

public class SetClientPortalAccessRequest
{
    public bool Blocked { get; set; }
}

```

### ClientManagementDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientLocationDto
{
    public int LocationId { get; set; }
    public string Name { get; set; } = "";
    public bool IsHomeLocation { get; set; }
    public bool GroupAccessEnabled { get; set; }
}
public class SetClientLocationAccessRequest
{
    public bool GroupAccessEnabled { get; set; }
    public string Reason { get; set; } = "";
}
public class ClientDuplicateFilter
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public int? ExcludeClientId { get; set; }
}
public class ClientDuplicateDto
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = "";
    public bool IsArchived { get; set; }
    public List<string> Matches { get; set; } = new();
}
public class ClientRefundDto
{
    public int ClientId { get; set; }
    public int ClientPackageId { get; set; }
    public string PackageName { get; set; } = "";
    public string Disposition { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public DateTime? ConfirmedAt { get; set; }
    public string? Reference { get; set; }
}

```

### ClientWorkspaceDto.cs
```csharp
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;

namespace StudioCRM.Application.DTOs.Clients;

public class ClientWorkspaceDto
{
    public ClientDto Profile { get; set; } = new();
    public ClientWorkspaceTrainerDto? Trainer { get; set; }
    public SubscriptionDto? Subscription { get; set; }
    public ClientBillingSummaryDto? Billing { get; set; }
    public TrainingPlanDto? TrainingPlan { get; set; }
    public List<ClientWorkspaceSessionDto> UpcomingSessions { get; set; } = new();
    public List<ClientWorkspaceCountedSessionDto> CountedSessions { get; set; } = new();
    public ClientWorkspaceQuickActionsDto QuickActions { get; set; } = new();
}

public class ClientWorkspaceTrainerDto
{
    public int TrainerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmailContactUrl { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PhoneContactUrl { get; set; }
    public string? AvatarUrl { get; set; }
}

public class ClientWorkspaceSessionDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string TrainerFullName { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public bool CountsAgainstPackage { get; set; }
    public bool IsCountedFromPackage { get; set; }
}

public class ClientWorkspaceCountedSessionDto
{
    public int SessionId { get; set; }
    public DateTime Date { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SessionsCharged { get; set; }
    public string PlannedBillingType { get; set; } = string.Empty;
    public string ActualBillingType { get; set; } = string.Empty;
    public decimal ExpectedUnitPrice { get; set; }
    public decimal ActualUnitPrice { get; set; }
    public decimal BalanceDifference { get; set; }
}

public class ClientWorkspaceQuickActionsDto
{
    public bool CanChangePackage { get; set; } = true;
    public bool CanChangeTrainer { get; set; } = true;
    public bool CanDeactivate { get; set; } = true;
    public bool CanAddPayment { get; set; } = true;
    public string? GoogleDriveFolderUrl { get; set; }
    public string? TrainingPlanUrl { get; set; }
}

```

### CreateClientDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class CreateClientDto
{
    public int? TrainerId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Goal { get; set; }

    public string? Notes { get; set; }

    public string? BillingStatus { get; set; }

    public string? Status { get; set; }

    public DateTime? NextSessionAt { get; set; }

    public DateTime? TrainingStartDate { get; set; }

    public int? CreatedBy { get; set; }
    public int LocationId { get; set; }
}

```

### SetClientTrainerRequest.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class SetClientTrainerRequest
{
    public int? TrainerId { get; set; }
}

```

### UpdateClientDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class UpdateClientDto
{
    public int? TrainerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Goal { get; set; }
    public string? Notes { get; set; }
    public string BillingStatus { get; set; } = "Pending";
    public string Status { get; set; } = "New";
    public DateTime? NextSessionAt { get; set; }
    public DateTime? TrainingStartDate { get; set; }
    public int LocationId { get; set; }
}

```

### CreateClientPackageRequest.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.ClientPackages;
public class CreateClientPackageRequest
{
    public int ClientId { get; set; }
    public int PackageId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? TotalSessions { get; set; }
    public decimal? TotalPrice { get; set; }

    public SessionBillingType? ExpectedBillingType { get; set; }

    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }

    public DateTime? PaymentDueDate { get; set; }
}

```

### PackageManagementDtos.cs
```csharp
namespace StudioCRM.Application.DTOs.ClientPackages;

public class PackageChangeRequest
{
    public string ExpectedVersion { get; set; } = "";
    public string Reason { get; set; } = "";
    public int? TotalSessions { get; set; }
    public decimal? TotalPrice { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaymentDueDate { get; set; }
}

public class ClosePackageRequest
{
    public string ExpectedVersion { get; set; } = "";
    public string Reason { get; set; } = "";
    public string DebtDisposition { get; set; } = "KeepDue";
    public string FundsDisposition { get; set; } = "KeepFunds";
    public decimal SettlementAmount { get; set; }
    public CreateClientPackageRequest? Replacement { get; set; }
}

public class ImportClientPackageRequest
{
    public Guid RequestId { get; set; }
    public CreateClientPackageRequest Package { get; set; } = new();
    public int UsedSessions { get; set; }
    public decimal AmountPaid { get; set; }
    public string Reason { get; set; } = "";
}

public class PackageManagementPreviewDto
{
    public int ClientPackageId { get; set; }
    public string Version { get; set; } = "";
    public decimal AmountDue { get; set; }
    public int RemainingSessions { get; set; }
    public bool CanDelete { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCorrect { get; set; }
    public bool CanClose { get; set; }
    public string? DeleteBlockReason { get; set; }
    public List<string> Blockers { get; set; } = new();
}

```

### BookingRulesDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class BookingRulesDto
{
    public int RegistrationClosesBeforeMinutes { get; set; }
    public int CancellationClosesBeforeMinutes { get; set; }
    public DateTime RegistrationClosesAtUtc { get; set; }
    public DateTime CancellationClosesAtUtc { get; set; }
    public string LateCancellationPolicy { get; set; } = "ContactStudio";
}

```

### CountSessionFromPackageRequest.cs
```csharp
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Sessions;

public class CountSessionFromPackageRequest
{
    public int SessionParticipantId { get; set; }

    public int ClientPackageId { get; set; }

    public SessionBillingType ActualBillingType { get; set; }

    public decimal? ActualUnitPrice { get; set; }
}

```

### CreateSessionDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class CreateSessionDto
{
    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? EventRules { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int RegistrationClosesBeforeMinutes { get; set; } = 30;
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int CancellationClosesBeforeMinutes { get; set; } = 720;

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int TrainerId { get; set; }

    public int LocationId { get; set; }

    public string Status { get; set; } = "Planned";

    public bool IsPubliclyBookable { get; set; }

    public string? PublicSlug { get; set; }

    public int? PublicCapacity { get; set; }

    public string? PlannedSessionType { get; set; }

    public List<string> OutlookCategories { get; set; } = new();

    public List<CreateSessionParticipantDto> Participants { get; set; } = new();
}

```

### CreateSessionParticipantDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class CreateSessionParticipantDto
{
    public int ClientId { get; set; }

    public bool CountsAgainstPackage { get; set; } = true;

    public int SessionsCharged { get; set; } = 1;

    public string? Note { get; set; }
}

```

### CreateSessionSeriesDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class CreateSessionSeriesDto
{
    public CreateSessionDto Session { get; set; } = new();

    public SessionRecurrenceDto Recurrence { get; set; } = new();
}

public class SessionRecurrenceDto
{
    public string Frequency { get; set; } = "Weekly";

    public int Interval { get; set; } = 1;

    public List<string> DaysOfWeek { get; set; } = new();

    public DateTime? EndDate { get; set; }

    public int? OccurrencesCount { get; set; }
}

public class SessionSeriesDto
{
    public string RecurringGroupId { get; set; } = string.Empty;

    public string Frequency { get; set; } = string.Empty;

    public int Interval { get; set; }

    public int OccurrencesCount { get; set; }

    public bool OutlookSeriesSynced { get; set; }

    public string? OutlookSyncWarning { get; set; }

    public List<SessionDto> Sessions { get; set; } = new();
}

public class SessionSeriesOutlookSyncDto
{
    public string RecurringGroupId { get; set; } = string.Empty;

    public bool OutlookSeriesSynced { get; set; }

    public string? OutlookSyncWarning { get; set; }
}

public class DeleteSessionSeriesResultDto
{
    public string RecurringGroupId { get; set; } = string.Empty;

    public int DeletedSessionsCount { get; set; }

    public bool OutlookSeriesDeleted { get; set; }
}

```

### SessionCorrectionDto.cs
```csharp
using System.Text.Json;

namespace StudioCRM.Application.DTOs.Sessions;

public class SessionCorrectionDto
{
    public int Id { get; set; }

    public int? SessionId { get; set; }

    public int OriginalSessionId { get; set; }

    public string ChangeType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public JsonElement BeforeState { get; set; }

    public JsonElement AfterState { get; set; }

    public int? ChangedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}

```

### SessionDto.cs
```csharp
using StudioCRM.Application.DTOs.Calendar;

namespace StudioCRM.Application.DTOs.Sessions;

public class SessionDto
{
    public string? EventRules { get; set; }
    public StudioCRM.Application.DTOs.Sessions.BookingRulesDto BookingRules { get; set; } = new();
    public bool IsGroupSession { get; set; }
    public int? Capacity { get; set; }
    public int BookedSeats { get; set; }
    public int? AvailableSeats { get; set; }
    public bool IsFullyBooked { get; set; }
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public bool CanEdit { get; set; }

    public int LocationId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsPubliclyBookable { get; set; }

    public string? PublicSlug { get; set; }

    public int? PublicCapacity { get; set; }

    public int? PublicAvailableSpots { get; set; }

    public string? PlannedSessionType { get; set; }

    public string? ActualSessionType { get; set; }

    public int? ActualParticipantsCount { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int ParticipantsCount { get; set; }

    public string ClientsDisplayName { get; set; } = string.Empty;

    public List<SessionParticipantListDto> Participants { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public int LocationParticipantsCount { get; set; }

    public int LocationLimit { get; set; } = 8;

    public bool IsLocationLimitExceeded { get; set; }

    public List<string> OutlookCategories { get; set; } = new();

    public List<OutlookCategoryDto> OutlookCategoryColors { get; set; } = new();

    public string? PrimaryOutlookCategory { get; set; }

    public string? PrimaryOutlookCategoryColor { get; set; }

    public bool IsRecurring { get; set; }

    public string? RecurringGroupId { get; set; }

    public int? RecurrenceInstanceNumber { get; set; }
}

```

### SessionFilterDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class SessionFilterDto
{
    public int? TrainerId { get; set; }

    public int? ClientId { get; set; }

    public int? LocationId { get; set; }

    public string? Status { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
```

### SessionParticipantListDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class SessionParticipantListDto
{
    public string? TrainerProfileUrl { get; set; }

    public int Id { get; set; }

    public int ClientId { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public int? PackageId { get; set; }

    public string? PackageName { get; set; }

    public int? ClientPackageId { get; set; }

    public string AttendanceStatus { get; set; } = string.Empty;

    public bool CountsAgainstPackage { get; set; }

    public bool IsCountedFromPackage { get; set; }

    public int SessionsCharged { get; set; }

    public string PlannedBillingType { get; set; } = string.Empty;

    public string ActualBillingType { get; set; } = string.Empty;

    public decimal ExpectedUnitPrice { get; set; }

    public decimal ActualUnitPrice { get; set; }

    public decimal BalanceDifference { get; set; }

    public string? Note { get; set; }
}

```

### SessionWorkspaceDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class SessionWorkspaceDto
{
    public SessionDto Session { get; set; } = new();
    public SessionOutlookSyncDto OutlookSync { get; set; } = new();
    public SessionWorkspaceQuickActionsDto QuickActions { get; set; } = new();
}

public class SessionOutlookSyncDto
{
    public bool IsSynced { get; set; }
    public string? Provider { get; set; }
    public string? ExternalEventId { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class SessionWorkspaceQuickActionsDto
{
    public bool CanEditParticipants { get; set; }
    public bool CanComplete { get; set; }
    public bool CanSyncOutlook { get; set; } = true;
}

```

### UpdateSessionDto.cs
```csharp
namespace StudioCRM.Application.DTOs.Sessions;

public class UpdateSessionDto
{
    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? EventRules { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int? RegistrationClosesBeforeMinutes { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int? CancellationClosesBeforeMinutes { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public int LocationId { get; set; }

    public string Status { get; set; } = "Planned";

    public bool IsPubliclyBookable { get; set; }

    public string? PublicSlug { get; set; }

    public int? PublicCapacity { get; set; }

    public string? PlannedSessionType { get; set; }

    public List<string> OutlookCategories { get; set; } = new();

    public List<CreateSessionParticipantDto>? Participants { get; set; }

    public string? CorrectionReason { get; set; }
}

```

### BalanceTransactionType.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum BalanceTransactionType
{
    PackageAdjustment = 1,
    ManualAdjustment = 2,
    PaymentCredit = 3,
    UsedInNextPackage = 4,
    PaymentOverpayment = 5,
    PaymentReversal = 6
}

```

### BillingStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum BillingStatus
{
    Paid = 1,
    Pending = 2,
    Overdue = 3
}

```

### ClientPackageActivationMode.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ClientPackageActivationMode
{
    Immediately = 1,
    AfterCurrentPackage = 2
}

```

### ClientPaymentSource.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ClientPaymentSource
{
    ClientRequest = 1,
    StaffEntry = 2,
    PaymentGateway = 3
}

```

### ClientPaymentStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ClientPaymentStatus
{
    PendingConfirmation = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Reversed = 5
}

```

### ClientStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ClientStatus
{
    New = 1,
    Active = 2,
    Inactive = 3,
    Archived = 4
}

```

### ExpenseCategory.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ExpenseCategory
{
    Other = 0,
    Rent = 1,
    Utilities = 2,
    Marketing = 3,
    Equipment = 4,
    Accounting = 5,
    Software = 6,
    Cleaning = 7,
    Repairs = 8,
    Supplies = 9,
    TrainerCosts = 10,
    TaxesAndFees = 11
}

```

### ExpensePaymentStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ExpensePaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    Overdue = 2,
    Cancelled = 3
}

```

### ExpenseRecurrenceEditScope.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ExpenseRecurrenceEditScope
{
    ThisOnly = 0,
    ThisAndFuture = 1,
    All = 2
}

```

### FiscalReceiptMode.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum FiscalReceiptMode
{
    Manual = 1,
    Automatic = 2,
    NotRequired = 3
}

```

### PaymentMethod.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum PaymentMethod
{
    Blik = 1,
    BankTransfer = 2,
    Cash = 3,
    PaymentGateway = 4
}

```

### PaymentStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    Overdue = 2,
    PartiallyPaid = 3,
    PendingConfirmation = 4
}

```

### ReceiptStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum ReceiptStatus
{
    None = 0,
    Issued = 1,
    Cancelled = 2,
    Pending = 3,
    ManualRequired = 4,
    Failed = 5
}

```

### SessionBillingType.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum SessionBillingType
{
    OneToOne = 1,
    TwoToOne = 2,
    ThreeToOne = 3,
    FourToOne = 4,
    Group = 5
}

```

### SessionStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum SessionStatus
{
    Planned = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4
}
```

### TrainerContractType.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum TrainerContractType
{
    B2B = 0,
    Zlecenie = 1
}

```

### TrainerStatus.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum TrainerStatus
{
    Active = 1,
    Inactive = 2,
    OnLeave = 3
}

```

### UserRole.cs
```csharp
namespace StudioCRM.Domain.Enums;

public enum UserRole
{
    Owner = 1,
    Trainer = 2,
    Client = 3
}

```
