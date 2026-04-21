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
                throw new InvalidOperationException("Trainer does not exist.");
        }

        if (request.ActivePackageId.HasValue)
        {
            var packageExists = await _context.Packages.AnyAsync(p => p.Id == request.ActivePackageId.Value);
            if (!packageExists)
                throw new InvalidOperationException("Package does not exist.");
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

        return await GetProjectedById(client.Id);
    }

    public async Task<List<ClientDto>> GetAllAsync()
    {
        return await BuildClientQuery()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<ClientDto?> GetByIdAsync(int id)
    {
        return await BuildClientQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<ClientDto?> UpdateAsync(int id, UpdateClientDto request)
    {
        if (request.TrainerId.HasValue)
        {
            var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == request.TrainerId.Value);
            if (!trainerExists)
                throw new InvalidOperationException("Trainer does not exist.");
        }

        if (request.ActivePackageId.HasValue)
        {
            var packageExists = await _context.Packages.AnyAsync(p => p.Id == request.ActivePackageId.Value);
            if (!packageExists)
                throw new InvalidOperationException("Package does not exist.");
        }

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
        {
            return null;
        }

        client.TrainerId = request.TrainerId;
        client.ActivePackageId = request.ActivePackageId;
        client.FirstName = request.FirstName;
        client.LastName = request.LastName;
        client.Email = request.Email;
        client.PhoneNumber = request.PhoneNumber;
        client.AvatarUrl = request.AvatarUrl;
        client.Goal = request.Goal;
        client.Notes = request.Notes;
        client.ProgressPercent = request.ProgressPercent;
        client.BillingStatus = request.BillingStatus;
        client.Status = request.Status;
        client.NextSessionAt = request.NextSessionAt;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetProjectedById(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
        {
            return false;
        }

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ClientDto>> GetFilteredAsync(ClientFilterDto filter)
    {
        var query = BuildClientQuery();

        if (filter.TrainerId.HasValue)
            query = query.Where(c => c.TrainerId == filter.TrainerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(c => c.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(search) ||
                c.LastName.ToLower().Contains(search) ||
                c.Email.ToLower().Contains(search));
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    private IQueryable<ClientDto> BuildClientQuery()
    {
        return _context.Clients
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
            });
    }

    private async Task<ClientDto> GetProjectedById(int id)
    {
        return await BuildClientQuery().FirstAsync(c => c.Id == id);
    }
}