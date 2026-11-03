using System.ComponentModel.DataAnnotations;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Tests.UnitTests;

public class PaymentMonthFilterTests
{
    [Fact]
    public void MonthIsTheStudioCalendarMonthInUtc()
    {
        // November: CET (UTC+1); October starts in CEST (UTC+2) and ends in CET.
        Assert.Equal((new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 30, 23, 0, 0, DateTimeKind.Utc)),
            StudioMonth.Range("2026-11"));
        Assert.Equal((new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc)),
            StudioMonth.Range("2026-10"));
        Assert.Equal(new DateTime(2026, 12, 31, 23, 0, 0, DateTimeKind.Utc), StudioMonth.Range("2026-12").EndUtc);
    }

    [Theory]
    [InlineData("2026-13")] [InlineData("2026-1")] [InlineData("11-2026")] [InlineData("2026-11-03")]
    public void MonthRejectsOtherFormats(string month) =>
        Assert.Throws<ValidationException>(() => new ClientPaymentFilterDto { Month = month }.Validate());

    [Fact]
    public void MonthCannotBeCombinedWithFromOrTo()
    {
        new ClientPaymentFilterDto { Month = "2026-11", TrainerId = 3 }.Validate();
        Assert.Throws<ValidationException>(() => new ClientPaymentFilterDto { Month = "2026-11", From = new DateTime(2026, 11, 1) }.Validate());
        Assert.Throws<ValidationException>(() => new ClientPaymentFilterDto { Month = "2026-11", To = new DateTime(2026, 11, 3) }.Validate());
    }
}
