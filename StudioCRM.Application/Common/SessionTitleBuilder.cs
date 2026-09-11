using StudioCRM.Domain.Entities;

namespace StudioCRM.Application.Common;

public static class SessionTitleBuilder
{
    public static bool ShouldDeriveFromParticipants(
        bool isPubliclyBookable,
        string? plannedSessionType)
    {
        return !isPubliclyBookable &&
            !string.Equals(plannedSessionType, "Group", StringComparison.OrdinalIgnoreCase);
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
        var subject = $"StudioCRM: {session.Title}";
        if (!ShouldDeriveFromParticipants(session.IsPubliclyBookable, session.PlannedSessionType))
            return subject;

        var clientName = string.Join(" + ", session.Participants.Select(p =>
            $"{p.Client.FirstName} {p.Client.LastName}".Trim()));

        return $"{subject} - {clientName}";
    }

    private static string ShortName(Client client)
    {
        var initial = string.IsNullOrWhiteSpace(client.LastName)
            ? string.Empty
            : client.LastName[0].ToString();

        return $"{client.FirstName} {initial}".Trim();
    }
}
