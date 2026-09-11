namespace StudioCRM.Application.Interfaces;

public interface ITpayConnectionService
{
    Task<bool> TestConnectionAsync(string accountKey, CancellationToken cancellationToken);
}
