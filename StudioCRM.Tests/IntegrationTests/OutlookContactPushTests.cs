using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class OutlookContactPushTests
{
    private static OutlookSettings Settings => new() { CalendarEmailDomain = "calendar.example.test", UseCalendarEmails = true };

    [PostgresFact]
    public async Task NewClientIsPushedToOwnersOwnTrainerAndLocationTrainersOnly()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context(outlookSettings: Settings);
        var studio = new Location { Name = "Studio" };
        var elsewhere = new Location { Name = "Elsewhere" };
        var ownerRole = new Role { Name = "Owner" };
        Trainer Trainer(string email, Location location)
        {
            var trainer = new Trainer { User = new User { Email = email } };
            db.Add(new TrainerLocation { Trainer = trainer, Location = location });
            return trainer;
        }
        var own = Trainer("own@test.invalid", studio);
        var substitute = Trainer("sub@test.invalid", studio);
        var far = Trainer("far@test.invalid", elsewhere);
        var owner = new User { Email = "owner@test.invalid" };
        var brokenOwner = new User { Email = "broken@test.invalid" };
        db.AddRange(new UserRole { User = owner, Role = ownerRole }, new UserRole { User = brokenOwner, Role = ownerRole });
        CalendarIntegration Outlook(User user, string token) => new() { User = user, AccessToken = token, ExternalUserId = token, Email = user.Email };
        db.AddRange(Outlook(owner, "owner"), Outlook(brokenOwner, "broken"), Outlook(own.User, "own"),
            Outlook(substitute.User, "sub"), Outlook(far.User, "far"));
        var client = new Client { FirstName = "Anna", LastName = "Nowak", Email = "anna@example.test", PhoneNumber = "500600700",
            Trainer = own, Location = studio };
        db.Add(client);
        await db.SaveChangesAsync();

        var graph = new GraphRecorder();
        var service = new OutlookContactService(db, null!, new NoRefresh(), new HttpClient(graph), Options.Create(Settings));
        await service.SyncClientToConnectedOutlooksAsync(client.Id);

        Assert.Contains("500600700", graph.Created["owner"]);
        Assert.Contains("500600700", graph.Created["own"]);
        Assert.Contains(client.CalendarEmail!, graph.Created["sub"]);
        Assert.DoesNotContain("500600700", graph.Created["sub"]);
        Assert.False(graph.Created.ContainsKey("far"));
        Assert.False(graph.Created.ContainsKey("broken"));
    }

    private sealed class NoRefresh : IOutlookTokenService
    {
        public Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false) => Task.CompletedTask;
    }

    private sealed class GraphRecorder : HttpMessageHandler
    {
        public Dictionary<string, string> Created { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization!.Parameter!;
            if (token == "broken")
                return new(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") };
            if (request.Method == HttpMethod.Get)
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json") };
            Created[token] = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.Created) { Content = new StringContent("{}") };
        }
    }
}
