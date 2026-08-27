using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface IPaymentConfigurationService
{
    Task<PaymentConfigurationDto> GetConfigurationAsync();
    Task<LegalEntityDto> CreateLegalEntityAsync(UpsertLegalEntityRequest request);
    Task<LegalEntityDto?> UpdateLegalEntityAsync(int id, UpsertLegalEntityRequest request);
    Task<PaymentProviderAccountDto> CreatePaymentProviderAccountAsync(UpsertPaymentProviderAccountRequest request);
    Task<PaymentProviderAccountDto?> UpdatePaymentProviderAccountAsync(int id, UpsertPaymentProviderAccountRequest request);
}
