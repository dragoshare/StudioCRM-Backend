using StudioCRM.Domain.Entities;

namespace StudioCRM.Application.Common;

public static class SessionTitleBuilder
{
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

    private static string ShortName(Client client)
    {
        var initial = string.IsNullOrWhiteSpace(client.LastName)
            ? string.Empty
            : client.LastName[0].ToString();

        return $"{client.FirstName} {initial}".Trim();
    }
}