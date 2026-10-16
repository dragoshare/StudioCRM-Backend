using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using StudioCRM.Api.Controllers;
using StudioCRM.Api.Swagger;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Domain.Enums;
using Swashbuckle.AspNetCore.Swagger;

namespace StudioCRM.Tests.UnitTests;

public class ListFilterContractTests
{
    [Theory]
    [InlineData("name")] [InlineData("nameDesc")] [InlineData("created")] [InlineData("createdDesc")]
    [InlineData("balance")] [InlineData("balanceDesc")] [InlineData("packageUsage")] [InlineData("packageUsageDesc")]
    public void PortalClientSortAcceptsDocumentedValues(string sort) =>
        new PortalPageFilter { Sort = sort, HasActivePackage = false }.Validate();

    [Theory]
    [InlineData("trainer")] [InlineData("Name")] [InlineData("balanceAsc")]
    public void PortalClientSortRejectsUnknownValues(string sort) =>
        Assert.Throws<ValidationException>(() => new PortalPageFilter { Sort = sort }.Validate());

    [Fact]
    public void PortalClientDefaultsStayUnchanged()
    {
        var filter = new PortalPageFilter();
        Assert.Equal(("name", 1, 25), (filter.Sort, filter.Page, filter.PageSize));
        Assert.Null(filter.HasActivePackage);
        Assert.Throws<ValidationException>(() => new PortalPageFilter { PageSize = 101 }.Validate());
    }

    [Theory]
    [InlineData(null)] [InlineData("paymentDateDesc")] [InlineData("paymentDate")] [InlineData("amount")] [InlineData("amountDesc")]
    public void PaymentSortAcceptsDocumentedValues(string? sort) =>
        new ClientPaymentFilterDto { Sort = sort, Method = PaymentMethod.Cash, AmountMin = 0, AmountMax = 0 }.Validate();

    public static TheoryData<ClientPaymentFilterDto> InvalidPaymentFilters => new()
    {
        new ClientPaymentFilterDto { Sort = "createdAt" },
        new ClientPaymentFilterDto { AmountMin = 200, AmountMax = 100 },
        new ClientPaymentFilterDto { AmountMin = -1 },
        new ClientPaymentFilterDto { AmountMax = -0.01m },
        new ClientPaymentFilterDto { From = new DateTime(2026, 10, 2), To = new DateTime(2026, 10, 1) },
        new ClientPaymentFilterDto { Method = (PaymentMethod)99 },
        new ClientPaymentFilterDto { Search = new string('x', 101) }
    };

    [Theory]
    [MemberData(nameof(InvalidPaymentFilters))]
    public void PaymentFilterRejectsInvalidRanges(ClientPaymentFilterDto filter) =>
        Assert.Throws<ValidationException>(filter.Validate);

    [Fact]
    public void PaymentFilterKeepsClampingPagingAndAcceptsOpenRanges() =>
        new ClientPaymentFilterDto { Page = 0, PageSize = 1000, AmountMin = 100, From = new DateTime(2026, 10, 1) }.Validate();

    [Fact]
    public void SwaggerDescribesEnumNumbersAndListParameters()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(BillingController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "test", Version = "v1" });
            c.SchemaFilter<EnumDescriptionSchemaFilter>();
            c.OperationFilter<ListEndpointsOperationFilter>();
        });
        using var app = builder.Build();
        var doc = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        var method = doc.Components.Schemas[nameof(PaymentMethod)];
        Assert.Equal("1 = Blik, 2 = BankTransfer, 3 = Cash, 4 = PaymentGateway", method.Description);
        Assert.Equal("integer", method.Type);
        Assert.Contains("5 = Reversed", doc.Components.Schemas[nameof(ClientPaymentStatus)].Description);
        var billingMode = doc.Components.Schemas[nameof(PackageBillingMode)];
        Assert.Equal("string", billingMode.Type);
        Assert.Equal(["SessionBased", "PeriodBased"], billingMode.Enum.Cast<OpenApiString>().Select(v => v.Value));

        var payments = doc.Paths["/api/billing/payments"].Operations[OperationType.Get];
        Assert.Contains("paymentDateDesc", payments.Description);
        var sort = payments.Parameters.Single(p => p.Name == "Sort");
        Assert.Equal(["paymentDateDesc", "paymentDate", "amountDesc", "amount", "clientName", "clientNameDesc", "overpayment", "overpaymentDesc"], sort.Schema.Enum.Cast<OpenApiString>().Select(v => v.Value));
        Assert.Contains("PaymentDate", payments.Parameters.Single(p => p.Name == "From").Description);
        Assert.Contains(payments.Parameters, p => p.Name == "Method");

        var clients = doc.Paths["/api/trainer-portal/clients/page"].Operations[OperationType.Get];
        Assert.Equal("name", ((OpenApiString)clients.Parameters.Single(p => p.Name == "Sort").Schema.Default).Value);
        Assert.Contains(clients.Parameters, p => p.Name == "HasActivePackage");
        Assert.Contains("New, Active, Inactive", clients.Parameters.Single(p => p.Name == "Status").Description);
    }
}

public class ListSortTranslationTests
{
    // ToQueryString needs no connection: it proves the sorts translate to a single SQL statement (no client eval, no N+1).
    private static StudioCRM.Infrastructure.Persistence.StudioCRMDbContext Context() => new(
        new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<StudioCRM.Infrastructure.Persistence.StudioCRMDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").Options, null, null, null);

    [Theory]
    [InlineData("name")] [InlineData("created")] [InlineData("createdDesc")]
    [InlineData("balance")] [InlineData("balanceDesc")] [InlineData("packageUsage")] [InlineData("packageUsageDesc")]
    public void PortalClientSortsTranslateToSql(string sort)
    {
        using var db = Context();
        var query = StudioCRM.Infrastructure.Services.TrainerPortalService.WhereHasActivePackage(db, db.Clients, true);
        var sql = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            StudioCRM.Infrastructure.Services.TrainerPortalService.OrderPortalClients(db, query, sort).Skip(25).Take(25));
        Assert.Contains("ORDER BY", sql);
        Assert.Matches(@"\.""Id""( DESC)?$", sql[..sql.LastIndexOf("LIMIT", StringComparison.Ordinal)].TrimEnd());
    }

    [Theory]
    [InlineData(null)] [InlineData("paymentDate")] [InlineData("amount")] [InlineData("amountDesc")]
    [InlineData("clientName")] [InlineData("clientNameDesc")] [InlineData("overpayment")] [InlineData("overpaymentDesc")]
    public void PaymentSortsUsePaymentDateAndIdTieBreak(string? sort)
    {
        using var db = Context();
        var sql = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            StudioCRM.Infrastructure.Services.ClientPaymentService.OrderPayments(db.ClientPayments, sort));
        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        Assert.Contains("\"PaymentDate\"", orderBy);
        Assert.DoesNotContain("\"CreatedAt\"", orderBy);
        Assert.Contains("\"Id\"", orderBy);
    }
}
