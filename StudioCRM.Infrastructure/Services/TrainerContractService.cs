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
            .Where(c => c.TrainerId == trainerId)
            .OrderByDescending(c => c.ValidFrom)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        return contracts.Select(MapContract).ToList();
    }

    public async Task<TrainerContractDto?> GetByIdAsync(int trainerId, int contractId)
    {
        var contract = await _context.TrainerContracts
            .FirstOrDefaultAsync(c => c.Id == contractId && c.TrainerId == trainerId);

        return contract is null ? null : MapContract(contract);
    }

    public async Task<TrainerContractDto> CreateAsync(int trainerId, CreateTrainerContractDto request)
    {
        await EnsureTrainerExistsAsync(trainerId);

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
        await EnsureNoOverlappingActiveContractAsync(contract.TrainerId, contract.ValidFrom, contract.ValidTo, null);

        await _context.TrainerContracts.AddAsync(contract);
        await _context.SaveChangesAsync();

        return MapContract(contract);
    }

    public async Task<TrainerContractDto?> UpdateAsync(
        int trainerId,
        int contractId,
        UpdateTrainerContractDto request)
    {
        var contract = await _context.TrainerContracts
            .FirstOrDefaultAsync(c => c.Id == contractId && c.TrainerId == trainerId);

        if (contract is null)
            return null;

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
                contract.Id);
        }

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
        int? excludedContractId)
    {
        var rangeEnd = validTo ?? DateTime.MaxValue;

        var hasOverlap = await _context.TrainerContracts.AnyAsync(c =>
            c.TrainerId == trainerId &&
            c.IsActive &&
            (!excludedContractId.HasValue || c.Id != excludedContractId.Value) &&
            c.ValidFrom <= rangeEnd &&
            (c.ValidTo == null || c.ValidTo >= validFrom));

        if (hasOverlap)
            throw new InvalidOperationException("Trainer already has an active contract overlapping this period.");
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
