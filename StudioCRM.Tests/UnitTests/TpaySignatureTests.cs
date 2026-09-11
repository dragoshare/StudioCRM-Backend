using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class TpaySignatureTests
{
    [Fact]
    public async Task AcceptsTrustedSignatureAndRejectsModifiedPayloadAndUntrustedHosts()
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Tpay test root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Tpay signing", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.Create(root, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), new byte[] { 1 });
        using var handler = new CertificateHandler(certificate.ExportCertificatePem(), root.ExportCertificatePem());
        using var http = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new TpayConnectionService(http, Options.Create(new TpaySettings()), cache);
        var body = Encoding.UTF8.GetBytes("tr_id=TR-test&tr_amount=100.00");
        var header = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"x5u\":\"https://secure.tpay.com/x509/notifications-jws.pem\"}"));
        var signature = key.SignData(Encoding.ASCII.GetBytes(header + "." + Encode(body)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(await service.VerifySignatureAsync(body, header + ".." + Encode(signature), default));
        Assert.False(await service.VerifySignatureAsync(Encoding.UTF8.GetBytes("tampered"), header + ".." + Encode(signature), default));
        var maliciousHeader = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"x5u\":\"https://secure.tpay.com.evil.test/x509/notifications-jws.pem\"}"));
        Assert.False(await service.VerifySignatureAsync(body, maliciousHeader + ".." + Encode(signature), default));
        Assert.Equal(2, handler.Requests);
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class CertificateHandler(string certificate, string root) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("tpay-jws-root.pem") ? root : certificate)
            });
        }
    }
}
