using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Application.ClientPackages.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Application.ClientPackages.Services;

public class ClientPackageService : IClientPackageService
{
    private readonly StudioCRMDbContext _context;

    public ClientPackageService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateAsync(CreateClientPackageRequest request)
    {
        var expectedUnitPrice = request.TotalPrice / request.TotalSessions;

        var clientPackage = new ClientPackage
        {
            ClientId = request.ClientId,
            PackageId = request.PackageId,
            Name = request.Name,
            TotalSessions = request.TotalSessions,
            TotalPrice = request.TotalPrice,
            ExpectedUnitPrice = expectedUnitPrice,
            ExpectedBillingType = request.ExpectedBillingType,
            PaymentStatus = PaymentStatus.Unpaid,
            PurchaseDate = request.PurchaseDate,
            ValidUntil = request.ValidUntil,
            PaymentDueDate = request.PaymentDueDate,
            IsActive = true
        };

        _context.ClientPackages.Add(clientPackage);
        await _context.SaveChangesAsync();

        return clientPackage.Id;
    }
}