using StudioCRM.Application.DTOs.ClientPackages;
namespace StudioCRM.Application.Interfaces;

public interface IClientPackageService
{
    Task<int> CreateAsync(CreateClientPackageRequest request);
    Task<bool> ActivateAsync(int clientId, int clientPackageId);
    Task<bool> DeleteAsync(int clientId, int clientPackageId);
    Task<PackageManagementPreviewDto> GetManagementPreviewAsync(int clientId, int clientPackageId);
    Task<PackageCorrectionResultDto> CorrectAsync(int clientId, int clientPackageId, PackageChangeRequest request);
    Task<int?> CloseAsync(int clientId, int clientPackageId, ClosePackageRequest request);
    Task<int?> DecideExpiredAsync(int clientId, int clientPackageId, ExpiredPackageDecisionRequest request);
    Task<int> ImportAsync(ImportClientPackageRequest request);
    Task<int> CreateHistoricalAsync(HistoricalClientPackageRequest request);
    Task<BalanceAdjustmentResultDto> AdjustBalanceAsync(int clientId, BalanceAdjustmentRequest request);
}
