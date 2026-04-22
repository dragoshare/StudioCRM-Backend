namespace StudioCRM.Application.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Email { get; }
    List<string> Roles { get; }

    bool IsAuthenticated { get; }
    bool IsOwner { get; }
    bool IsTrainer { get; }
    bool IsClient { get; }
}