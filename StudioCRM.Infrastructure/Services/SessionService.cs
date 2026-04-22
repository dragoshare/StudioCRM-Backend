using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SessionService(StudioCRMDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SessionDto> CreateAsync(CreateSessionDto request)
    {
        if (_currentUser.IsTrainer)
        {
            if (!_currentUser.UserId.HasValue)
                throw new InvalidOperationException("Current trainer user is invalid.");

            var currentTrainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

            if (currentTrainer is null)
                throw new InvalidOperationException("Trainer profile not found.");

            if (request.TrainerId != currentTrainer.Id)
                throw new InvalidOperationException("Trainer can create sessions only for themselves.");

            request.TrainerId = currentTrainer.Id;
        }

        await ValidateReferences(request.TrainerId, request.ClientId, request.PackageId, request.LocationId);

        var session = new Session
        {
            Title = request.Title,
            Note = request.Note,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            TrainerId = request.TrainerId,
            ClientId = request.ClientId,
            PackageId = request.PackageId,
            StudioRoom = request.StudioRoom,
            LocationId = request.LocationId,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();

        return await GetProjectedById(session.Id);
    }

    public async Task<List<SessionDto>> GetAllAsync()
    {
        var query = ApplyAccessControl(BuildSessionQuery());

        return await query
            .OrderBy(s => s.StartAt)
            .ToListAsync();
    }

    public async Task<SessionDto?> GetByIdAsync(int id)
    {
        var query = ApplyAccessControl(BuildSessionQuery());

        return await query
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<SessionDto?> UpdateAsync(int id, UpdateSessionDto request)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null)
            return null;

        if (_currentUser.IsTrainer)
        {
            if (!_currentUser.UserId.HasValue)
                throw new InvalidOperationException("Current trainer user is invalid.");

            var currentTrainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

            if (currentTrainer is null)
                throw new InvalidOperationException("Trainer profile not found.");

            if (session.TrainerId != currentTrainer.Id)
                throw new InvalidOperationException("Trainer can update only their own sessions.");

            request.TrainerId = currentTrainer.Id;
        }

        await ValidateReferences(request.TrainerId, request.ClientId, request.PackageId, request.LocationId);

        session.Title = request.Title;
        session.Note = request.Note;
        session.StartAt = request.StartAt;
        session.EndAt = request.EndAt;
        session.TrainerId = request.TrainerId;
        session.ClientId = request.ClientId;
        session.PackageId = request.PackageId;
        session.StudioRoom = request.StudioRoom;
        session.LocationId = request.LocationId;
        session.Status = request.Status;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetProjectedById(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null)
            return false;

        session.IsDeleted = true;
        session.DeletedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var session = await _context.Sessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null || !session.IsDeleted)
            return false;

        session.IsDeleted = false;
        session.DeletedAt = null;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<SessionDto>> GetDeletedAsync()
    {
        return await _context.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Client)
            .Include(s => s.Package)
            .Include(s => s.Location)
            .Where(s => s.IsDeleted)
            .Select(s => new SessionDto
            {
                Id = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                TrainerId = s.TrainerId,
                ClientId = s.ClientId,
                PackageId = s.PackageId,
                LocationId = s.LocationId,
                LocationName = s.Location.Name,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = s.Client.FirstName + " " + s.Client.LastName,
                PackageName = s.Package != null ? s.Package.Name : null,
                StudioRoom = s.StudioRoom,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                CreatedBy = s.CreatedBy
            })
            .ToListAsync();
    }

    public async Task<List<SessionDto>> GetFilteredAsync(SessionFilterDto filter)
    {
        var query = ApplyAccessControl(BuildSessionQuery());

        if (filter.TrainerId.HasValue)
            query = query.Where(s => s.TrainerId == filter.TrainerId.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(s => s.ClientId == filter.ClientId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(s => s.LocationId == filter.LocationId.Value);

        if (filter.DateFrom.HasValue)
            query = query.Where(s => s.StartAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(s => s.StartAt <= filter.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(s => s.Status == filter.Status);

        return await query
            .OrderBy(s => s.StartAt)
            .ToListAsync();
    }

    private IQueryable<SessionDto> BuildSessionQuery()
    {
        return _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Client)
            .Include(s => s.Package)
            .Include(s => s.Location)
            .Select(s => new SessionDto
            {
                Id = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                TrainerId = s.TrainerId,
                ClientId = s.ClientId,
                PackageId = s.PackageId,
                LocationId = s.LocationId,
                LocationName = s.Location.Name,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = s.Client.FirstName + " " + s.Client.LastName,
                PackageName = s.Package != null ? s.Package.Name : null,
                StudioRoom = s.StudioRoom,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                CreatedBy = s.CreatedBy
            });
    }

    private IQueryable<SessionDto> ApplyAccessControl(IQueryable<SessionDto> query)
    {
        if (_currentUser.IsOwner)
            return query;

        if (_currentUser.IsTrainer && _currentUser.UserId.HasValue)
        {
            return query.Where(s =>
                _context.Trainers.Any(t => t.Id == s.TrainerId && t.UserId == _currentUser.UserId.Value));
        }

        return query.Where(s => false);
    }

    private async Task<SessionDto> GetProjectedById(int id)
    {
        var query = ApplyAccessControl(BuildSessionQuery());

        return await query.FirstAsync(s => s.Id == id);
    }

    private async Task ValidateReferences(int trainerId, int clientId, int? packageId, int locationId)
    {
        var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == trainerId);
        if (!trainerExists)
            throw new InvalidOperationException("Trainer does not exist.");

        var clientExists = await _context.Clients.AnyAsync(c => c.Id == clientId);
        if (!clientExists)
            throw new InvalidOperationException("Client does not exist.");

        if (packageId.HasValue)
        {
            var packageExists = await _context.Packages.AnyAsync(p => p.Id == packageId.Value);
            if (!packageExists)
                throw new InvalidOperationException("Package does not exist.");
        }

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        var trainerInLocation = await _context.TrainerLocations
            .AnyAsync(tl => tl.TrainerId == trainerId && tl.LocationId == locationId);

        if (!trainerInLocation)
            throw new InvalidOperationException("Trainer is not assigned to this location.");
    }
}