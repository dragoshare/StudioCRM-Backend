using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Api.BackgroundServices;
using StudioCRM.Api.Controllers;
using StudioCRM.Api.Security;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.Interfaces.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookReconcileJobsTests
{
    private static readonly Guid Org = Guid.NewGuid();

    [Fact]
    public void SecondClickWhileRunningReusesTheJobOfTheSameUser()
    {
        var jobs = new OutlookReconcileJobs();
        var first = jobs.Enqueue(Org, new(1, true, false), new());
        Assert.Same(first, jobs.Enqueue(Org, new(1, true, false), new() { OnlyMine = true }));
        Assert.NotSame(first, jobs.Enqueue(Org, new(2, false, true), new()));
        Assert.NotSame(first, jobs.Enqueue(Guid.NewGuid(), new(1, true, false), new()));
        Assert.Same(first, jobs.Find(first.Id));
    }

    [Fact]
    public async Task WorkerRunsInTheRequestOrganizationForTheCapturedUserAndClassifiesErrors()
    {
        var fake = new FakeReconcile();
        var (jobs, worker) = Start(fake);
        try
        {
            var done = jobs.Enqueue(Org, new(7, true, false), new() { TrainerId = 3 });
            await done.Done.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(OutlookReconcileJobState.Completed, done.State);
            Assert.Equal((Org, 7, 3), (fake.Organization, fake.Actor!.UserId, fake.Request!.TrainerId));

            fake.Throw = new InvalidOperationException("Trainer does not have active Outlook integration.");
            var rejected = jobs.Enqueue(Org, new(8, false, true), new());
            await rejected.Done.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((OutlookReconcileJobState.Failed, true), (rejected.State, rejected.IsUserError));

            fake.Throw = new HttpRequestException("socket");
            var crashed = jobs.Enqueue(Org, new(9, false, true), new());
            await crashed.Done.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((OutlookReconcileJobState.Failed, false), (crashed.State, crashed.IsUserError));
            Assert.DoesNotContain("socket", crashed.Error);
        }
        finally { await worker.StopAsync(default); }
    }

    [Fact]
    public async Task EndpointReturnsResultWhenFastAndAcceptedWithJobWhenSlow()
    {
        var fake = new FakeReconcile();
        var (jobs, worker) = Start(fake);
        try
        {
            var organization = new RequestOrganizationContext();
            organization.SetValidated(Org);
            var controller = new OutlookController(null!, null!, null!, null!, null!, null!) { ControllerContext = Owner(5) };

            var fast = Assert.IsType<OkObjectResult>(await controller.Reconcile(new(), jobs, organization));
            Assert.Same(fake.Result, fast.Value);

            fake.Gate = new TaskCompletionSource();
            var slow = Assert.IsType<AcceptedResult>(await controller.Reconcile(new() { OnlyMine = true }, jobs, organization));
            Assert.StartsWith("/api/outlook/reconcile/", slow.Location);
            fake.Gate.SetResult();
        }
        finally { await worker.StopAsync(default); }
    }

    private static (OutlookReconcileJobs, OutlookReconcileWorker) Start(FakeReconcile fake)
    {
        var services = new ServiceCollection();
        services.AddScoped<RequestOrganizationContext>();
        services.AddScoped<IOutlookWebhookService>(sp => fake.For(sp.GetRequiredService<RequestOrganizationContext>()));
        var jobs = new OutlookReconcileJobs();
        var worker = new OutlookReconcileWorker(jobs, services.BuildServiceProvider(), NullLogger<OutlookReconcileWorker>.Instance);
        worker.StartAsync(default).GetAwaiter().GetResult();
        return (jobs, worker);
    }

    private static ControllerContext Owner(int userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "Owner")], "test"))
        }
    };

    private sealed class FakeReconcile : IOutlookWebhookService
    {
        private RequestOrganizationContext? context;
        public OutlookReconciliationResultDto Result { get; } = new() { IntegrationsProcessed = 1 };
        public Exception? Throw { get; set; }
        public TaskCompletionSource? Gate { get; set; }
        public Guid Organization { get; private set; }
        public OutlookReconcileActor? Actor { get; private set; }
        public OutlookReconciliationRequestDto? Request { get; private set; }

        public FakeReconcile For(RequestOrganizationContext organization) { context = organization; return this; }

        public async Task<OutlookReconciliationResultDto> ReconcileAsync(OutlookReconciliationRequestDto request, OutlookReconcileActor actor)
        {
            (Organization, Actor, Request) = (context!.OrganizationId, actor, request);
            if (Gate != null) await Gate.Task;
            if (Throw != null) throw Throw;
            return Result;
        }

        public Task<OutlookReconciliationResultDto> ReconcileAsync(OutlookReconciliationRequestDto request) => throw new NotSupportedException();
        public Task HandleNotificationAsync(string requestBody) => throw new NotSupportedException();
    }
}
