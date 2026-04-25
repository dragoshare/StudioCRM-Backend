using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class SessionParticipantService : ISessionParticipantService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SessionParticipantService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<SessionParticipantDto>> GetBySessionIdAsync(int sessionId)
    {
        return await _context.SessionParticipants
            .Include(sp => sp.Client)
            .Include(sp => sp.Package)
            .Where(sp => sp.SessionId == sessionId)
            .OrderBy(sp => sp.Client.LastName)
            .ThenBy(sp => sp.Client.FirstName)
            .Select(sp => new SessionParticipantDto
            {
                Id = sp.Id,
                SessionId = sp.SessionId,
                ClientId = sp.ClientId,
                ClientFullName = sp.Client.FirstName + " " + sp.Client.LastName,
                PackageId = sp.PackageId,
                PackageName = sp.Package != null ? sp.Package.Name : null,
                AttendanceStatus = sp.AttendanceStatus,
                CountsAgainstPackage = sp.CountsAgainstPackage,
                SessionsCharged = sp.SessionsCharged,
                Note = sp.Note
            })
            .ToListAsync();
    }

    public async Task<SessionParticipantDto> AddParticipantAsync(int sessionId, AddSessionParticipantDto request)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            throw new InvalidOperationException("Session does not exist.");

        if (session.Status == "Completed")
            throw new InvalidOperationException("Cannot add participant to completed session.");

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId);

        if (client is null)
            throw new InvalidOperationException("Client does not exist.");

        var alreadyExists = await _context.SessionParticipants
            .AnyAsync(sp => sp.SessionId == sessionId && sp.ClientId == request.ClientId);

        if (alreadyExists)
            throw new InvalidOperationException("Client is already assigned to this session.");

        var packageId = request.PackageId ?? client.ActivePackageId;

        if (packageId.HasValue)
        {
            var packageExists = await _context.Packages.AnyAsync(p => p.Id == packageId.Value);
            if (!packageExists)
                throw new InvalidOperationException("Package does not exist.");
        }

        var participant = new SessionParticipant
        {
            SessionId = sessionId,
            ClientId = client.Id,
            PackageId = packageId,
            AttendanceStatus = "Planned",
            CountsAgainstPackage = request.CountsAgainstPackage,
            SessionsCharged = request.SessionsCharged,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.SessionParticipants.AddAsync(participant);


        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await MapParticipantAsync(participant.Id);
    }

    public async Task<bool> RemoveParticipantAsync(int sessionId, int participantId)
    {
        var participant = await _context.SessionParticipants
            .Include(sp => sp.Session)
            .FirstOrDefaultAsync(sp => sp.Id == participantId && sp.SessionId == sessionId);

        if (participant is null)
            return false;

        if (participant.Session.Status == "Completed")
            throw new InvalidOperationException("Cannot remove participant from completed session.");

        _context.SessionParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> CompleteSessionAsync(int sessionId, CompleteSessionDto request)
    {
        var session = await _context.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            return false;

        if (session.Status == "Cancelled")
            throw new InvalidOperationException("Cancelled session cannot be completed.");

        if (session.Status == "Completed")
            throw new InvalidOperationException("Session is already completed.");

        if (!request.Participants.Any())
            throw new InvalidOperationException("At least one participant is required.");

        foreach (var participantRequest in request.Participants)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == participantRequest.ClientId);

            if (client is null)
                throw new InvalidOperationException($"Client {participantRequest.ClientId} does not exist.");

            var participant = await _context.SessionParticipants
                .FirstOrDefaultAsync(sp =>
                    sp.SessionId == sessionId &&
                    sp.ClientId == participantRequest.ClientId);

            if (participant is null)
            {
                participant = new SessionParticipant
                {
                    SessionId = sessionId,
                    ClientId = client.Id,
                    PackageId = client.ActivePackageId,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.SessionParticipants.AddAsync(participant);
            }

            participant.AttendanceStatus = participantRequest.AttendanceStatus;
            participant.CountsAgainstPackage = participantRequest.CountsAgainstPackage;
            participant.SessionsCharged = participantRequest.SessionsCharged;
            participant.Note = participantRequest.Note;
            participant.UpdatedAt = DateTime.UtcNow;
        }

        var presentCount = request.Participants
            .Count(p => p.AttendanceStatus == "Present");

        session.Status = "Completed";
        session.ActualSessionType = request.ActualSessionType;
        session.ActualParticipantsCount = presentCount;
        session.CompletedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<SessionParticipantDto> MapParticipantAsync(int participantId)
    {
        return await _context.SessionParticipants
            .Include(sp => sp.Client)
            .Include(sp => sp.Package)
            .Where(sp => sp.Id == participantId)
            .Select(sp => new SessionParticipantDto
            {
                Id = sp.Id,
                SessionId = sp.SessionId,
                ClientId = sp.ClientId,
                ClientFullName = sp.Client.FirstName + " " + sp.Client.LastName,
                PackageId = sp.PackageId,
                PackageName = sp.Package != null ? sp.Package.Name : null,
                AttendanceStatus = sp.AttendanceStatus,
                CountsAgainstPackage = sp.CountsAgainstPackage,
                SessionsCharged = sp.SessionsCharged,
                Note = sp.Note
            })
            .FirstAsync();
    }
}