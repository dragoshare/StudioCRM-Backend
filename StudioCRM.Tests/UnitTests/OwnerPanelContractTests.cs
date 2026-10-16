using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Api.Controllers;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Tests.UnitTests;

public class OwnerPanelContractTests
{
    [Theory]
    [InlineData(null, null, "paymentDateDesc")]
    [InlineData("amount", null, "amount")]
    [InlineData("amount", "desc", "amountDesc")]
    [InlineData("amountDesc", "asc", "amount")]
    [InlineData(null, "asc", "paymentDate")]
    public void PaymentOrderCombinesWithSort(string? sort, string? order, string expected) =>
        Assert.Equal(expected, new ClientPaymentFilterDto { Sort = sort, Order = order }.ResolveSort());

    [Fact]
    public void PaymentFilterRejectsUnknownOrderAndLocationMatch()
    {
        Assert.Throws<ValidationException>(new ClientPaymentFilterDto { Order = "up" }.Validate);
        Assert.Throws<ValidationException>(new ClientPaymentFilterDto { LocationMatch = "client" }.Validate);
        new ClientPaymentFilterDto { LocationMatch = "effective", Order = "desc", Overpayment = true }.Validate();
    }

    [Fact]
    public void ClientPageDefaultsMatchLegacyListOrderAndValidateValues()
    {
        var filter = new ClientPageFilter();
        Assert.Equal(("createdDesc", 1, 25, false), (filter.ResolveSort(), filter.Page, filter.PageSize, filter.IsArchived));
        Assert.Equal("nameDesc", new ClientPageFilter { Sort = "name", Order = "desc" }.ResolveSort());
        Assert.Throws<ValidationException>(new ClientPageFilter { PageSize = 101 }.Validate);
        Assert.Throws<ValidationException>(new ClientPageFilter { PackageStatus = "expired" }.Validate);
        Assert.Throws<ValidationException>(new ClientPageFilter { Sort = "phone" }.Validate);
    }

    [Fact]
    public void SettlementAndFilterOptionQueriesValidateRanges()
    {
        new TrainerSettlementListFilter { Year = 2026, Month = 10 }.Validate();
        Assert.Throws<ValidationException>(new TrainerSettlementListFilter { Year = 2026, Month = 13 }.Validate);
        Assert.Throws<ValidationException>(new TrainerSettlementListFilter { Year = 2026, Month = 1, PageSize = 0 }.Validate);
        Assert.Throws<ValidationException>(new OwnerFilterOptionsQuery { Limit = 101 }.Validate);
    }

    [Theory]
    [InlineData("+48 600-100-200", "600100200")]
    [InlineData("0048600100200", "600100200")]
    [InlineData("600 100", "600100")]
    [InlineData("12 34 56 78 901", "12345678901")]
    [InlineData("Anna 12", null)]
    [InlineData("anna", null)]
    public void PhoneSearchComparesDigitsAndDropsPolishPrefix(string search, string? expected) =>
        Assert.Equal(expected, ClientPageFilter.NormalizePhoneSearch(search));

