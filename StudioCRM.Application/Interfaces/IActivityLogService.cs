using StudioCRM.Application.DTOs.ActivityLog;
using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface IActivityLogService
{
    Task<PagedResultDto<ActivityLogDto>> GetAsync(ActivityLogFilter filter, CancellationToken cancellationToken = default);
    ActivityLogMetadataDto GetMetadata();
}
