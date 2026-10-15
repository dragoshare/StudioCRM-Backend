using StudioCRM.Domain.Entities;

namespace StudioCRM.Application.Common;

public static class SessionTitleBuilder
{
    public const string GroupClassOutlookCategory = "Zajęcia grupowe";

    public static bool ShouldDeriveFromParticipants(
        bool isPubliclyBookable,
        string? plannedSessionType)
    {
        return !isPubliclyBookable &&
            !ConsultationSessions.IsConsultation(plannedSessionType) &&
            !string.Equals(
                plannedSessionType,
                nameof(StudioCRM.Domain.Enums.SessionBillingType.Group),
                StringComparison.OrdinalIgnoreCase);
    }

    public static string Build(List<Client> clients)
    {
        var ordered = clients
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .ToList();

        if (ordered.Count == 0)
            return "Sesja";

        return string.Join(" + ", ordered.Select(ShortName));
    }

    public static string BuildOutlookSubject(Session session)
    {
        // A consultation keeps the title written in Outlook (usually the prospect's name).
        if (ConsultationSessions.IsConsultation(session.PlannedSessionType))
            return session.Title;

        if (!ShouldDeriveFromParticipants(session.IsPubliclyBookable, session.PlannedSessionType))
            return $"ZAJĘCIA GRUPOWE: {session.Title} | {session.Location.Name}";

        return Build(session.Participants.Select(p => p.Client).ToList());
    }

    private static string ShortName(Client client)
    {
        var initial = string.IsNullOrWhiteSpace(client.LastName)
            ? string.Empty
            : client.LastName[0].ToString();

        return $"{client.FirstName} {initial}".Trim();
    }
}
