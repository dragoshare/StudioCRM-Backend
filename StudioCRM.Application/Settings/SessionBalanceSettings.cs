using StudioCRM.Application.Common;

namespace StudioCRM.Application.Settings;

public class SessionBalanceSettings
{
    // Location id -> 1:1 level used for personal/semi balance differences (e.g. 55 gives a 35 PLN gap to 1:2).
    public Dictionary<string, decimal> OneToOneLevelByLocation { get; set; } = new();

    public decimal OneToOneLevel(int? locationId) =>
        locationId.HasValue && OneToOneLevelByLocation.TryGetValue(locationId.Value.ToString(), out var level)
            ? level
            : SessionBalancePolicy.DefaultOneToOneLevel;
}
