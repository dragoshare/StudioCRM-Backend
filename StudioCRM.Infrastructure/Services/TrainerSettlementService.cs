using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainerSettlementService : ITrainerSettlementService
{
    private const string WindowsStudioTimeZone = "Central European Standard Time";

    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TrainerSettlementService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TrainerMonthlySettlementDto?> GetMonthlySettlementAsync(
        int trainerId,
        int year,
        int month,
        int? locationId = null)
    {
        ValidateMonth(year, month);

        await EnsureAccessAsync(trainerId);

        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == trainerId);

        if (trainer is null)
            return null;

        var from = GetStudioMonthStartUtc(year, month);
        var nextYear = month == 12 ? year + 1 : year;
        var nextMonth = month == 12 ? 1 : month + 1;
        var to = GetStudioMonthStartUtc(nextYear, nextMonth);

        var savedSettlement = await _context.TrainerMonthlySettlements
            .FirstOrDefaultAsync(s =>
                s.TrainerId == trainerId &&
                s.Year == year &&
                s.Month == month);

        var sessions = await LoadCompletedSessionsAsync([trainerId], from, to, locationId);

        var rates = await _context.TrainerRates
            .Where(r => r.TrainerId == trainerId)
            .ToListAsync();

        var contracts = await GetContractsForPeriodAsync(trainerId, from, to);
        var items = BuildSettlementItems(sessions, rates, contracts);
        var contractedItems = items
            .Where(i => i.IsCoveredByContract)
            .ToList();
        var nonContractedItems = items
            .Where(i => !i.IsCoveredByContract)
            .ToList();

        return new TrainerMonthlySettlementDto
        {
            TrainerId = trainer.Id,
            LocationId = locationId,
            TrainerFullName = $"{trainer.User.FirstName} {trainer.User.LastName}",
            Year = year,
            Month = month,
            TotalHours = contractedItems.Sum(i => i.Hours),
            TotalSessions = contractedItems.Count,
            TotalAmount = contractedItems.Sum(i => i.Amount),
            ContractedTotalHours = contractedItems.Sum(i => i.Hours),
            ContractedTotalSessions = contractedItems.Count,
            ContractedTotalAmount = contractedItems.Sum(i => i.Amount),
            NonContractedTotalHours = nonContractedItems.Sum(i => i.Hours),
            NonContractedTotalSessions = nonContractedItems.Count,
            IsPaid = savedSettlement?.IsPaid ?? false,
            PaidAt = savedSettlement?.PaidAt.HasValue == true
                ? ToStudioDisplayDateTime(savedSettlement.PaidAt.Value)
                : null,
            Items = contractedItems,
            NonContractedItems = nonContractedItems
        };
    }

    public async Task<TrainerSettlementListDto> GetMonthlySettlementsAsync(TrainerSettlementListFilter filter)
    {
        if (!_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Only owner can list trainer settlements.");
        filter.Validate();

        var (year, month) = (filter.Year, filter.Month);
        var from = GetStudioMonthStartUtc(year, month);
        var to = GetStudioMonthStartUtc(month == 12 ? year + 1 : year, month == 12 ? 1 : month + 1);
        var locationId = filter.LocationId;

        var trainersQuery = _context.Trainers.AsNoTracking();
        if (locationId.HasValue)
            trainersQuery = trainersQuery.Where(t =>
                t.TrainerLocations.Any(l => l.LocationId == locationId.Value) ||
                t.Sessions.Any(s => s.Status == "Completed" && s.LocationId == locationId.Value && s.StartAt >= from && s.StartAt < to));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            trainersQuery = trainersQuery.Where(t =>
                (t.User.FirstName + " " + t.User.LastName).ToLower().Contains(term) || t.User.Email.ToLower().Contains(term));
        }

        var trainers = await trainersQuery
            .OrderBy(t => t.User.LastName).ThenBy(t => t.User.FirstName).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.User.FirstName, t.User.LastName })
            .ToListAsync();
        var ids = trainers.Select(t => t.Id).ToArray();

        // One query per data set for the whole filtered list; totals reuse the single-trainer rules.
        var sessions = (await LoadCompletedSessionsAsync(ids, from, to, locationId)).ToLookup(s => s.TrainerId);
        var rates = (await _context.TrainerRates.AsNoTracking().Where(r => ids.Contains(r.TrainerId)).ToListAsync()).ToLookup(r => r.TrainerId);
        var contracts = (await _context.TrainerContracts.AsNoTracking()
            .Include(c => c.ContractLocations).ThenInclude(cl => cl.Location)
            .Where(c => ids.Contains(c.TrainerId) && c.IsActive && c.ValidFrom < to && (c.ValidTo == null || c.ValidTo >= from))
            .OrderByDescending(c => c.ValidFrom).ThenByDescending(c => c.Id)
            .ToListAsync()).ToLookup(c => c.TrainerId);
        var settlements = await _context.TrainerMonthlySettlements.AsNoTracking()
            .Where(s => ids.Contains(s.TrainerId) && s.Year == year && s.Month == month)
            .ToDictionaryAsync(s => s.TrainerId);

        var items = trainers.Select(t =>
        {
            var built = BuildSettlementItems(sessions[t.Id].ToList(), rates[t.Id].ToList(), contracts[t.Id].ToList());
            var contracted = built.Where(i => i.IsCoveredByContract).ToList();
            var nonContracted = built.Where(i => !i.IsCoveredByContract).ToList();
            var saved = settlements.GetValueOrDefault(t.Id);
            return new TrainerSettlementListItemDto
            {
                TrainerId = t.Id,
                TrainerFullName = $"{t.FirstName} {t.LastName}",
                TotalHours = contracted.Sum(i => i.Hours),
                TotalSessions = contracted.Count,
                TotalAmount = contracted.Sum(i => i.Amount),
                NonContractedTotalHours = nonContracted.Sum(i => i.Hours),
                NonContractedTotalSessions = nonContracted.Count,
                IsPaid = saved?.IsPaid ?? false,
                PaidAt = saved?.PaidAt.HasValue == true ? ToStudioDisplayDateTime(saved.PaidAt.Value) : null
            };
        }).ToList();

        var totalAmount = items.Sum(i => i.TotalAmount);
        var paidAmount = items.Where(i => i.IsPaid).Sum(i => i.TotalAmount);
        return new TrainerSettlementListDto
        {
            Year = year,
            Month = month,
            LocationId = locationId,
            Items = items.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = items.Count,
            TotalPages = items.Count == 0 ? 0 : (int)Math.Ceiling(items.Count / (decimal)filter.PageSize),
            Summary = new TrainerSettlementListSummaryDto
            {
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                UnpaidAmount = totalAmount - paidAmount,
                TrainersCount = items.Count,
                PaidTrainersCount = items.Count(i => i.IsPaid)
            },
            AsOf = DateTime.UtcNow,
            TimeZone = "Europe/Warsaw"
        };
    }

    private async Task<List<Session>> LoadCompletedSessionsAsync(int[] trainerIds, DateTime from, DateTime to, int? locationId)
    {
        return await _context.Sessions
            .AsNoTracking()
            .Where(s =>
                trainerIds.Contains(s.TrainerId) &&
                s.Status == "Completed" &&
                (!locationId.HasValue || s.LocationId == locationId.Value) &&
                s.StartAt >= from &&
                s.StartAt < to)
            .OrderBy(s => s.StartAt)
            .Select(s => new Session { Id = s.Id, Title = s.Title, TrainerId = s.TrainerId,
                StartAt = s.StartAt, EndAt = s.EndAt, LocationId = s.LocationId,
                Location = new Location { Id = s.LocationId, Name = s.Location.Name },
                ActualSessionType = s.ActualSessionType, PlannedSessionType = s.PlannedSessionType,
                ActualParticipantsCount = s.ActualParticipantsCount ?? s.Participants.Count,
                IsPubliclyBookable = s.IsPubliclyBookable })
            .ToListAsync();
    }

    public async Task<TrainerMonthlySettlementDto?> MarkAsPaidAsync(
        int trainerId,
        int year,
        int month)
    {
        ValidateMonth(year, month);

        if (!_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Only owner can mark settlement as paid.");

        var preview = await GetMonthlySettlementAsync(trainerId, year, month);

        if (preview is null)
            return null;

        var settlement = await _context.TrainerMonthlySettlements
            .FirstOrDefaultAsync(s =>
                s.TrainerId == trainerId &&
                s.Year == year &&
                s.Month == month);

        var now = DateTime.UtcNow;

        if (settlement is null)
        {
            settlement = new TrainerMonthlySettlement
            {
                TrainerId = trainerId,
                Year = year,
                Month = month,
                CreatedAt = now
            };

            await _context.TrainerMonthlySettlements.AddAsync(settlement);
        }

        settlement.TotalAmount = preview.TotalAmount;
        settlement.TotalHours = preview.TotalHours;
        settlement.TotalSessions = preview.TotalSessions;
        settlement.IsPaid = true;
        settlement.PaidAt = now;
        settlement.PaidByUserId = _currentUser.UserId;
        settlement.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return await GetMonthlySettlementAsync(trainerId, year, month);
    }

    public async Task<TrainerMonthlySettlementDto?> ReopenAsync(
        int trainerId,
        int year,
        int month)
    {
        ValidateMonth(year, month);

        if (!_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Only owner can reopen settlement.");

        var preview = await GetMonthlySettlementAsync(trainerId, year, month);

        if (preview is null)
            return null;

        var settlement = await _context.TrainerMonthlySettlements
            .FirstOrDefaultAsync(s =>
                s.TrainerId == trainerId &&
                s.Year == year &&
                s.Month == month);

        if (settlement is null)
            return preview;

        settlement.IsPaid = false;
        settlement.PaidAt = null;
        settlement.PaidByUserId = null;
        settlement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetMonthlySettlementAsync(trainerId, year, month);
    }

    public async Task<TrainerWorkHoursDocumentDto?> GenerateWorkHoursDocumentAsync(
        int trainerId,
        int year,
        int month,
        int? locationId = null)
    {
        ValidateMonth(year, month);

        await EnsureAccessAsync(trainerId);

        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == trainerId);

        if (trainer is null)
            return null;

        var from = GetStudioMonthStartUtc(year, month);
        var nextYear = month == 12 ? year + 1 : year;
        var nextMonth = month == 12 ? 1 : month + 1;
        var to = GetStudioMonthStartUtc(nextYear, nextMonth);

        var sessions = await _context.Sessions
            .Include(s => s.Location)
            .Include(s => s.Participants)
            .Where(s =>
                s.TrainerId == trainerId &&
                s.Status == "Completed" &&
                (!locationId.HasValue || s.LocationId == locationId.Value) &&
                s.StartAt >= from &&
                s.StartAt < to)
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        var rates = await _context.TrainerRates
            .Where(r => r.TrainerId == trainerId)
            .ToListAsync();

        var contracts = await GetContractsForPeriodAsync(trainerId, from, to);
        if (locationId.HasValue)
            contracts = contracts.Where(c => c.ContractLocations.Any(l => l.LocationId == locationId.Value)).ToList();
        var contractedSessions = sessions
            .Where(s => ResolveCoveringContract(contracts, s) != null)
            .ToList();
        var contract = ResolveDocumentContract(contracts, contractedSessions);
        var hourlyRate = ResolveHourlyRate(rates, from);

        if (hourlyRate <= 0 && contractedSessions.Count > 0)
            hourlyRate = ResolveHourlyRate(rates, contractedSessions[0].StartAt);

        var model = new WorkHoursDocumentModel
        {
            TrainerFirstName = trainer.User.FirstName,
            TrainerLastName = trainer.User.LastName,
            ContractType = contract.ContractType,
            ContractNumber = contract.ContractNumber,
            ContractSignedAt = ToStudioDisplayDateTime(contract.SignedAt),
            Year = year,
            Month = month,
            HourlyRate = hourlyRate,
            Rows = BuildWorkHourRows(contractedSessions)
        };

        return new TrainerWorkHoursDocumentDto
        {
            FileName = BuildWorkHoursFileName(trainer.User.FirstName, trainer.User.LastName, year, month)
                .Replace(".docx", locationId.HasValue ? $"-lokalizacja-{locationId.Value}.docx" : ".docx"),
            Content = WorkHoursDocumentBuilder.Build(model)
        };
    }

    private async Task EnsureAccessAsync(int trainerId)
    {
        if (_currentUser.IsOwner)
            return;

        if (_currentUser.IsTrainer)
        {
            var currentTrainerId = await _context.Trainers
                .Where(t => t.UserId == _currentUser.UserId)
                .Select(t => t.Id)
                .FirstOrDefaultAsync();

            if (currentTrainerId == trainerId)
                return;
        }

        throw new UnauthorizedAccessException("You do not have access to this settlement.");
    }

    private async Task<List<TrainerContract>> GetContractsForPeriodAsync(
        int trainerId,
        DateTime from,
        DateTime to)
    {
        return await _context.TrainerContracts
            .Include(c => c.ContractLocations)
                .ThenInclude(cl => cl.Location)
            .Where(c =>
                c.TrainerId == trainerId &&
                c.IsActive &&
                c.ValidFrom < to &&
                (c.ValidTo == null || c.ValidTo >= from))
            .OrderByDescending(c => c.ValidFrom)
            .ThenByDescending(c => c.Id)
            .ToListAsync();
    }

    private static TrainerContract ResolveDocumentContract(
        List<TrainerContract> contracts,
        List<Session> contractedSessions)
    {
        if (contracts.Count == 0)
        {
            throw new InvalidOperationException(
                "Trainer does not have an active contract for this settlement month.");
        }

        if (contractedSessions.Count == 0)
        {
            return contracts[0];
        }

        var contractsUsed = contractedSessions
            .Select(s => ResolveCoveringContract(contracts, s)!)
            .DistinctBy(c => c.Id)
            .ToList();

        if (contractsUsed.Count > 1)
        {
            throw new InvalidOperationException(
                "Work-hours document can only be generated when the month's contracted sessions belong to one contract.");
        }

        return contractsUsed[0];
    }

    // A session is contracted only when its studio date falls within the contract period (both ends inclusive)
    // and its location is one of the contract locations. Contracts are ordered newest first.
    internal static TrainerContract? ResolveCoveringContract(IEnumerable<TrainerContract> contracts, Session session)
    {
        var sessionDate = ToStudioDisplayDateTime(session.StartAt).Date;
        return contracts.FirstOrDefault(c =>
            c.ContractLocations.Any(l => l.LocationId == session.LocationId) &&
            ToStudioDisplayDateTime(c.ValidFrom).Date <= sessionDate &&
            (!c.ValidTo.HasValue || sessionDate <= ToStudioDisplayDateTime(c.ValidTo.Value).Date));
    }

    internal static List<TrainerSettlementItemDto> BuildSettlementItems(
        List<Session> sessions,
        List<TrainerRate> rates,
        List<TrainerContract> contracts)
    {
        var items = new List<TrainerSettlementItemDto>();

        foreach (var session in sessions)
        {
            var sessionType = ResolveSettlementSessionType(session);
            var hours = ResolveBillableHours(sessionType, session.StartAt, session.EndAt);
            var groupRate = TrainerGroupRate.Resolve(session, sessionType, rates);
            var rate = groupRate ?? ResolveHourlyRate(rates, session.StartAt);
            var amount = groupRate ?? hours * rate;
            var contract = ResolveCoveringContract(contracts, session);
            var isCovered = contract != null;

            items.Add(new TrainerSettlementItemDto
            {
                SessionId = session.Id,
                StartAt = ToStudioDisplayDateTime(session.StartAt),
                EndAt = ToStudioDisplayDateTime(session.EndAt),
                Title = session.Title,
                SessionType = sessionType,
                LocationId = session.LocationId,
                LocationName = session.Location.Name,
                IsCoveredByContract = isCovered,
                ContractId = contract?.Id,
                ContractNumber = contract?.ContractNumber,
                Hours = hours,
                Rate = rate,
                RateType = groupRate.HasValue ? "PerSession" : "Hourly",
                Amount = amount,
                ParticipantsCount = session.ActualParticipantsCount ?? session.Participants.Count
            });
        }

        return items;
    }

    private static List<WorkHoursDocumentRow> BuildWorkHourRows(List<Session> sessions)
    {
        var localSessions = sessions
            .Select(session => new
            {
                Session = session,
                StartAt = ToStudioDisplayDateTime(session.StartAt),
                EndAt = ToStudioDisplayDateTime(session.EndAt),
                SessionType = ResolveSettlementSessionType(session)
            })
            .Where(session => session.EndAt > session.StartAt)
            .OrderBy(session => session.StartAt)
            .ToList();

        var rows = new List<WorkHoursDocumentRow>();
        WorkHoursDocumentRow? current = null;
        DateTime currentEndAt = default;

        foreach (var session in localSessions)
        {
            var startsNewBlock = current is null;

            if (current is not null)
            {
                startsNewBlock =
                    session.StartAt.Date != current.Date.Date ||
                    session.StartAt > currentEndAt;
            }

            if (startsNewBlock)
            {
                current = new WorkHoursDocumentRow
                {
                    Date = session.StartAt.Date,
                    StartAt = session.StartAt,
                    EndAt = session.EndAt
                };

                rows.Add(current);
            }
            else if (session.EndAt > current!.EndAt)
            {
                current.EndAt = session.EndAt;
            }

            currentEndAt = current!.EndAt;
            ApplySemiPersonalBonus(current, session.SessionType);
            current.Hours = ResolveClockHours(current.StartAt, current.EndAt);
        }

        return rows;
    }

    private static void ApplySemiPersonalBonus(WorkHoursDocumentRow row, string sessionType)
    {
        switch (sessionType)
        {
            case "TwoToOne":
                row.TwoToOneBonusUnits += 1;
                break;
            case "ThreeToOne":
                row.ThreeToOneBonusUnits += 1;
                break;
            case "FourToOne":
                row.FourToOneBonusUnits += 1;
                break;
        }
    }

    private static decimal ResolveClockHours(DateTime startAt, DateTime endAt)
    {
        return Math.Round((decimal)(endAt - startAt).TotalHours, 2);
    }

    private static string BuildWorkHoursFileName(
        string firstName,
        string lastName,
        int year,
        int month)
    {
        var trainer = string.Join(
            "-",
            new[] { firstName, lastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(SanitizeFileNamePart));

        if (string.IsNullOrWhiteSpace(trainer))
            trainer = "trainer";

        return $"ewidencja-godzin-{trainer}-{year}-{month:00}.docx";
    }

    private static string SanitizeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select(ch => invalidChars.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "trainer"
            : sanitized;
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year < 2000 || year > 2100)
            throw new InvalidOperationException("Invalid year.");

        if (month < 1 || month > 12)
            throw new InvalidOperationException("Invalid month.");
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
            _ => "Group"
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

    private static DateTime GetStudioMonthStartUtc(int year, int month)
    {
        var local = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, GetStudioTimeZone());
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
}
