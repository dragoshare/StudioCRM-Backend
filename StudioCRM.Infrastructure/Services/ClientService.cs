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
            var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == request.TrainerId.Value);
            if (!trainerExists)
            {
                throw new InvalidOperationException("Trainer does not exist.");
            }
        }

        var client = new Client
        {
            TrainerId = request.TrainerId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
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
            FirstName = client.FirstName,
            LastName = client.LastName,
            Email = client.Email,
            PhoneNumber = client.PhoneNumber,
            IsActive = client.IsActive,
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
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                IsActive = c.IsActive,
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
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                IsActive = c.IsActive,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            })
            .FirstOrDefaultAsync();
    }
}