using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Api.Controllers;
[ApiController]
[Authorize]
[Route("api/group-enrollments")]
public class GroupEnrollmentsController(GroupEnrollmentService service) : ControllerBase
{
    [HttpPost("sessions/{sessionId:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<GroupEnrollmentDto>> Prepare(int sessionId, PrepareGroupEnrollmentRequest request) => Ok(await service.PrepareAsync(sessionId, request));
    [HttpGet("sessions/{sessionId:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> List(int sessionId, int page = 1, int pageSize = 25) => Ok(await service.ListAsync(sessionId, page, pageSize));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<GroupEnrollmentDto>> Get(int id) => Ok(await service.GetAsync(id));
    [HttpPost("{id:int}/confirm")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<GroupEnrollmentDto>> Confirm(int id) => Ok(await service.ConfirmAsync(id));
}
