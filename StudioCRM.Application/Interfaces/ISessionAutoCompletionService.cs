namespace StudioCRM.Application.Interfaces;

public interface ISessionAutoCompletionService
{
    Task<SessionAutoCompletionResult> CompleteFinishedSessionsAsync(CancellationToken cancellationToken = default);
}

public class SessionAutoCompletionResult
{
    public int CompletedCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }
}
