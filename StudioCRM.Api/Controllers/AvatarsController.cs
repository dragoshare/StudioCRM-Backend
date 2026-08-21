using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/avatars")]
[Authorize]
public class AvatarsController : ControllerBase
{
    private readonly IAvatarService _avatarService;

    public AvatarsController(IAvatarService avatarService)
    {
        _avatarService = avatarService;
    }

    [HttpPost("me")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarDto>> UploadMyAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Avatar file is required." });

        return await HandleAsync(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _avatarService.UploadCurrentUserAvatarAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken));
        });
    }

    [HttpDelete("me")]
    public async Task<ActionResult<AvatarDto>> DeleteMyAvatar(CancellationToken cancellationToken)
    {
        return await HandleAsync(async () =>
            Ok(await _avatarService.DeleteCurrentUserAvatarAsync(cancellationToken)));
    }

    [HttpPost("clients/{clientId:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarDto>> UploadClientAvatar(
        int clientId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Avatar file is required." });

        return await HandleAsync(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _avatarService.UploadClientAvatarAsync(
                clientId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken));
        });
    }

    [HttpDelete("clients/{clientId:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<AvatarDto>> DeleteClientAvatar(
        int clientId,
        CancellationToken cancellationToken)
    {
        return await HandleAsync(async () =>
            Ok(await _avatarService.DeleteClientAvatarAsync(clientId, cancellationToken)));
    }

    [HttpPost("trainers/{trainerId:int}")]
    [Authorize(Roles = "Owner")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarDto>> UploadTrainerAvatar(
        int trainerId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Avatar file is required." });

        return await HandleAsync(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _avatarService.UploadTrainerAvatarAsync(
                trainerId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken));
        });
    }

    [HttpDelete("trainers/{trainerId:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<AvatarDto>> DeleteTrainerAvatar(
        int trainerId,
        CancellationToken cancellationToken)
    {
        return await HandleAsync(async () =>
            Ok(await _avatarService.DeleteTrainerAvatarAsync(trainerId, cancellationToken)));
    }

    private async Task<ActionResult<AvatarDto>> HandleAsync(Func<Task<ActionResult<AvatarDto>>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
