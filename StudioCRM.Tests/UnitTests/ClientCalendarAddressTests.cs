using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class ClientCalendarAddressTests
{
    private static OutlookSettings Settings => new() { CalendarEmailDomain = "calendar.example.test", UseCalendarEmails = true };

    [Fact]
    public void AddressSurvivesPersonalDetailsAndDomainChanges()
    {
        var client = new Client();
        Assert.True(ClientCalendarAddress.Ensure(client, "calendar.example.test"));
        var address = client.CalendarEmail;
        client.Email = "real@example.test";
        client.FirstName = "Anna";
        client.UserId = 42;
        Assert.False(ClientCalendarAddress.Ensure(client, "other.example.test"));
        Assert.Equal(address, client.CalendarEmail);
        Assert.Equal(address, ClientCalendarAddress.Recipient(client, Settings));
        var other = new Client();
        ClientCalendarAddress.Ensure(other, "calendar.example.test");
        Assert.NotEqual(address, other.CalendarEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.test")]
    [InlineData("a@example.test")]
    [InlineData("example.test/path")]
    public void InvalidDomainCannotGenerateAddress(string domain) =>
        Assert.Throws<InvalidOperationException>(() => ClientCalendarAddress.Ensure(new Client(), domain));

    [Fact]
    public void EnabledModeNeverFallsBackToRealEmail()
    {
        var client = new Client { Email = "real@example.test" };
        Assert.Throws<InvalidOperationException>(() => ClientCalendarAddress.Recipient(client, Settings));
        Assert.Equal(client.Email, ClientCalendarAddress.Recipient(client, new OutlookSettings()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavingOfflineClientGeneratesAddressWithoutCreatingAccount(bool asyncSave)
    {
        using var db = Context();
        var client = new Client { FirstName = "Anna", LastName = "Nowak" };
        db.Clients.Add(client);
        if (asyncSave) await db.SaveChangesAsync(); else db.SaveChanges();
        Assert.EndsWith("@calendar.example.test", client.CalendarEmail);
        Assert.Equal("", client.Email);
        Assert.Null(client.UserId);
    }

    [Fact]
    public async Task TechnicalAddressCannotBecomeLoginEmail()
    {
        using var db = Context();
        db.Users.Add(new User { Email = "klient-123@calendar.example.test" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void OutgoingAttendeesUseAliasesIncludingOfflineClientsAndKeepResource()
    {
        using var db = Context();
        var service = new OutlookCalendarSyncService(db, Options.Create(Settings), new HttpClient(), NullLogger<OutlookCalendarSyncService>.Instance);
        var offline = new Client { FirstName = "Anna", CalendarEmail = "klient-a@calendar.example.test" };
        var registered = new Client { FirstName = "Jan", Email = "real@example.test", CalendarEmail = "klient-b@calendar.example.test" };
        var session = new Session { Location = new Location { Name = "Studio", CalendarEmail = "room@example.test" },
            Participants = new List<SessionParticipant> { new() { Client = offline }, new() { Client = registered } } };
        var attendees = typeof(OutlookCalendarSyncService).GetMethod("BuildAttendees", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, new object[] { session });
        var json = JsonSerializer.Serialize(attendees);
        Assert.Contains(offline.CalendarEmail, json);
        Assert.Contains(registered.CalendarEmail, json);
        Assert.Contains("room@example.test", json);
        Assert.DoesNotContain("real@example.test", json);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContactSyncUsesTechnicalAddressAndPreservesName(bool existing)
    {
        using var db = Context();
        var handler = new GraphHandler(existing ? "{\"value\":[{\"id\":\"contact-1\"}]}" : "{\"value\":[]}");
        var service = new OutlookContactService(db, null!, null!, new HttpClient(handler), Options.Create(Settings));
        var client = new Client { FirstName = "Anna", LastName = "Nowak", CalendarEmail = "klient-a@calendar.example.test" };
        await Upsert(service, client);
        Assert.Equal(existing ? HttpMethod.Patch : HttpMethod.Post, handler.WriteMethod);
        Assert.Contains("Anna", handler.Payload);
        Assert.Contains(client.CalendarEmail, handler.Payload);
    }

    [Fact]
    public async Task FailedContactLookupDoesNotCreateDuplicate()
    {
        using var db = Context();
        var handler = new GraphHandler("{}", HttpStatusCode.TooManyRequests);
        var service = new OutlookContactService(db, null!, null!, new HttpClient(handler), Options.Create(Settings));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Upsert(service, new Client { CalendarEmail = "klient-a@calendar.example.test" }));
        Assert.Null(handler.WriteMethod);
    }

    private static Task Upsert(OutlookContactService service, Client client) => (Task)typeof(OutlookContactService)
        .GetMethod("UpsertOutlookContactAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service, new object[] { "test-token", client })!;

    private static StudioCRMDbContext Context() => new(new DbContextOptionsBuilder<StudioCRMDbContext>()
        .UseNpgsql("Host=localhost;Database=never_connected;Username=test")
        .AddInterceptors(new SuppressWrites()).Options, Options.Create(Settings));

    private sealed class SuppressWrites : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => InterceptionResult<int>.SuppressWithResult(0);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }

    private sealed class GraphHandler(string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpMethod? WriteMethod { get; private set; }
        public string Payload { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return new(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
            WriteMethod = request.Method;
            Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