    [Theory]
    [InlineData("balance")] [InlineData("balanceDesc")] [InlineData("packageUsage")] [InlineData("packageUsageDesc")]
    [InlineData("trainer")] [InlineData("trainerDesc")] [InlineData("archivedAt")] [InlineData("archivedAtDesc")]
    [InlineData("name")] [InlineData("nameDesc")] [InlineData("created")] [InlineData("createdDesc")]
    public void ClientPageSortsTranslateToOneStatementWithIdTieBreak(string sort)
    {
        new ClientPageFilter { Sort = sort }.Validate();
        using var db = TranslationContext();
        var sql = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            StudioCRM.Infrastructure.Services.ClientService.OrderClientPage(db, db.Clients, sort).Skip(25).Take(25));
        Assert.Matches(@"\.""Id""( DESC)?$", sql[..sql.LastIndexOf("LIMIT", StringComparison.Ordinal)].TrimEnd());
    }

    [Fact]
    public void PhoneSearchRunsInSqlAsDigitsOnlyComparison()
    {
        using var db = TranslationContext();
        var sql = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            StudioCRM.Infrastructure.Services.ClientService.WhereMatchesSearch(db.Clients, "+48 600 100 200"));
        Assert.Contains("replace(", sql);
        Assert.Contains("600100200", sql);
    }

    [Fact]
    public void FilterOptionsGroupAccessRequiresLocation() =>
        Assert.Throws<ValidationException>(new OwnerFilterOptionsQuery { GroupAccess = true }.Validate);

    private static StudioCRM.Infrastructure.Persistence.StudioCRMDbContext TranslationContext() => new(
        new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<StudioCRM.Infrastructure.Persistence.StudioCRMDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").Options, null, null, null);

    [Fact]
    public void SessionFilterIsPagedOnlyWhenPagingIsSent()
    {
        Assert.False(new SessionFilterDto { From = DateTime.UtcNow }.IsPaged);
        Assert.True(new SessionFilterDto { PageSize = 10 }.IsPaged);
    }

    [Fact]
    public async Task ClientsListKeepsLegacyArrayWithoutPagingParameters()
    {
        var clients = Fake<IClientService>.Create((method, _) => method.Name switch
        {
            nameof(IClientService.GetAllAsync) => Task.FromResult(new List<ClientDto>()),
            nameof(IClientService.GetPageAsync) => Task.FromResult(new ClientPageDto()),
            _ => throw new NotSupportedException(method.Name)
        });
        var controller = new ClientsController(clients, null!, null!, null!) { ControllerContext = Context("") };
        Assert.IsType<List<ClientDto>>(Assert.IsType<OkObjectResult>(await controller.GetAll(new ClientPageFilter())).Value);
        controller.ControllerContext = Context("?page=2");
        Assert.IsType<ClientPageDto>(Assert.IsType<OkObjectResult>(await controller.GetAll(new ClientPageFilter { Page = 2 })).Value);
    }

    [Fact]
    public async Task StaffPaymentTakesIdempotencyKeyFromHeaderAndRejectsMismatch()
    {
        CreateClientPaymentRequest? received = null;
        var payments = Fake<IClientPaymentService>.Create((method, args) =>
        {
            received = (CreateClientPaymentRequest)args![0]!;
            return Task.FromResult(new ClientPaymentDto { ClientId = 1 });
        });
        var controller = new BillingController(payments, null!);
        var key = Guid.NewGuid();
        await controller.CreateStaffPayment(new CreateClientPaymentRequest { ClientId = 1, Amount = 10, Method = PaymentMethod.Cash }, key.ToString());
        Assert.Equal(key, received!.IdempotencyKey);

        received = null;
        var mismatch = await controller.CreateStaffPayment(new CreateClientPaymentRequest { IdempotencyKey = Guid.NewGuid() }, key.ToString());
        Assert.IsType<BadRequestObjectResult>(mismatch.Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CreateStaffPayment(new CreateClientPaymentRequest(), "not-a-uuid")).Result);
        Assert.Null(received);
    }

    [Fact]
    public async Task OperationLookupReturnsNotFoundWithStateAndRejectsInvalidKey()
    {
        var payments = Fake<IClientPaymentService>.Create((_, _) => Task.FromResult<PaymentOperationStatusDto?>(null));
        var controller = new OwnerPanelController(null!, null!, payments);
        Assert.IsType<BadRequestObjectResult>(await controller.Operation("abc"));
        Assert.IsType<NotFoundObjectResult>(await controller.Operation(Guid.NewGuid().ToString()));
    }

    private static ControllerContext Context(string query) =>
        new() { HttpContext = new DefaultHttpContext { Request = { QueryString = new QueryString(query) } } };

    public class Fake<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object?[]?, object?> handler = null!;
        public static T Create(Func<MethodInfo, object?[]?, object?> handler)
        {
            var proxy = DispatchProxy.Create<T, Fake<T>>();
            ((Fake<T>)(object)proxy).handler = handler;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => handler(targetMethod!, args);
    }
}
