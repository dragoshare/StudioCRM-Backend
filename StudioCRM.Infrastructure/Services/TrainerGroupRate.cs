using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Services;

internal static class TrainerGroupRate
{
    internal static decimal? Resolve(Session session, string sessionType, IEnumerable<TrainerRate> rates)
    {
        var isGroup = session.IsPubliclyBookable ||
            string.Equals(session.PlannedSessionType, "Group", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sessionType, "Group", StringComparison.OrdinalIgnoreCase);
        if (!isGroup)
            return null;

        return rates
            .Where(r => r.TrainerId == session.TrainerId && r.SessionType == "Group" &&
                r.ValidFrom <= session.StartAt && (r.ValidTo == null || session.StartAt < r.ValidTo))
            .OrderByDescending(r => r.ValidFrom)
            .ThenByDescending(r => r.Id)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefault();
    }
}
