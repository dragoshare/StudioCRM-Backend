using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Trainers;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Services;
public partial class TrainerService
{
    private async Task<TrainerDto> CreateOrganizationTrainerAsync(CreateTrainerDto request)
    {
        if (!await _context.Organizations.AnyAsync(o => o.Id == _context.OrganizationId && o.Status == "Active"))
            throw new InvalidOperationException("Organization is not active.");
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("Email and password are required.");
        // Reusing another account, including a legacy account, is deliberately unavailable.
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            throw new InvalidOperationException("Account already exists. Use a separate email for this studio.");
        var locations = request.LocationIds?.Distinct().ToArray() ?? [];
        if (locations.Length == 0 || await _context.Locations.CountAsync(l => locations.Contains(l.Id)) != locations.Length)
            throw new InvalidOperationException("Select locations belonging to this organization.");
        var user = new User { Email = email, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim() };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        var trainer = new Trainer { User = user, Bio = request.Bio, Phone = request.Phone, Status = request.Status,
            TeamJoinedDate = NormalizeNullableDateTime(request.TeamJoinedDate), CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow, CreatedBy = request.CreatedBy };
        _context.Add(trainer);
        _context.Add(new OrganizationMembership { OrganizationId = _context.OrganizationId, User = user,
            IsTrainer = true, CreatedAt = DateTime.UtcNow });
        foreach (var location in locations) _context.Add(new TrainerLocation { Trainer = trainer, LocationId = location });
        await _context.SaveChangesAsync();
        return await GetProjectedById(trainer.Id);
    }
}
