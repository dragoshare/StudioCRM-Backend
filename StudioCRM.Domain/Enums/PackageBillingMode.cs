using System.Text.Json.Serialization;
namespace StudioCRM.Domain.Enums;
[JsonConverter(typeof(JsonStringEnumConverter<PackageBillingMode>))]
public enum PackageBillingMode { SessionBased = 0, PeriodBased = 1 }
