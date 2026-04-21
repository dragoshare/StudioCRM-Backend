using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly StudioCRMDbContext _context;

    public SessionService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDto> CreateAsync(CreateSessionDto request)
    {
        await ValidateReferences(request.TrainerId, request.ClientId, request.PackageId);

        var session = new Session
        {
            Title = request.Title,
            Note = request.Note,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            TrainerId = request.TrainerId,
            ClientId = request.ClientId,
            PackageId = request.PackageId,
            Location = request.Location,
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
        return await BuildSessionQuery()
            .OrderBy(s => s.StartAt)
            .ToListAsync();
    }

    public async Task<SessionDto?> GetByIdAsync(int id)
    {
        return await BuildSessionQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<SessionDto?> UpdateAsync(int id, UpdateSessionDto request)
    {
        await ValidateReferences(request.TrainerId, request.ClientId, request.PackageId);

        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null)
        {
            return null;
        }

        session.Title = request.Title;
        session.Note = request.Note;
        session.StartAt = request.StartAt;
        session.EndAt = request.EndAt;
        session.TrainerId = request.TrainerId;
        session.ClientId = request.ClientId;
        session.PackageId = request.PackageId;
        session.Location = request.Location;
        session.Status = request.Status;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetProjectedById(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null)
        {
            return false;
        }

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
        {
            return false;
        }

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
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = s.Client.FirstName + " " + s.Client.LastName,
                PackageName = s.Package != null ? s.Package.Name : null,
                Location = s.Location,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                CreatedBy = s.CreatedBy
            })
            .ToListAsync();
    }

    public async Task<List<SessionDto>> GetFilteredAsync(SessionFilterDto filter)
    {
        var query = BuildSessionQuery();

        if (filter.TrainerId.HasValue)
            query = query.Where(s => s.TrainerId == filter.TrainerId.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(s => s.ClientId == filter.ClientId.Value);

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
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = s.Client.FirstName + " " + s.Client.LastName,
                PackageName = s.Package != null ? s.Package.Name : null,
                Location = s.Location,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                CreatedBy = s.CreatedBy
            });
    }

    private async Task<SessionDto> GetProjectedById(int id)
    {
        return await BuildSessionQuery().FirstAsync(s => s.Id == id);
    }

    private async Task ValidateReferences(int trainerId, int clientId, int? packageId)
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
    }
}