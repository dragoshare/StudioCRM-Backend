using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class OutlookGroupClassTests
{
    // An Outlook event with the "Zajęcia grupowe" category becomes a group class: type Group, only clients with
    // group access at the location, group packages pre-assigned and no 1:4 limit.
    [PostgresFact]
    public async Task GroupCategoryCreatesGroupClassWithGroupAccessClientsAndGroupPackages()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Kłaj", City = "Test", CalendarEmail = "klaj@test.invalid" };
        var trainer = new Trainer { User = new User { Email = "group-trainer@test.invalid", FirstName = "G", LastName = "Trener" } };
        var integration = new CalendarIntegration { User = trainer.User, Provider = "Outlook", IsActive = true };
        Client Member(string name) => new() { FirstName = name, LastName = "G", Email = $"{name}@test.invalid", Location = location };
        var members = Enumerable.Range(1, 5).Select(i => Member($"member{i}")).ToList();
        var outsider = Member("outsider");
        db.AddRange(location, trainer, integration, outsider, new TrainerLocation { Trainer = trainer, Location = location });
        db.AddRange(members);
        db.AddRange(members.Select(m => new ClientLocationMembership { Client = m, Location = location, GroupAccessEnabled = true }));
        var personal = new ClientPackage { Client = members[0], Package = new Package { Name = "Personal" }, Name = "Personal", TotalSessions = 8, PurchaseDate = DateTime.UtcNow.AddDays(-1), ExpectedBillingType = SessionBillingType.OneToOne };
        var groupPackage = new ClientPackage { Client = members[0], Package = new Package { Name = "Group", BillingType = SessionBillingType.Group }, Name = "Group", TotalSessions = 4, PurchaseDate = DateTime.UtcNow.AddDays(-5), ExpectedBillingType = SessionBillingType.Group, PaymentStatus = PaymentStatus.Paid };
        db.AddRange(personal, groupPackage);
        await db.SaveChangesAsync();

        var start = DateTime.UtcNow.AddDays(2);
        var evt = new ExternalCalendarEvent
        {
            CalendarIntegrationId = integration.Id, ExternalEventId = "group-1", Subject = "Zajęcia grupowe – TRX",
            StartAt = start, EndAt = start.AddHours(1), LocationEmail = location.CalendarEmail, OrganizerEmail = trainer.User.Email,
            CategoriesJson = JsonSerializer.Serialize(new[] { "Zajęcia grupowe" }),
            AttendeesJson = JsonSerializer.Serialize(members.Select(m => m.Email).Append(outsider.Email))
        };
        db.Add(evt);
        await db.SaveChangesAsync();

        var (session, warnings) = await new OutlookEventMapperService(db).MapToSessionAsync(evt);

        Assert.NotNull(session);
        Assert.Equal(("Group", "Zajęcia grupowe – TRX"), (session!.PlannedSessionType, session.Title));
        var participants = await db.SessionParticipants.Where(p => p.SessionId == session.Id).ToListAsync();
        Assert.Equal(5, participants.Count);
        Assert.Equal(groupPackage.Id, participants.Single(p => p.ClientId == members[0].Id).ClientPackageId);
        Assert.Contains(warnings, w => w.Contains(outsider.Email));
    }
}
