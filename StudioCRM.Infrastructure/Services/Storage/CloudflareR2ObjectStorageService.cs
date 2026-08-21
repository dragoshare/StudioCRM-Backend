using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Application.Settings;

namespace StudioCRM.Infrastructure.Services.Storage;

public class CloudflareR2ObjectStorageService : IObjectStorageService
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Region = "auto";
    private const string Service = "s3";
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private readonly HttpClient _httpClient;
    private readonly CloudflareR2Settings _settings;

    public CloudflareR2ObjectStorageService(
        HttpClient httpClient,
        IOptions<CloudflareR2Settings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<StoredObjectDto> UploadAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var safeContentType = NormalizeContentType(contentType);
        var payloadHash = Hash(content);
        using var request = CreateSignedRequest(HttpMethod.Put, key, payloadHash, safeContentType);
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(safeContentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "R2 upload", cancellationToken);

        return new StoredObjectDto
        {
            Key = key,
            Url = BuildPublicUrl(key)
        };
    }

    public async Task<StoredObjectDownloadDto> DownloadAsync(
        string key,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = CreateSignedRequest(HttpMethod.Get, key, EmptyPayloadHash);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "R2 download", cancellationToken);

        return new StoredObjectDownloadDto
        {
            FileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(key) : fileName,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            Content = await response.Content.ReadAsByteArrayAsync(cancellationToken)
        };
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = CreateSignedRequest(HttpMethod.Delete, key, EmptyPayloadHash);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "R2 delete", cancellationToken);
    }

    private HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string key,
        string payloadHash,
        string? contentType = null)
    {
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var uri = BuildObjectUri(key);
        var host = uri.Host;

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = host,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = amzDate
        };

        if (!string.IsNullOrWhiteSpace(contentType))
            headers["content-type"] = contentType;

        var canonicalHeaders = string.Concat(headers.Select(h => $"{h.Key}:{h.Value.Trim()}\n"));
        var signedHeaders = string.Join(';', headers.Keys);
        var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
        var canonicalRequest = string.Join('\n', new[]
        {
            method.Method,
            uri.AbsolutePath,
            string.Empty,
            canonicalHeaders,
            signedHeaders,
            payloadHash
        });

        var stringToSign = string.Join('\n', new[]
        {
            Algorithm,
            amzDate,
            credentialScope,
            Hash(Encoding.UTF8.GetBytes(canonicalRequest))
        });

        var signingKey = GetSignatureKey(_settings.SecretAccessKey, dateStamp);
        var signature = ToHex(HmacSHA256(signingKey, stringToSign));

        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"{Algorithm} Credential={_settings.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);

        return request;
    }

    private Uri BuildObjectUri(string key)
    {
        var endpoint = ResolveEndpoint();
        var escapedBucket = Uri.EscapeDataString(_settings.BucketName.Trim());
        var escapedKey = string.Join(
            '/',
            key.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        var builder = new UriBuilder(endpoint)
        {
            Path = $"{escapedBucket}/{escapedKey}"
        };

        return builder.Uri;
    }

    private Uri ResolveEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(_settings.Endpoint))
            return new Uri(_settings.Endpoint.TrimEnd('/'));

        return new Uri($"https://{_settings.AccountId.Trim()}.r2.cloudflarestorage.com");
    }

    private string? BuildPublicUrl(string key)
    {
        if (string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            return null;

        var escapedKey = string.Join(
            '/',
            key.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{escapedKey}";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.BucketName) ||
            string.IsNullOrWhiteSpace(_settings.AccessKeyId) ||
            string.IsNullOrWhiteSpace(_settings.SecretAccessKey) ||
            (string.IsNullOrWhiteSpace(_settings.Endpoint) && string.IsNullOrWhiteSpace(_settings.AccountId)))
        {
            throw new InvalidOperationException("Cloudflare R2 storage is not configured.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var details = string.IsNullOrWhiteSpace(body)
            ? response.ReasonPhrase
            : body;

        throw new HttpRequestException(
            $"{operation} failed with status {(int)response.StatusCode}: {details}");
    }

    private static string NormalizeContentType(string? contentType)
    {
        return MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            ? parsed.MediaType ?? "application/octet-stream"
            : "application/octet-stream";
    }

    private static string Hash(byte[] bytes)
    {
        return ToHex(SHA256.HashData(bytes));
    }

    private static byte[] GetSignatureKey(string secretKey, string dateStamp)
    {
        var dateKey = HmacSHA256(Encoding.UTF8.GetBytes($"AWS4{secretKey}"), dateStamp);
        var dateRegionKey = HmacSHA256(dateKey, Region);
        var dateRegionServiceKey = HmacSHA256(dateRegionKey, Service);
        return HmacSHA256(dateRegionServiceKey, "aws4_request");
    }

    private static byte[] HmacSHA256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
