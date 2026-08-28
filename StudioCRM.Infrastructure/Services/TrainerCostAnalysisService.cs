using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainerCostAnalysisService : ITrainerCostAnalysisService
{
    private const string WindowsStudioTimeZone = "Central European Standard Time";

    private readonly StudioCRMDbContext _context;

    public TrainerCostAnalysisService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<TrainerSessionProfitabilityDto>> GetSessionProfitabilityAsync(
        TrainerCostAnalysisFilterDto filter)
    {
        var rows = await BuildSessionProfitabilityRowsAsync(filter);
        rows = ApplyInMemoryFilters(rows, filter);

        var page = NormalizePage(filter.Page);
        var pageSize = NormalizePageSize(filter.PageSize);
        var totalCount = rows.Count;

        return new PagedResultDto<TrainerSessionProfitabilityDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = ResolveTotalPages(totalCount, pageSize),
            Items = rows
                .OrderByDescending(x => x.StartAt)
                .ThenByDescending(x => x.SessionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };
    }

    public async Task<TrainerCostStatisticsDto> GetStatisticsAsync(
        TrainerCostAnalysisFilterDto filter)
    {
        var rows = ApplyInMemoryFilters(
            await BuildSessionProfitabilityRowsAsync(filter),
            filter);
        var participantRows = rows
            .SelectMany(session => session.Participants.Select(participant =>
                new ParticipantProfitabilityRow(session, participant)))
            .ToList();

        return new TrainerCostStatisticsDto
        {
            From = NormalizeNullableDateTime(filter.From),
            To = NormalizeNullableDateTime(filter.To),
            SessionsCount = rows.Count,
            CoveredSessionsCount = rows.Count(x => x.IsCoveredByContract),
            UncoveredSessionsCount = rows.Count(x => !x.IsCoveredByContract),
            ParticipantsCount = rows.Sum(x => x.ParticipantsCount),
            BillableHours = rows.Sum(x => x.BillableHours),
            RevenueAmount = rows.Sum(x => x.RevenueAmount),
            TrainerCostAmount = rows.Sum(x => x.TrainerCostAmount),
            PotentialTrainerCostAmount = rows.Sum(x => x.PotentialTrainerCostAmount),
            UncoveredPotentialTrainerCostAmount = rows
                .Where(x => !x.IsCoveredByContract)
                .Sum(x => x.PotentialTrainerCostAmount),
            ProfitAmount = rows.Sum(x => x.ProfitAmount),
            ProfitMarginPercent = ResolveMarginPercent(
                rows.Sum(x => x.ProfitAmount),
                rows.Sum(x => x.RevenueAmount)),
            ByTrainer = rows
                .GroupBy(x => new { x.TrainerId, x.TrainerName })
                .Select(x => BuildSessionBreakdown(x.Key.TrainerId.ToString(), x.Key.TrainerName, x))
                .OrderByDescending(x => x.ProfitAmount)
                .ToList(),
            ByLocation = rows
                .GroupBy(x => new { x.LocationId, x.LocationName })
                .Select(x => BuildSessionBreakdown(x.Key.LocationId.ToString(), x.Key.LocationName, x))
                .OrderByDescending(x => x.ProfitAmount)
                .ToList(),
            ByLegalEntity = rows
                .GroupBy(x => new
                {
                    Key = x.LegalEntityId?.ToString() ?? "none",
                    Label = x.LegalEntityName ?? "Bez firmy"
                })
                .Select(x => BuildSessionBreakdown(x.Key.Key, x.Key.Label, x))
                .OrderByDescending(x => x.ProfitAmount)
                .ToList(),
            ByClient = participantRows
                .GroupBy(x => new { x.Participant.ClientId, x.Participant.ClientName })
                .Select(x => BuildParticipantBreakdown(x.Key.ClientId.ToString(), x.Key.ClientName, x))
                .OrderByDescending(x => x.ProfitAmount)
                .ToList(),
            ByPackage = participantRows
                .GroupBy(x => new
                {
                    Key = x.Participant.ClientPackageId?.ToString() ?? "none",
                    Label = x.Participant.PackageName ?? "Bez pakietu"
                })
                .Select(x => BuildParticipantBreakdown(x.Key.Key, x.Key.Label, x))
                .OrderByDescending(x => x.ProfitAmount)
                .ToList(),
            ByMonth = rows
                .GroupBy(x => x.StartAt.ToString("yyyy-MM"))
                .Select(x => BuildSessionBreakdown(x.Key, x.Key, x))
                .OrderBy(x => x.Key)
                .ToList()
        };
    }

    private async Task<List<TrainerSessionProfitabilityDto>> BuildSessionProfitabilityRowsAsync(
        TrainerCostAnalysisFilterDto filter)
    {
        var query = _context.Sessions
            .Include(x => x.Trainer)
                .ThenInclude(x => x.User)
            .Include(x => x.Location)
                .ThenInclude(x => x.LegalEntity)
            .Include(x => x.Participants)
                .ThenInclude(x => x.Client)
            .Include(x => x.Participants)
                .ThenInclude(x => x.ClientPackage)
            .Where(x => x.Status == "Completed")
            .AsQueryable();

        if (filter.TrainerId.HasValue)
            query = query.Where(x => x.TrainerId == filter.TrainerId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(x => x.LocationId == filter.LocationId.Value);

        if (filter.LegalEntityId.HasValue)
            query = query.Where(x => x.Location.LegalEntityId == filter.LegalEntityId.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(x => x.Participants.Any(p => p.ClientId == filter.ClientId.Value));

        if (filter.ClientPackageId.HasValue)
            query = query.Where(x => x.Participants.Any(p => p.ClientPackageId == filter.ClientPackageId.Value));

        if (filter.From.HasValue)
        {
            var from = NormalizeNullableDateTime(filter.From)!.Value;
            query = query.Where(x => x.StartAt >= from);
        }

        if (filter.To.HasValue)
        {
            var to = NormalizeNullableDateTime(filter.To)!.Value;
            query = query.Where(x => x.StartAt <= to);
        }

        var sessions = await query
            .OrderByDescending(x => x.StartAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        if (sessions.Count == 0)
            return new List<TrainerSessionProfitabilityDto>();

        var trainerIds = sessions
            .Select(x => x.TrainerId)
            .Distinct()
            .ToList();
        var periodFrom = sessions.Min(x => x.StartAt);
        var periodTo = sessions.Max(x => x.EndAt);
        var rates = await _context.TrainerRates
            .Where(x => trainerIds.Contains(x.TrainerId))
            .ToListAsync();
        var contracts = await _context.TrainerContracts
            .Include(x => x.ContractLocations)
            .Where(x =>
                trainerIds.Contains(x.TrainerId) &&
                x.IsActive &&
                x.ValidFrom <= periodTo &&
                (x.ValidTo == null || x.ValidTo >= periodFrom))
            .OrderByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return sessions
            .Select(session => BuildSessionRow(
                session,
                rates.Where(x => x.TrainerId == session.TrainerId).ToList(),
                contracts))
            .ToList();
    }

    private static List<TrainerSessionProfitabilityDto> ApplyInMemoryFilters(
        List<TrainerSessionProfitabilityDto> rows,
        TrainerCostAnalysisFilterDto filter)
    {
        if (filter.IsCoveredByContract.HasValue)
        {
            rows = rows
                .Where(x => x.IsCoveredByContract == filter.IsCoveredByContract.Value)
                .ToList();
        }

        return rows;
    }

    private static TrainerSessionProfitabilityDto BuildSessionRow(
        Session session,
        List<TrainerRate> trainerRates,
        List<TrainerContract> contracts)
    {
        var contract = ResolveContract(session, contracts);
        var sessionType = ResolveSettlementSessionType(session);
        var billableHours = ResolveBillableHours(sessionType, session.StartAt, session.EndAt);
        var hourlyRate = ResolveHourlyRate(trainerRates, session.StartAt);
        var potentialTrainerCost = decimal.Round(billableHours * hourlyRate, 2);
        var trainerCost = contract is null ? 0 : potentialTrainerCost;
        var participants = BuildParticipantRows(session);
        AllocateTrainerCost(participants, trainerCost, potentialTrainerCost);
        var revenue = participants.Sum(x => x.RevenueAmount);
        var profit = revenue - trainerCost;

        return new TrainerSessionProfitabilityDto
        {
            SessionId = session.Id,
            Title = session.Title,
            StartAt = ToStudioDisplayDateTime(session.StartAt),
            EndAt = ToStudioDisplayDateTime(session.EndAt),
            TrainerId = session.TrainerId,
            TrainerName = $"{session.Trainer.User.FirstName} {session.Trainer.User.LastName}".Trim(),
            LocationId = session.LocationId,
            LocationName = session.Location.Name,
            LegalEntityId = session.Location.LegalEntityId,
            LegalEntityName = session.Location.LegalEntity?.Name,
            SessionType = sessionType,
            ParticipantsCount = session.ActualParticipantsCount ?? CountPresentParticipants(session.Participants),
            BillableHours = billableHours,
            HourlyRate = hourlyRate,
            IsCoveredByContract = contract is not null,
            ContractId = contract?.Id,
            ContractNumber = contract?.ContractNumber,
            RevenueAmount = revenue,
            TrainerCostAmount = trainerCost,
            PotentialTrainerCostAmount = potentialTrainerCost,
            ProfitAmount = profit,
            ProfitMarginPercent = ResolveMarginPercent(profit, revenue),
            Participants = participants
        };
    }

    private static List<TrainerSessionParticipantProfitabilityDto> BuildParticipantRows(Session session)
    {
        return session.Participants
            .OrderBy(x => x.Client.LastName)
            .ThenBy(x => x.Client.FirstName)
            .Select(participant =>
            {
                var unitPrice = participant.ActualUnitPrice ?? participant.ExpectedUnitPrice ?? 0;
                var revenue = IsRevenueParticipant(participant)
                    ? decimal.Round(unitPrice * Math.Max(1, participant.SessionsCharged), 2)
                    : 0;

                return new TrainerSessionParticipantProfitabilityDto
                {
                    ClientId = participant.ClientId,
                    ClientName = $"{participant.Client.FirstName} {participant.Client.LastName}".Trim(),
                    ClientPackageId = participant.ClientPackageId,
                    PackageName = participant.ClientPackage?.Name,
                    AttendanceStatus = participant.AttendanceStatus,
                    SessionsCharged = participant.SessionsCharged,
                    UnitPrice = unitPrice,
                    RevenueAmount = revenue,
                    AllocatedTrainerCostAmount = 0,
                    ProfitAmount = revenue,
                    ProfitMarginPercent = ResolveMarginPercent(revenue, revenue)
                };
            })
            .ToList();
    }

    private static void AllocateTrainerCost(
        List<TrainerSessionParticipantProfitabilityDto> participants,
        decimal trainerCost,
        decimal potentialTrainerCost)
    {
        if ((trainerCost <= 0 && potentialTrainerCost <= 0) || participants.Count == 0)
            return;

        var revenueParticipants = participants
            .Where(x => x.RevenueAmount > 0)
            .ToList();
        var allocationParticipants = revenueParticipants.Count > 0
            ? revenueParticipants
            : participants
                .Where(x => x.AttendanceStatus == "Present")
                .ToList();

        if (allocationParticipants.Count == 0)
            allocationParticipants = participants;

        var revenueTotal = allocationParticipants.Sum(x => x.RevenueAmount);
        decimal allocatedTrainerCost = 0;
        decimal allocatedPotentialTrainerCost = 0;

        for (var i = 0; i < allocationParticipants.Count; i++)
        {
            var participant = allocationParticipants[i];
            var cost = ResolveAllocatedCost(
                trainerCost,
                allocatedTrainerCost,
                revenueTotal,
                participant.RevenueAmount,
                allocationParticipants.Count,
                i);
            var potentialCost = ResolveAllocatedCost(
                potentialTrainerCost,
                allocatedPotentialTrainerCost,
                revenueTotal,
                participant.RevenueAmount,
                allocationParticipants.Count,
                i);

            participant.AllocatedTrainerCostAmount = cost;
            participant.PotentialAllocatedTrainerCostAmount = potentialCost;
            participant.ProfitAmount = participant.RevenueAmount - cost;
            participant.ProfitMarginPercent = ResolveMarginPercent(
                participant.ProfitAmount,
                participant.RevenueAmount);
            allocatedTrainerCost += cost;
            allocatedPotentialTrainerCost += potentialCost;
        }
    }

    private static decimal ResolveAllocatedCost(
        decimal totalCost,
        decimal alreadyAllocated,
        decimal revenueTotal,
        decimal participantRevenue,
        int participantsCount,
        int participantIndex)
    {
        if (totalCost <= 0)
            return 0;

        return participantIndex == participantsCount - 1
            ? totalCost - alreadyAllocated
            : revenueTotal > 0
                ? decimal.Round(totalCost * participantRevenue / revenueTotal, 2)
                : decimal.Round(totalCost / participantsCount, 2);
    }

    private static TrainerContract? ResolveContract(
        Session session,
        List<TrainerContract> contracts)
    {
        return contracts
            .Where(x =>
                x.TrainerId == session.TrainerId &&
                x.ValidFrom <= session.StartAt &&
                (x.ValidTo == null || x.ValidTo >= session.StartAt) &&
                x.ContractLocations.Any(cl => cl.LocationId == session.LocationId))
            .OrderByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
    }

    private static TrainerCostBreakdownDto BuildSessionBreakdown(
        string key,
        string label,
        IEnumerable<TrainerSessionProfitabilityDto> rows)
    {
        var items = rows.ToList();
        var profit = items.Sum(x => x.ProfitAmount);
        var revenue = items.Sum(x => x.RevenueAmount);

        return new TrainerCostBreakdownDto
        {
            Key = key,
            Label = label,
            SessionsCount = items.Count,
            ParticipantsCount = items.Sum(x => x.ParticipantsCount),
            BillableHours = items.Sum(x => x.BillableHours),
            RevenueAmount = revenue,
            TrainerCostAmount = items.Sum(x => x.TrainerCostAmount),
            PotentialTrainerCostAmount = items.Sum(x => x.PotentialTrainerCostAmount),
            ProfitAmount = profit,
            ProfitMarginPercent = ResolveMarginPercent(profit, revenue)
        };
    }

    private static TrainerCostBreakdownDto BuildParticipantBreakdown(
        string key,
        string label,
        IEnumerable<ParticipantProfitabilityRow> rows)
    {
        var items = rows.ToList();
        var profit = items.Sum(x => x.Participant.ProfitAmount);
        var revenue = items.Sum(x => x.Participant.RevenueAmount);

        return new TrainerCostBreakdownDto
        {
            Key = key,
            Label = label,
            SessionsCount = items.Select(x => x.Session.SessionId).Distinct().Count(),
            ParticipantsCount = items.Count,
            BillableHours = 0,
            RevenueAmount = revenue,
            TrainerCostAmount = items.Sum(x => x.Participant.AllocatedTrainerCostAmount),
            PotentialTrainerCostAmount = items.Sum(x => x.Participant.PotentialAllocatedTrainerCostAmount),
            ProfitAmount = profit,
            ProfitMarginPercent = ResolveMarginPercent(profit, revenue)
        };
    }

    private static bool IsRevenueParticipant(SessionParticipant participant)
    {
        return participant.AttendanceStatus == "Present" &&
            participant.SessionsCharged > 0 &&
            (participant.ActualUnitPrice.HasValue || participant.ExpectedUnitPrice.HasValue);
    }

    private static int CountPresentParticipants(IEnumerable<SessionParticipant> participants)
    {
        return participants.Count(x => x.AttendanceStatus == "Present");
    }

    private static decimal ResolveBillableHours(string sessionType, DateTime startAt, DateTime endAt)
    {
        var fixedHours = sessionType switch
        {
            "TwoToOne" => 1.6m,
            "ThreeToOne" => 2.2m,
            "FourToOne" => 2.66m,
            _ => (decimal?)null
        };

        if (fixedHours.HasValue)
            return fixedHours.Value;

        var hours = (decimal)(endAt - startAt).TotalHours;

        if (hours <= 0)
            return 0;

        return Math.Round(hours, 2);
    }

    private static string ResolveSettlementSessionType(Session session)
    {
        if (!string.IsNullOrWhiteSpace(session.ActualSessionType))
            return session.ActualSessionType;

        if (!string.IsNullOrWhiteSpace(session.PlannedSessionType))
            return session.PlannedSessionType;

        var count = session.ActualParticipantsCount ?? session.Participants.Count;

        return count switch
        {
            1 => "OneToOne",
            2 => "TwoToOne",
            3 => "ThreeToOne",
            4 => "FourToOne",
            _ => "FourToOne"
        };
    }

    private static decimal ResolveHourlyRate(
        List<TrainerRate> rates,
        DateTime sessionDate)
    {
        var rate = rates
            .Where(r =>
                r.SessionType == "Hourly" &&
                r.ValidFrom <= sessionDate &&
                (r.ValidTo == null || r.ValidTo > sessionDate))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefault();

        if (rate?.Rate > 0)
            return rate.Rate;

        rate = rates
            .Where(r => r.SessionType == "Hourly" && r.IsActive && r.Rate > 0)
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefault();

        return rate?.Rate ?? 0;
    }

    private static decimal? ResolveMarginPercent(decimal profit, decimal revenue)
    {
        if (revenue <= 0)
            return null;

        return decimal.Round(profit / revenue * 100, 2);
    }

    private static DateTime? NormalizeNullableDateTime(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static DateTime ToStudioDisplayDateTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(utc, GetStudioTimeZone()),
            DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo GetStudioTimeZone()
    {
        foreach (var id in new[] { "Europe/Warsaw", WindowsStudioTimeZone })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static int NormalizePage(int page)
    {
        return Math.Max(1, page);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, 100);
    }

    private static int ResolveTotalPages(int totalCount, int pageSize)
    {
        if (totalCount == 0)
            return 0;

        return (int)Math.Ceiling(totalCount / (decimal)pageSize);
    }

    private sealed record ParticipantProfitabilityRow(
        TrainerSessionProfitabilityDto Session,
        TrainerSessionParticipantProfitabilityDto Participant);
}
