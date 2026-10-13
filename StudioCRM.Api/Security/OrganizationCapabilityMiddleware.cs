using Microsoft.AspNetCore.Mvc.Controllers;
using StudioCRM.Api.Controllers;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Api.Security;
// New studios are restricted to the audited initial surface.
public sealed class OrganizationCapabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, RequestOrganizationContext scope)
    {
        if (http.User.Identity?.IsAuthenticated == true && scope.OrganizationId != StudioCRMDbContext.LegacyOrganizationId)
        {
            var action = http.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var allowed = action?.ControllerTypeInfo.AsType() == typeof(LocationsController)
                || (action?.ControllerTypeInfo.AsType() == typeof(CurrentOrganizationController) && action.ActionName == "Get")
                || (action?.ControllerTypeInfo.AsType() == typeof(AuthController)
                    && action.ActionName is "Me" or "Logout" or "ChangePassword" or "Login" or "Refresh");
            if (!allowed)
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                await http.Response.WriteAsJsonAsync(new { code = "OrganizationCapabilityUnavailable", message = "This feature is not enabled for this organization yet." });
                return;
            }
        }
        await next(http);
    }
}
