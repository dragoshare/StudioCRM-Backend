using System.Globalization;
using StudioCRM.Application.DTOs.Payments;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Application.Common;

public static class PaymentInstructionBuilder
{
    private const string DefaultTransferTitleTemplate = "Pakiet {PackageName} - {ClientFullName}";

    public static PaymentInstructionsDto Build(
        Location? location,
        string clientFullName,
        string packageName,
        int clientPackageId,
        decimal amountDue,
        string currency)
    {
        var titleTemplate = string.IsNullOrWhiteSpace(location?.TransferTitleTemplate)
            ? DefaultTransferTitleTemplate
            : location.TransferTitleTemplate!;

        return new PaymentInstructionsDto
        {
            RecipientName = Normalize(location?.PaymentRecipientName),
            BankAccountNumber = Normalize(location?.BankAccountNumber),
            BlikPhoneNumber = Normalize(location?.BlikPhoneNumber),
            TransferTitle = ResolveTemplate(
                titleTemplate,
                clientFullName,
                packageName,
                clientPackageId,
                amountDue,
                currency),
            Description = ResolveTemplate(
                location?.PaymentDescription,
                clientFullName,
                packageName,
                clientPackageId,
                amountDue,
                currency)
        };
    }

    private static string? ResolveTemplate(
        string? template,
        string clientFullName,
        string packageName,
        int clientPackageId,
        decimal amountDue,
        string currency)
    {
        if (string.IsNullOrWhiteSpace(template))
            return null;

        var normalizedClientName = clientFullName.Trim();
        var nameParts = normalizedClientName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.FirstOrDefault() ?? string.Empty;
        var lastName = nameParts.Length > 1
            ? string.Join(" ", nameParts.Skip(1))
            : string.Empty;

        return template
            .Replace("{ClientFullName}", normalizedClientName, StringComparison.OrdinalIgnoreCase)
            .Replace("{FirstName}", firstName, StringComparison.OrdinalIgnoreCase)
            .Replace("{LastName}", lastName, StringComparison.OrdinalIgnoreCase)
            .Replace("{PackageName}", packageName.Trim(), StringComparison.OrdinalIgnoreCase)
            .Replace("{ClientPackageId}", clientPackageId.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{AmountDue}", amountDue.ToString("0.00", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{Currency}", currency.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
