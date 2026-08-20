using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.TrainerContracts;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainerContractService : ITrainerContractService
{
    private readonly StudioCRMDbContext _context;

    public TrainerContractService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<List<TrainerContractDto>> GetByTrainerIdAsync(int trainerId)
    {
        await EnsureTrainerExistsAsync(trainerId);

        var contracts = await _context.TrainerContracts
            .Include(c => c.ContractLocations)
                .ThenInclude(cl => cl.Location)
            .Where(c => c.TrainerId == trainerId)
            .OrderByDescending(c => c.ValidFrom)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        return contracts.Select(MapContract).ToList();
    }

    public async Task<TrainerContractDto?> GetByIdAsync(int trainerId, int contractId)
    {
        var contract = await _context.TrainerContracts
            .Include(c => c.ContractLocations)
                .ThenInclude(cl => cl.Location)
            .FirstOrDefaultAsync(c => c.Id == contractId && c.TrainerId == trainerId);

        return contract is null ? null : MapContract(contract);
    }

    public async Task<TrainerContractDto> CreateAsync(int trainerId, CreateTrainerContractDto request)
    {
        await EnsureTrainerExistsAsync(trainerId);
        var locationIds = await ResolveContractLocationIdsAsync(trainerId, request.LocationIds);

        var contract = new TrainerContract
        {
            TrainerId = trainerId,
            ContractType = NormalizeContractType(request.ContractType),
            ContractNumber = NormalizeRequiredText(request.ContractNumber, "Contract number is required."),
            SignedAt = NormalizeDateTime(request.SignedAt),
            ValidFrom = NormalizeDateTime(request.ValidFrom),
            ValidTo = NormalizeNullableDateTime(request.ValidTo),
            Notes = NormalizeOptionalText(request.Notes),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        ValidateDateRange(contract.ValidFrom, contract.ValidTo);
        await EnsureNoOverlappingActiveContractAsync(
            contract.TrainerId,
            contract.ValidFrom,
            contract.ValidTo,
            locationIds,
            null);

        await _context.TrainerContracts.AddAsync(contract);
        await _context.SaveChangesAsync();

        await SetContractLocationsAsync(contract, locationIds);

        return MapContract(contract);
    }

    public async Task<TrainerContractDto?> UpdateAsync(
        int trainerId,
        int contractId,
        UpdateTrainerContractDto request)
    {
        var contract = await _context.TrainerContracts
            .Include(c => c.ContractLocations)
            .FirstOrDefaultAsync(c => c.Id == contractId && c.TrainerId == trainerId);

        if (contract is null)
            return null;

        var locationIds = await ResolveContractLocationIdsAsync(trainerId, request.LocationIds);

        contract.ContractType = NormalizeContractType(request.ContractType);
        contract.ContractNumber = NormalizeRequiredText(request.ContractNumber, "Contract number is required.");
        contract.SignedAt = NormalizeDateTime(request.SignedAt);
        contract.ValidFrom = NormalizeDateTime(request.ValidFrom);
        contract.ValidTo = NormalizeNullableDateTime(request.ValidTo);
        contract.Notes = NormalizeOptionalText(request.Notes);
        contract.IsActive = request.IsActive;
        contract.UpdatedAt = DateTime.UtcNow;

        ValidateDateRange(contract.ValidFrom, contract.ValidTo);

        if (contract.IsActive)
        {
            await EnsureNoOverlappingActiveContractAsync(
                contract.TrainerId,
                contract.ValidFrom,
                contract.ValidTo,
                locationIds,
                contract.Id);
        }

        await SetContractLocationsAsync(contract, locationIds);

        await _context.SaveChangesAsync();

        return MapContract(contract);
    }

    private async Task EnsureTrainerExistsAsync(int trainerId)
    {
        var exists = await _context.Trainers.AnyAsync(t => t.Id == trainerId);

        if (!exists)
            throw new InvalidOperationException("Trainer does not exist.");
    }

    private async Task EnsureNoOverlappingActiveContractAsync(
        int trainerId,
        DateTime validFrom,
        DateTime? validTo,
        List<int> locationIds,
        int? excludedContractId)
    {
        var rangeEnd = validTo ?? DateTime.MaxValue;

        var hasOverlap = await _context.TrainerContracts.AnyAsync(c =>
            c.TrainerId == trainerId &&
            c.IsActive &&
            (!excludedContractId.HasValue || c.Id != excludedContractId.Value) &&
            c.ValidFrom <= rangeEnd &&
            (c.ValidTo == null || c.ValidTo >= validFrom) &&
            c.ContractLocations.Any(cl => locationIds.Contains(cl.LocationId)));

        if (hasOverlap)
            throw new InvalidOperationException("Trainer already has an active contract overlapping this period for one of these locations.");
    }

    private static TrainerContractDto MapContract(TrainerContract contract)
    {
        var now = DateTime.UtcNow;
        var isCurrent =
            contract.IsActive &&
            contract.ValidFrom <= now &&
            (!contract.ValidTo.HasValue || contract.ValidTo.Value >= now);

        return new TrainerContractDto
        {
            Id = contract.Id,
            TrainerId = contract.TrainerId,
            ContractType = contract.ContractType.ToString(),
            ContractNumber = contract.ContractNumber,
            SignedAt = contract.SignedAt,
            ValidFrom = contract.ValidFrom,
            ValidTo = contract.ValidTo,
            LocationIds = contract.ContractLocations
                .OrderBy(cl => cl.Location.Name)
                .Select(cl => cl.LocationId)
                .ToList(),
            LocationNames = contract.ContractLocations
                .OrderBy(cl => cl.Location.Name)
                .Select(cl => cl.Location.Name)
                .ToList(),
            Notes = contract.Notes,
            IsActive = contract.IsActive,
            IsCurrent = isCurrent,
            IsExpired = contract.ValidTo.HasValue && contract.ValidTo.Value < now,
            DaysUntilEnd = contract.ValidTo.HasValue
                ? Math.Max(0, (int)Math.Ceiling((contract.ValidTo.Value.Date - now.Date).TotalDays))
                : null,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt
        };
    }

    private static void ValidateDateRange(DateTime validFrom, DateTime? validTo)
    {
        if (validTo.HasValue && validTo.Value < validFrom)
            throw new InvalidOperationException("Contract valid-to date cannot be earlier than valid-from date.");
    }

    private async Task<List<int>> ResolveContractLocationIdsAsync(
        int trainerId,
        List<int>? requestedLocationIds)
    {
        var assignedLocationIds = await _context.TrainerLocations
            .Where(tl => tl.TrainerId == trainerId)
            .Select(tl => tl.LocationId)
            .ToListAsync();

        if (assignedLocationIds.Count == 0)
            throw new InvalidOperationException("Trainer must be assigned to at least one location before adding a contract.");

        var normalized = requestedLocationIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? new List<int>();

        if (normalized.Count == 0)
            return assignedLocationIds.OrderBy(id => id).ToList();

        var assignedSet = assignedLocationIds.ToHashSet();

        if (normalized.Any(id => !assignedSet.Contains(id)))
            throw new InvalidOperationException("Contract can only include locations assigned to this trainer.");

        return normalized.OrderBy(id => id).ToList();
    }

    private async Task SetContractLocationsAsync(
        TrainerContract contract,
        List<int> locationIds)
    {
        if (contract.Id == 0)
            await _context.SaveChangesAsync();

        var existingLocations = await _context.TrainerContractLocations
            .Where(cl => cl.TrainerContractId == contract.Id)
            .ToListAsync();

        _context.TrainerContractLocations.RemoveRange(existingLocations);

        await _context.TrainerContractLocations.AddRangeAsync(
            locationIds.Select(locationId => new TrainerContractLocation
            {
                TrainerContractId = contract.Id,
                LocationId = locationId
            }));

        await _context.SaveChangesAsync();

        contract.ContractLocations = await _context.TrainerContractLocations
            .Include(cl => cl.Location)
            .Where(cl => cl.TrainerContractId == contract.Id)
            .ToListAsync();
    }

    private static TrainerContractType NormalizeContractType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return TrainerContractType.B2B;

        return Enum.TryParse<TrainerContractType>(value.Trim(), ignoreCase: true, out var contractType)
            ? contractType
            : throw new InvalidOperationException("Invalid trainer contract type.");
    }

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new InvalidOperationException(errorMessage)
            : normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static DateTime NormalizeDateTime(DateTime value)
    {
        if (value == default)
            throw new InvalidOperationException("Contract date is required.");

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? NormalizeNullableDateTime(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
