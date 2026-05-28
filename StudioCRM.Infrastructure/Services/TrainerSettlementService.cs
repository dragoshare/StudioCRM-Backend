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
        int month)
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

        var sessions = await _context.Sessions
            .Include(s => s.Participants)
            .Where(s =>
                s.TrainerId == trainerId &&
                s.Status == "Completed" &&
                s.StartAt >= from &&
                s.StartAt < to)
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        var rates = await _context.TrainerRates
            .Where(r => r.TrainerId == trainerId)
            .ToListAsync();

        var items = new List<TrainerSettlementItemDto>();

        foreach (var session in sessions)
        {
            var sessionType = ResolveSettlementSessionType(session);
            var hours = ResolveBillableHours(sessionType, session.StartAt, session.EndAt);
            var rate = ResolveHourlyRate(rates, session.StartAt);

            var amount = hours * rate;

            items.Add(new TrainerSettlementItemDto
            {
                SessionId = session.Id,
                StartAt = ToStudioDisplayDateTime(session.StartAt),
                EndAt = ToStudioDisplayDateTime(session.EndAt),
                Title = session.Title,
                SessionType = sessionType,
                Hours = hours,
                Rate = rate,
                Amount = amount,
                ParticipantsCount = session.ActualParticipantsCount ?? session.Participants.Count
            });
        }

        return new TrainerMonthlySettlementDto
        {
            TrainerId = trainer.Id,
            TrainerFullName = $"{trainer.User.FirstName} {trainer.User.LastName}",
            Year = year,
            Month = month,
            TotalHours = items.Sum(i => i.Hours),
            TotalSessions = items.Count,
            TotalAmount = items.Sum(i => i.Amount),
            IsPaid = savedSettlement?.IsPaid ?? false,
            PaidAt = savedSettlement?.PaidAt.HasValue == true
                ? ToStudioDisplayDateTime(savedSettlement.PaidAt.Value)
                : null,
            Items = items
        };
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
        int month)
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
            .Include(s => s.Participants)
            .Where(s =>
                s.TrainerId == trainerId &&
                s.Status == "Completed" &&
                s.StartAt >= from &&
                s.StartAt < to)
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        var rates = await _context.TrainerRates
            .Where(r => r.TrainerId == trainerId)
            .ToListAsync();

        var contract = await ResolveContractForPeriodAsync(trainerId, from, to);
        var hourlyRate = ResolveHourlyRate(rates, from);

        if (hourlyRate <= 0 && sessions.Count > 0)
            hourlyRate = ResolveHourlyRate(rates, sessions[0].StartAt);

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
            Rows = BuildWorkHourRows(sessions)
        };

        return new TrainerWorkHoursDocumentDto
        {
            FileName = BuildWorkHoursFileName(trainer.User.FirstName, trainer.User.LastName, year, month),
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

    private async Task<TrainerContract> ResolveContractForPeriodAsync(
        int trainerId,
        DateTime from,
        DateTime to)
    {
        var contract = await _context.TrainerContracts
            .Where(c =>
                c.TrainerId == trainerId &&
                c.IsActive &&
                c.ValidFrom < to &&
                (c.ValidTo == null || c.ValidTo >= from))
            .OrderByDescending(c => c.ValidFrom)
            .FirstOrDefaultAsync();

        return contract
            ?? throw new InvalidOperationException(
                "Trainer does not have an active contract for this settlement month.");
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
