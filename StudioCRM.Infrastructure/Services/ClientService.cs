using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class ClientService : IClientService
{
    private readonly StudioCRMDbContext _context;

    public ClientService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<ClientDto> CreateAsync(CreateClientDto request)
    {
        if (request.TrainerId.HasValue)
        {
            var trainerExists = await _context.Trainers
                .AnyAsync(t => t.Id == request.TrainerId.Value);

            if (!trainerExists)
            {
                throw new InvalidOperationException("Trainer does not exist.");
            }
        }

        if (request.ActivePackageId.HasValue)
        {
            var packageExists = await _context.Packages
                .AnyAsync(p => p.Id == request.ActivePackageId.Value);

            if (!packageExists)
            {
                throw new InvalidOperationException("Package does not exist.");
            }
        }

        var client = new Client
        {
            TrainerId = request.TrainerId,
            ActivePackageId = request.ActivePackageId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            AvatarUrl = request.AvatarUrl,
            Goal = request.Goal,
            Notes = request.Notes,
            ProgressPercent = request.ProgressPercent,
            BillingStatus = request.BillingStatus ?? "Pending",
            Status = request.Status ?? "New",
            NextSessionAt = request.NextSessionAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _context.Clients.AddAsync(client);
        await _context.SaveChangesAsync();

        string? trainerFullName = null;

        if (client.TrainerId.HasValue)
        {
            trainerFullName = await _context.Trainers
                .Include(t => t.User)
                .Where(t => t.Id == client.TrainerId.Value)
                .Select(t => t.User.FirstName + " " + t.User.LastName)
                .FirstOrDefaultAsync();
        }

        return new ClientDto
        {
            Id = client.Id,
            TrainerId = client.TrainerId,
            ActivePackageId = client.ActivePackageId,
            FirstName = client.FirstName,
            LastName = client.LastName,
            FullName = $"{client.FirstName} {client.LastName}",
            Email = client.Email,
            PhoneNumber = client.PhoneNumber,
            AvatarUrl = client.AvatarUrl,
            Goal = client.Goal,
            Notes = client.Notes,
            ProgressPercent = client.ProgressPercent,
            BillingStatus = client.BillingStatus,
            Status = client.Status,
            NextSessionAt = client.NextSessionAt,
            CreatedAt = client.CreatedAt,
            UpdatedAt = client.UpdatedAt,
            CreatedBy = client.CreatedBy,
            TrainerFullName = trainerFullName
        };
    }

    public async Task<List<ClientDto>> GetAllAsync()
    {
        return await _context.Clients
            .Include(c => c.Trainer)
            .ThenInclude(t => t!.User)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                AvatarUrl = c.AvatarUrl,
                Goal = c.Goal,
                Notes = c.Notes,
                ProgressPercent = c.ProgressPercent,
                BillingStatus = c.BillingStatus,
                Status = c.Status,
                NextSessionAt = c.NextSessionAt,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                CreatedBy = c.CreatedBy,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            })
            .ToListAsync();
    }

    public async Task<ClientDto?> GetByIdAsync(int id)
    {
        return await _context.Clients
            .Include(c => c.Trainer)
            .ThenInclude(t => t!.User)
            .Where(c => c.Id == id)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                AvatarUrl = c.AvatarUrl,
                Goal = c.Goal,
                Notes = c.Notes,
                ProgressPercent = c.ProgressPercent,
                BillingStatus = c.BillingStatus,
                Status = c.Status,
                NextSessionAt = c.NextSessionAt,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                CreatedBy = c.CreatedBy,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            })
            .FirstOrDefaultAsync();
    }
}