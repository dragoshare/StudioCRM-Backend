using System.Net;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class TpayConnectionServiceTests
{
    [Fact]
    public async Task SandboxUsesOnlySelectedAccountAndEncodesCredentials()
    {
        using var handler = new Handler(async request =>
        {
            Assert.Equal("https://openapi.sandbox.tpay.com/oauth/auth", request.RequestUri!.AbsoluteUri);
            Assert.Equal("client_id=first&client_secret=a%26b", await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"private-token\"}")
            };
        });
        using var client = new HttpClient(handler);
        var settings = new TpaySettings();
        settings.Accounts["Studio1"] = new() { ClientId = "first", ClientSecret = "a&b" };
        settings.Accounts["Studio2"] = new() { ClientId = "second", ClientSecret = "other" };
        var service = new TpayConnectionService(client, Options.Create(settings));
        Assert.True(await service.TestConnectionAsync("Studio1", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TestConnectionAsync("missing", default));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "secret-provider-error")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task RejectsFailedAuthenticationWithoutExposingResponse(HttpStatusCode status, string body)
    {
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body)
        }));
        using var client = new HttpClient(handler);
        var settings = new TpaySettings();
        settings.Accounts["Studio1"] = new() { ClientId = "id", ClientSecret = "secret" };
        var service = new TpayConnectionService(client, Options.Create(settings));
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.TestConnectionAsync("Studio1", default));
        Assert.DoesNotContain(body, exception.Message);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request);
    }
}
