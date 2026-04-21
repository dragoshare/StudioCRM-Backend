using StudioCRM.Application.DTOs.Dashboard;

namespace StudioCRM.Application.Interfaces;

public interface IDashboardService
{
    Task<OwnerDashboardDto> GetOwnerDashboardAsync();
}