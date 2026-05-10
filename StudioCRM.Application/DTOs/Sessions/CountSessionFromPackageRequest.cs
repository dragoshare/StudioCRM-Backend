using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Sessions;

public class CountSessionFromPackageRequest
{
    public int SessionParticipantId { get; set; }

    public int ClientPackageId { get; set; }

    public SessionBillingType ActualBillingType { get; set; }

    public decimal? ActualUnitPrice { get; set; }
}
