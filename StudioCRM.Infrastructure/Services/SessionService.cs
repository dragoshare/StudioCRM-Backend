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
        var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == request.TrainerId);
        if (!trainerExists)
        {
            throw new InvalidOperationException("Trainer does not exist.");
        }

        var clientExists = await _context.Clients.AnyAsync(c => c.Id == request.ClientId);
        if (!clientExists)
        {
            throw new InvalidOperationException("Client does not exist.");
        }

        if (request.PackageId.HasValue)
        {
            var packageExists = await _context.Packages.AnyAsync(p => p.Id == request.PackageId.Value);
            if (!packageExists)
            {
                throw new InvalidOperationException("Package does not exist.");
            }
        }

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

        var result = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Client)
            .Include(s => s.Package)
            .Where(s => s.Id == session.Id)
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
            .FirstAsync();

        return result;
    }

    public async Task<List<SessionDto>> GetAllAsync()
    {
        return await _context.Sessions
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
            })
            .ToListAsync();
    }

    public async Task<SessionDto?> GetByIdAsync(int id)
    {
        return await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Client)
            .Include(s => s.Package)
            .Where(s => s.Id == id)
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
            .FirstOrDefaultAsync();
    }
}