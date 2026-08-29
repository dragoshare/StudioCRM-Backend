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
