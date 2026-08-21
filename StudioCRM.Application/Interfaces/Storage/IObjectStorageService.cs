namespace StudioCRM.Application.Interfaces.Storage;

public interface IObjectStorageService
{
    Task<StoredObjectDto> UploadAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<StoredObjectDownloadDto> DownloadAsync(
        string key,
        string? fileName = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public class StoredObjectDto
{
    public string Key { get; set; } = string.Empty;
    public string? Url { get; set; }
}

public class StoredObjectDownloadDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
