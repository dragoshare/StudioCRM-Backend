using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface IClientService
{
    Task<List<ClientLocationDto>> GetLocationsAsync(int clientId);
    Task SetLocationAccessAsync(int clientId, int locationId, SetClientLocationAccessRequest request);
    Task RemoveLocationAsync(int clientId, int locationId);
    Task<List<ClientDuplicateDto>> FindDuplicatesAsync(ClientDuplicateFilter filter);
    Task<PagedResultDto<ClientPackageBillingDto>> GetPackageHistoryAsync(int clientId, int page = 1, int pageSize = 25);
    Task<PagedResultDto<ClientRefundDto>> GetRefundsAsync(int? clientId, int page = 1, int pageSize = 25);
    Task<ClientDto> CreateAsync(CreateClientDto request);
    Task<List<ClientDto>> GetAllAsync();
    Task<ClientDto?> GetByIdAsync(int id);
    Task<ClientWorkspaceDto?> GetWorkspaceAsync(int id);
    Task<ClientDto?> UpdateAsync(int id, UpdateClientDto request);
    Task<bool> DeleteAsync(int id);
    Task<ClientArchiveCheckDto> CheckArchiveAsync(int id);
    Task<List<ClientArchiveResultDto>> ArchiveManyAsync(BulkArchiveClientsRequest request);
    Task<bool> SetPortalAccessAsync(int id, bool blocked);
    Task<bool> DeletePermanentlyAsync(int id);
    Task<bool> AssignTrainerAsync(int id, SetClientTrainerRequest request);
    Task<List<ClientDto>> GetFilteredAsync(ClientFilterDto filter);
    Task<bool> RestoreAsync(int id);
    Task<List<ClientDto>> GetDeletedAsync();
    Task<List<ClientLegalConsentDto>> GetLegalConsentsAsync(int clientId);
    Task<PagedResultDto<ClientSessionHistoryDto>> GetSessionHistoryAsync(int clientId, ClientHistoryFilter filter);
    Task<PagedResultDto<ClientAuditDto>> GetAuditAsync(int clientId, int page = 1, int pageSize = 25);
    Task<ClientClosurePreviewDto> GetClosurePreviewAsync(int clientId);
    Task<ClientClosurePreviewDto> CloseCooperationAsync(int clientId, CloseClientRequest request);
    Task ConfirmRefundAsync(int clientId, int packageId, ConfirmClientRefundRequest request);
    Task ResumeRetainedPackageAsync(int clientId, int packageId, ResumeRetainedPackageRequest request);
}
