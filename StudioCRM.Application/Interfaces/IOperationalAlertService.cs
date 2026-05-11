using StudioCRM.Application.DTOs.Alerts;

namespace StudioCRM.Application.Interfaces;

public interface IOperationalAlertService
{
    Task<OperationalAlertsDto> GetAlertsAsync(OperationalAlertFilterDto filter);
}
