using Microsoft.AspNetCore.Mvc.Controllers;
using StudioCRM.Api.Controllers;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Api.Security;

public sealed class OrganizationCapabilityMiddleware(RequestDelegate next)
{
    private static readonly HashSet<Type> CoreControllers = [
        typeof(LocationsController), typeof(TrainersController), typeof(ClientsController), typeof(PackagesController),
        typeof(SessionsController), typeof(SessionParticipantsController), typeof(ClientPackagesController),
        typeof(BillingController), typeof(ExpensesController), typeof(PeriodPackagesController),
        typeof(DashboardController), typeof(OwnerOverviewController), typeof(OwnerPanelController),
        typeof(OwnerSettingsController), typeof(NotificationsController), typeof(ActivityLogController),
        typeof(OwnerMilestonesController), typeof(TrainerPortalMilestonesController)
    ];
    public async Task InvokeAsync(HttpContext http, RequestOrganizationContext scope)
    {
        if (http.User.Identity?.IsAuthenticated == true && scope.OrganizationId != StudioCRMDbContext.LegacyOrganizationId)
        {
            var action = http.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var type = action?.ControllerTypeInfo.AsType();
            var allowed = type != null && CoreControllers.Contains(type)
                || (type == typeof(TrainerPortalSeriesController) && action!.ActionName is "Create" or "Delete")
                || (type == typeof(TrainerPortalController) && !action!.ActionName.Contains("Invit", StringComparison.OrdinalIgnoreCase))
                || (type == typeof(ClientPortalController) && action!.ActionName is not ("RequestEmailChange" or "RequestPayment"))
                || (type == typeof(CurrentOrganizationController) && action!.ActionName == "Get")
                || (type == typeof(AuthController) && action!.ActionName is "Me" or "Organizations" or "Logout" or "ChangePassword" or "Login" or "Refresh" or "Register" or "UploadMyAvatar" or "DeleteMyAvatar");
            if (!allowed)
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                await http.Response.WriteAsJsonAsync(new { code = "OrganizationCapabilityUnavailable", message = "This integration or account flow is not enabled for this organization yet." });
                return;
            }
        }
        await next(http);
    }
}
