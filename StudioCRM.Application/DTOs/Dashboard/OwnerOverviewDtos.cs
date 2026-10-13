namespace StudioCRM.Application.DTOs.Dashboard;
public record DashboardMoneyMetric(decimal Current, decimal Previous, decimal ChangeAmount, decimal? ChangePercent);
public record DashboardCountMetric(int Current, int Previous, int Change, decimal? ChangePercent);
public record OwnerOverviewSummaryDto(string Period, string PreviousPeriod, string TimeZone, DateTime AsOf, bool IsPartial, string Currency,
    DashboardMoneyMetric Revenue, DashboardCountMetric ActiveClients, int NewClients,
    DashboardCountMetric CompletedSessions, int ClientsWithoutJoinedAt);
public record MonthlyRevenueDto(string Period, decimal Received, decimal Refunded, decimal Revenue, bool IsPartial);
public record OwnerRevenueDto(string Period, int Months, string Currency, string TimeZone, DateTime AsOf,
    List<MonthlyRevenueDto> Items, decimal Total, decimal Average, decimal PreviousYearTotal, decimal? YearOverYearChangePercent);
public record RecentOwnerClientDto(int Id, string FirstName, string LastName, string FullName, DateTime? JoinedAt, DateTime CreatedAt);
public record OwnerAttentionDto(DateTime AsOf, int InactivityDays, int ClientsWithoutTraining30Days,
    int ClientsWithOneRemainingSession, int ClientsWithoutAttendanceBaseline);
