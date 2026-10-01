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

    [Theory]
    [InlineData("intermediate-first", true)]
    [InlineData("root-first", true)]
    [InlineData("missing-root", false)]
    [InlineData("wrong-root", false)]
    [InlineData("expired-leaf", false)]
    public async Task ValidatesCompleteCaBundleWithoutTrustingAnIncompleteOrInvalidChain(string scenario, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var uniqueName = Guid.NewGuid().ToString("N");
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest($"CN=Root {uniqueName}", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(now.AddDays(-3), now.AddDays(3));

        using var intermediateKey = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest($"CN=Intermediate {uniqueName}", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var intermediatePublic = intermediateRequest.Create(root, now.AddDays(-2), now.AddDays(2), new byte[] { 2 });
        using var intermediate = intermediatePublic.CopyWithPrivateKey(intermediateKey);

        using var signingKey = RSA.Create(2048);
        var signingRequest = new CertificateRequest($"CN=Signing {uniqueName}", signingKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        signingRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        signingRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var certificate = signingRequest.Create(intermediate, now.AddDays(-1),
            scenario == "expired-leaf" ? now.AddHours(-1) : now.AddDays(1), new byte[] { 3 });

        using var otherRootKey = RSA.Create(2048);
        // Same subject, different key: name matching alone must not establish trust.
        var otherRootRequest = new CertificateRequest(root.SubjectName, otherRootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        otherRootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        otherRootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var otherRoot = otherRootRequest.CreateSelfSigned(now.AddDays(-3), now.AddDays(3));
        var bundle = scenario switch
        {
            "root-first" => root.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem(),
            "missing-root" => intermediate.ExportCertificatePem(),
            "wrong-root" => intermediate.ExportCertificatePem() + "\n" + otherRoot.ExportCertificatePem(),
            _ => intermediate.ExportCertificatePem() + "\n" + root.ExportCertificatePem()
        };
        using var handler = new CertificateHandler(certificate.ExportCertificatePem(), bundle);
        using var http = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new TpayConnectionService(http, Options.Create(new TpaySettings { UseSandbox = true }), cache);
        var body = Encoding.UTF8.GetBytes("tr_id=TR-test&tr_amount=1.00");
        var header = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"x5u\":\"https://secure.sandbox.tpay.com/x509/notifications-jws.pem\"}"));
        var signature = header + ".." + Encode(signingKey.SignData(
            Encoding.ASCII.GetBytes(header + "." + Encode(body)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.Equal(expected, await service.VerifySignatureAsync(body, signature, default));
        Assert.False(await service.VerifySignatureAsync(Encoding.UTF8.GetBytes("tampered"), signature, default));
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
