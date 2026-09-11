using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Infrastructure.Services;

public partial class TpayConnectionService : ITpayApiClient
{
    public async Task<TpayTransaction> CreateTransactionAsync(string accountKey, decimal amount, string description,
        string reference, string email, string name, string notificationUrl, string returnUrl, CancellationToken ct)
    {
        var token = await GetTokenAsync(accountKey, ct);
        var baseUrl = options.Value.UseSandbox ? "https://openapi.sandbox.tpay.com" : "https://api.tpay.com";
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/transactions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            amount, description, hiddenDescription = reference,
            payer = new { email, name },
            callbacks = new
            {
                notification = new { url = notificationUrl },
                payerUrls = new { success = returnUrl, error = returnUrl }
            }
        });
        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Tpay transaction creation failed.", null, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var data = document.RootElement;
        var id = data.GetProperty("transactionId").GetString();
        var title = data.GetProperty("title").GetString();
        var url = data.GetProperty("transactionPaymentUrl").GetString();
        var expectedHost = options.Value.UseSandbox ? "secure.sandbox.tpay.com" : "secure.tpay.com";
        if (data.GetProperty("result").GetString() != "success" || string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(title) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != expectedHost || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new HttpRequestException("Tpay returned an invalid transaction response.");
        return new(id, title, url!);
    }

    public async Task<bool> VerifySignatureAsync(byte[] body, string signature, CancellationToken ct)
    {
        try
        {
            var parts = signature.Split('.');
            if (parts.Length != 3 || parts[1].Length != 0 || signature.Length > 8192)
                return false;
            using var header = JsonDocument.Parse(DecodeBase64Url(parts[0]));
            var data = header.RootElement;
            if (data.GetProperty("alg").GetString() != "RS256" || data.TryGetProperty("crit", out _) ||
                !Uri.TryCreate(data.GetProperty("x5u").GetString(), UriKind.Absolute, out var url) ||
                url.Scheme != "https" || !url.IsDefaultPort || url.UserInfo.Length != 0 ||
                url.Query.Length != 0 || url.Fragment.Length != 0 ||
                url.AbsolutePath != "/x509/notifications-jws.pem" ||
                !(url.Host == "secure.tpay.com" || options.Value.UseSandbox && url.Host == "secure.sandbox.tpay.com"))
                return false;

            var certificatePem = await GetCertificatePemAsync(url.AbsoluteUri, ct);
            var rootPem = await GetCertificatePemAsync($"https://{url.Host}/x509/tpay-jws-root.pem", ct);
            using var certificate = X509Certificate2.CreateFromPem(certificatePem);
            using var root = X509Certificate2.CreateFromPem(rootPem);
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            if (!chain.Build(certificate))
                return false;
            using var rsa = certificate.GetRSAPublicKey();
            var payload = Convert.ToBase64String(body).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return rsa is not null && rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + payload),
                DecodeBase64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<string> GetCertificatePemAsync(string url, CancellationToken ct)
    {
        if (cache.TryGetValue<string>(url, out var pem) && pem is not null)
            return pem;
        pem = await httpClient.GetStringAsync(url, ct);
        cache.Set(url, pem, TimeSpan.FromMinutes(15));
        return pem;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }
}
