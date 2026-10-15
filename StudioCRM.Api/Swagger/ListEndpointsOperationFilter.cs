using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.TrainerPortal;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StudioCRM.Api.Swagger;

// Documents filters, defaults and paging of client/payment lists. Contract: docs/FRONT-listy-klientow-i-wplat.md.
public class ListEndpointsOperationFilter : IOperationFilter
{
    private const string Paging = "Paginacja: Page 1..100000 (domyślnie 1), PageSize 1..100 (domyślnie 25). ";

    private static readonly Dictionary<string, string> ClientParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Page"] = "Numer strony, 1..100000. Domyślnie 1. Poza zakresem: 400.",
        ["PageSize"] = "Rozmiar strony, 1..100. Domyślnie 25. Poza zakresem: 400.",
        ["Search"] = "Fragment imienia i nazwiska lub emaila (bez rozróżniania wielkości liter), max 100 znaków.",
        ["LocationId"] = "Główna lokalizacja klienta (Client.LocationId).",
        ["Status"] = "Status klienta, dokładne dopasowanie: New, Active, Inactive. Archiwalni (usunięci) klienci nie są zwracani.",
        ["HasActivePackage"] = "true: klient ma co najmniej jeden aktywny, niezamknięty pakiet (niepuste ActivePackages); false: nie ma żadnego. Brak parametru: wszyscy.",
        ["Sort"] = "Dozwolone: " + PortalPageFilter.SortValues.Replace("|", ", ") +
            ". Domyślnie name (nazwisko, imię, Id). balance = CurrentBalance; packageUsage = ActivePackageUsedSessions/ActivePackageTotalSessions, klienci bez pakietu osobistego/semi na końcu. Dogrywka zawsze po Id."
    };

    private static readonly Dictionary<string, string> PaymentParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Page"] = "Numer strony. Domyślnie 1; wartości spoza 1..100000 są przycinane.",
        ["PageSize"] = "Rozmiar strony. Domyślnie 25; wartości spoza 1..100 są przycinane.",
        ["Search"] = "Fragment imienia i nazwiska lub emaila klienta, max 100 znaków.",
        ["Method"] = "Metoda płatności: 1 = Blik, 2 = BankTransfer, 3 = Cash, 4 = PaymentGateway.",
        ["Status"] = "1 = PendingConfirmation, 2 = Confirmed, 3 = Rejected, 4 = Cancelled, 5 = Reversed.",
        ["Source"] = "1 = ClientRequest, 2 = StaffEntry, 3 = PaymentGateway.",
        ["ReceiptStatus"] = "0 = None, 1 = Issued, 2 = Cancelled, 3 = Pending, 4 = ManualRequired, 5 = Failed.",
        ["From"] = "Data wpłaty (PaymentDate) od, włącznie. ISO 8601; bez strefy traktowane jako UTC. Musi być <= To.",
        ["To"] = "Data wpłaty (PaymentDate) do, włącznie. ISO 8601; bez strefy traktowane jako UTC.",
        ["AmountMin"] = "Kwota brutto (Amount) od, włącznie, >= 0. Musi być <= AmountMax.",
        ["AmountMax"] = "Kwota brutto (Amount) do, włącznie, >= 0.",
        ["Sort"] = "Dozwolone: " + ClientPaymentFilterDto.SortValues.Replace("|", ", ") +
            ". Domyślnie paymentDateDesc. Daty wg PaymentDate (nie CreatedAt); kwota z dogrywką PaymentDate malejąco, potem Id."
    };

    private static readonly Dictionary<string, (string Description, Dictionary<string, string> Parameters, Dictionary<string, IOpenApiAny> Examples)> Operations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["api/trainer-portal/clients/page"] = (
            "Strona klientów przypisanych do zalogowanego profilu trenera (także dla Ownera z profilem trenera). " + Paging + "Domyślne sortowanie: name. " +
            "Przykład: GET /api/trainer-portal/clients/page?page=1&pageSize=25&status=Active&hasActivePackage=false&sort=balance",
            ClientParameters,
            new(StringComparer.OrdinalIgnoreCase) { ["Status"] = new OpenApiString("Active"), ["HasActivePackage"] = new OpenApiBoolean(true), ["Sort"] = new OpenApiString("packageUsageDesc") }),
        ["api/trainer-portal/clients"] = (
            "Pełna lista klientów zalogowanego trenera bez parametrów, sortowana po CreatedAt malejąco. Do filtrów i sortowania użyj /api/trainer-portal/clients/page.",
            new(), new()),
        ["api/billing/payments"] = (
            "Wpłaty w organizacji (Owner). " + Paging + "Domyślne sortowanie: paymentDateDesc. " +
            "Przykład: GET /api/billing/payments?method=3&amountMin=100&amountMax=500&from=2026-10-01T00:00:00Z&to=2026-10-31T23:59:59Z&sort=amountDesc",
            PaymentParameters,
            new(StringComparer.OrdinalIgnoreCase) { ["Method"] = new OpenApiInteger(3), ["AmountMin"] = new OpenApiDouble(100), ["AmountMax"] = new OpenApiDouble(500), ["Sort"] = new OpenApiString("amountDesc") }),
        ["api/billing/clients/{clientId}/payments"] = (
            "Wpłaty jednego klienta. Parametry jak w /api/billing/payments (ClientId z trasy). " + Paging,
            PaymentParameters, new()),
        ["api/trainer-portal/clients/{id}/payments"] = (
            "Wpłaty klienta dostępnego dla trenera. Parametry jak w /api/billing/payments. " + Paging,
            PaymentParameters, new())
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath?.Split('?')[0].TrimEnd('/');
        if (path is null || !Operations.TryGetValue(path, out var docs)) return;
        operation.Description = docs.Description;
        foreach (var parameter in operation.Parameters)
        {
            if (docs.Parameters.TryGetValue(parameter.Name, out var description)) parameter.Description = description;
            if (docs.Examples.TryGetValue(parameter.Name, out var example)) parameter.Example = example;
            if (parameter.Name.Equals("Sort", StringComparison.OrdinalIgnoreCase) && docs.Parameters.Count > 0)
            {
                var values = (path.EndsWith("payments") ? ClientPaymentFilterDto.SortValues : PortalPageFilter.SortValues).Split('|');
                parameter.Schema.Enum = values.Select(v => (IOpenApiAny)new OpenApiString(v)).ToList();
                parameter.Schema.Default = new OpenApiString(values[0]);
            }
        }
    }
}
