using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StudioCRM.Api.Controllers;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Tests.IntegrationTests;

public class TpayNotificationHttpTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MvcPassesOriginalFormBytesToNotificationVerifier(bool validSignature)
    {
        // Exercise real MVC model binding, which direct controller calls bypass.
        var body = Encoding.UTF8.GetBytes("tr_id=TR-test&tr_amount=1.00&tr_desc=Niepo%C5%82omice+%2B+test&tr_crc=crm-payment-1");
        var payments = new CapturingPaymentService(body);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers().AddApplicationPart(typeof(TpayPaymentsController).Assembly);
        builder.Services.AddSingleton<ITpayPaymentService>(payments);
        await using var app = builder.Build();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            using var http = new HttpClient { BaseAddress = new Uri(addresses.Addresses.Single()) };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/tpay/notifications");
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            var signature = validSignature ? "test-header..test-signature" : "test-header..invalid-signature";
            request.Headers.Add("X-JWS-Signature", signature);
            using var response = await http.SendAsync(request);

            Assert.Equal(body, payments.ReceivedBody);
            Assert.Equal(signature, payments.ReceivedSignature);
            Assert.Equal(validSignature ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
            if (validSignature)
                Assert.Equal("TRUE", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class CapturingPaymentService(byte[] expectedBody) : ITpayPaymentService
    {
        public byte[]? ReceivedBody { get; private set; }
        public string? ReceivedSignature { get; private set; }

        public Task HandleTpayNotificationAsync(byte[] body, string signature, CancellationToken ct)
        {
            ReceivedBody = body;
            ReceivedSignature = signature;
            if (!body.SequenceEqual(expectedBody) || signature != "test-header..test-signature")
                throw new UnauthorizedAccessException("Invalid Tpay signature.");
            return Task.CompletedTask;
        }

        public Task<ClientPaymentDto> CreateTpayCheckoutAsync(int clientPackageId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClientPaymentDto> GetTpayPaymentAsync(int paymentId, CancellationToken ct) => throw new NotSupportedException();
    }
}
