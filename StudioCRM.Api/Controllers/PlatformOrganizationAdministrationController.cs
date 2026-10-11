using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.DTOs.Organizations;
namespace StudioCRM.Api.Controllers;

[ApiController, Authorize(Roles = "SuperAdmin")]
[Route("api/super-admin/organizations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PlatformOrganizationAdministrationController(IPlatformOrganizationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationRequest request)
    {
        var result = await service.CreateAsync(request);
        return Created($"/api/super-admin/organizations/{result.OrganizationId}", result);
    }
    [HttpPatch("{organizationId:guid}")]
    public async Task<IActionResult> Update(Guid organizationId, UpdateOrganizationRequest request) => Ok(await service.UpdateAsync(organizationId, request));
    [HttpPut("{organizationId:guid}/owners/{userId:int}")]
    public async Task<IActionResult> Owner(Guid organizationId, int userId, AssignOrganizationOwnerRequest request) => Ok(await service.AssignOwnerAsync(organizationId, userId, request));
    [HttpGet("{organizationId:guid}/users")]
    public async Task<IActionResult> Users(Guid organizationId, string? role = null, int page = 1, int pageSize = 25) => Ok(await service.UsersAsync(organizationId, role, page, pageSize));
}
[ApiController, Authorize(Roles = "SuperAdmin")]
[Route("api/super-admin/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PlatformUsersController(IPlatformOrganizationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string query, int page = 1, int pageSize = 25) => Ok(await service.SearchUsersAsync(query, page, pageSize));
}
