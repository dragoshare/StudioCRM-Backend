using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StudioCRM.Api.Controllers;
using StudioCRM.Api.Security;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class PortalHttpBenchmarkTests
{
    // Explicitly opt in: the functional suite must not depend on machine-specific timing.
    [PostgresFact]
    public async Task MeasureRealMvcEndpointsWithJwtAndIsolatedSyntheticData()
    {
        var output = Environment.GetEnvironmentVariable("ATLAS_BENCHMARK_OUTPUT");
        if (string.IsNullOrEmpty(output)) return;
        await using var database = await TestDatabase.CreateAsync();
        await using var seed = database.Context();
        var trainer = new Trainer { User = new User { Email = "benchmark@test.invalid", IsActive = true } };
        var location = new Location { Name = "Benchmark", City = "Test" };
        seed.Add(new StudioCRM.Domain.Entities.UserRole { User = trainer.User, Role = new Role { Name = "Trainer" } });
        seed.Add(new TrainerLocation { Trainer = trainer, Location = location });
        var clients = Enumerable.Range(0, 500).Select(i => new Client { FirstName = "Client", LastName = i.ToString("D4"), Email = $"client{i}@test.invalid", Trainer = trainer, Location = location, Status = "Active" }).ToArray();
        seed.Clients.AddRange(clients);
        var package = new Package { Name = "Package", Location = location, Price = 400, SessionsLimit = 4 };
        var now = DateTime.UtcNow.Date;
        for (var i = 0; i < 365; i++)
        {
            var at = now.AddDays(i - 300).AddHours(10);
            seed.Add(new SessionParticipant { Client = clients[0], Session = new Session { Trainer = trainer, Location = location,
                Title = "Training", StartAt = at, EndAt = at.AddHours(1), Status = i < 300 ? "Completed" : "Planned",
                ActualSessionType = i < 300 ? "OneToOne" : null, ActualParticipantsCount = i < 300 ? 1 : null } });
        }
        for (var i = 0; i < 20; i++) seed.Add(new ClientPackage { Client = clients[0], Package = package, Name = "Historical package", TotalSessions = 4,
            TotalPrice = 400, AmountPaid = i == 19 ? 100 : 400, IsActive = i == 19, PurchaseDate = now.AddDays(-400 + i * 20) });
        for (var i = 0; i < 100; i++) seed.Add(new ClientPayment { Client = clients[0], Amount = 40, PaymentDate = now.AddDays(-i),
            Status = i < 50 ? ClientPaymentStatus.PendingConfirmation : ClientPaymentStatus.Confirmed, Source = ClientPaymentSource.ClientRequest });
        seed.Add(new RefreshToken { User = trainer.User, Token = "benchmark-refresh", ExpiresAt = now.AddDays(2) });
        await seed.SaveChangesAsync();
        var token = (await PortalSecurityTests.Auth(seed).RefreshAsync(new() { RefreshToken = "benchmark-refresh" })).Token;
        var counter = new QueryCounter();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(s => s.Listen(IPAddress.Loopback, 0));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddScoped(sp => database.Context(sp.GetRequiredService<ICurrentUserService>(), interceptor: counter));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => AtlasJwt.Configure(o,
            new JwtSettings { Key = new string('k', 64), Issuer = "atlas-test", Audience = "atlas-test", ExpiryInMinutes = 15 }));
        builder.Services.AddControllers().AddApplicationPart(typeof(TrainerPortalController).Assembly).AddControllersAsServices();
        builder.Services.AddScoped<IClientPaymentService>(sp => PortalSecurityTests.Payments(sp.GetRequiredService<StudioCRMDbContext>(), sp.GetRequiredService<ICurrentUserService>()));
        builder.Services.AddScoped<IClientService>(sp => new ClientService(sp.GetRequiredService<StudioCRMDbContext>(), sp.GetRequiredService<ICurrentUserService>(), sp.GetRequiredService<IClientPaymentService>(), null!));
        builder.Services.AddScoped<ISessionService>(sp => PortalSecurityTests.Sessions(sp.GetRequiredService<StudioCRMDbContext>(), sp.GetRequiredService<ICurrentUserService>()));
        builder.Services.AddScoped<ITrainerSettlementService, TrainerSettlementService>();
        builder.Services.AddScoped<IOperationalAlertService, OperationalAlertService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<ITrainerPortalService, TrainerPortalService>();
        builder.Services.AddScoped<IPackageService, PackageService>();
        builder.Services.AddScoped(sp => new TrainerPortalController(sp.GetRequiredService<ITrainerPortalService>(), sp.GetRequiredService<IClientPaymentService>(), null!, null!, null!, null!, sp.GetRequiredService<ISessionService>(), null!));
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http = new HttpClient { BaseAddress = new Uri(address) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/trainer-portal/clients/page")).StatusCode);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/Trainers")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/trainer-portal/clients/page?pageSize=101")).StatusCode);
            var range = $"from={now:yyyy-MM-dd}T00:00:00Z&to={now.AddDays(7):yyyy-MM-dd}T00:00:00Z";
            var routes = new[] { "/api/trainer-portal/sessions", "/api/trainer-portal/sessions?" + range,
                "/api/sessions/filter?" + range, "/api/trainer-portal/clients", "/api/trainer-portal/clients/page?pageSize=25",
                $"/api/trainer-portal/clients/{clients[0].Id}/sessions/recent", $"/api/trainer-portal/clients/{clients[0].Id}/billing",
                $"/api/trainer-portal/clients/{clients[0].Id}/billing/summary", "/api/trainer-portal/payments/pending",
                "/api/trainer-portal/payments/pending/page?pageSize=25", "/api/Packages/options",
                "/api/trainer-portal/dashboard", "/api/trainer-portal/dashboard/summary",
                $"/api/trainer-portal/settlement?year={now.Year}&month={now.Month}",
                $"/api/trainer-portal/settlement/summary?year={now.Year}&month={now.Month}",
                "/api/Notifications/page?pageSize=25", "/api/Notifications/unread-count" };
            var rows = new List<object>();
            foreach (var route in routes)
            {
                for (var warm = 0; warm < 2; warm++) { using var response = await http.GetAsync(route); Assert.True(response.IsSuccessStatusCode, route + ": " + await response.Content.ReadAsStringAsync()); }
                var times = new List<double>(); var counts = new List<int>(); var bytes = 0;
                for (var i = 0; i < 20; i++)
                {
                    counter.Count = 0;
                    var clock = Stopwatch.StartNew();
                    using var response = await http.GetAsync(route);
                    var body = await response.Content.ReadAsByteArrayAsync(); clock.Stop();
                    Assert.True(response.IsSuccessStatusCode, route);
                    times.Add(clock.Elapsed.TotalMilliseconds); counts.Add(counter.Count); bytes = body.Length;
                }
                times.Sort();
                rows.Add(new { route, medianMs = Math.Round((times[9] + times[10]) / 2, 2), p95Ms = Math.Round(times[18], 2), bytes, sqlMin = counts.Min(), sqlMax = counts.Max() });
            }
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { environment = "Local Kestrel HTTP + real MVC/JWT/account validation + isolated PostgreSQL; no proxy/TLS/provider calls; EF pooling disabled by test fixture", samplesPerRoute = 20, clients = 500, sessions = 365, payments = 100, packageCycles = 20, results = rows }, new JsonSerializerOptions { WriteIndented = true }));
            await PortalSecurityTests.Auth(seed).LogoutAsync(trainer.UserId);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/trainer-portal/clients/page")).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    internal sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Count); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Count); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Count); return ValueTask.FromResult(result); }
    }
}
