using StudioCRM.Application.DTOs.Public;

namespace StudioCRM.Application.Interfaces;

public interface IPublicGroupClassService
{
    Task<List<PublicGroupLocationDto>> GetLocationsAsync();

    Task<List<PublicGroupPackageDto>> GetPackagesAsync(int? locationId);

    Task<List<PublicGroupClassDto>> GetClassesAsync(PublicGroupClassFilterDto filter);

    Task<PublicGroupClassDto?> GetClassAsync(int id);

    Task<PublicGroupClassDto?> GetClassBySlugAsync(string slug);

    Task<PublicGroupPackageDto?> GetPackageBySlugAsync(string slug);

    Task<PublicGroupPurchaseDto> PurchasePackageForCurrentClientAsync(int packageId);

    Task<PublicGroupBookingDto> BookCurrentClientAsync(int sessionId);

    Task<bool> CancelCurrentClientBookingAsync(int sessionId);
}
