using StudioCRM.Application.DTOs.Locations;

namespace StudioCRM.Application.DTOs.Billing;

public class PaymentConfigurationDto
{
    public List<LegalEntityDto> LegalEntities { get; set; } = new();

    public List<PaymentProviderAccountDto> PaymentProviderAccounts { get; set; } = new();

    public List<LocationDto> Locations { get; set; } = new();
}
