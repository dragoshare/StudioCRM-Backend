using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class ClientService : IClientService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ClientService(StudioCRMDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        if (_currentUser.IsTrainer)
        {
            if (!_currentUser.UserId.HasValue)
                throw new InvalidOperationException("Current trainer user is invalid.");

            var currentTrainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

            if (currentTrainer is null)
                throw new InvalidOperationException("Trainer profile not found.");

            if (request.TrainerId.HasValue && request.TrainerId.Value != currentTrainer.Id)
                throw new InvalidOperationException("Trainer can create clients only for themselves.");

            request.TrainerId = currentTrainer.Id;
        }

        var client = new Client
        {
            TrainerId = request.TrainerId,
            ActivePackageId = request.ActivePackageId,
            LocationId = request.LocationId,
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
        var query = ApplyAccessControl(BuildClientQuery());

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<ClientDto?> GetByIdAsync(int id)
    {
        var query = ApplyAccessControl(BuildClientQuery());

        return await query
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

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
            return null;

        if (_currentUser.IsTrainer)
        {
            if (!_currentUser.UserId.HasValue)
                throw new InvalidOperationException("Current trainer user is invalid.");

            var currentTrainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

            if (currentTrainer is null)
                throw new InvalidOperationException("Trainer profile not found.");

            if (client.TrainerId != currentTrainer.Id)
                throw new InvalidOperationException("Trainer can update only their own clients.");

            request.TrainerId = currentTrainer.Id;
        }

        client.TrainerId = request.TrainerId;
        client.ActivePackageId = request.ActivePackageId;
        client.LocationId = request.LocationId;
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
            return false;

        client.IsDeleted = true;
        client.DeletedAt = DateTime.UtcNow;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var client = await _context.Clients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client is null || !client.IsDeleted)
            return false;

        client.IsDeleted = false;
        client.DeletedAt = null;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ClientDto>> GetDeletedAsync()
    {
        return await _context.Clients
            .IgnoreQueryFilters()
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Include(c => c.ActivePackage)
            .Include(c => c.Location)
            .Where(c => c.IsDeleted)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
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

    public async Task<List<ClientDto>> GetFilteredAsync(ClientFilterDto filter)
    {
        var query = ApplyAccessControl(BuildClientQuery());

        if (filter.TrainerId.HasValue)
            query = query.Where(c => c.TrainerId == filter.TrainerId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(c => c.LocationId == filter.LocationId.Value);

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
            .Include(c => c.Location)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
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

    private IQueryable<ClientDto> ApplyAccessControl(IQueryable<ClientDto> query)
    {
        if (_currentUser.IsOwner)
            return query;

        if (_currentUser.IsTrainer && _currentUser.UserId.HasValue)
        {
            return query.Where(c =>
                c.TrainerId != null &&
                _context.Trainers.Any(t => t.Id == c.TrainerId && t.UserId == _currentUser.UserId.Value));
        }

        return query.Where(c => false);
    }

    private async Task<ClientDto> GetProjectedById(int id)
    {
        var query = ApplyAccessControl(BuildClientQuery());

        return await query.FirstAsync(c => c.Id == id);
    }
}