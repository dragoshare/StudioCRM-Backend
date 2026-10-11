using Microsoft.EntityFrameworkCore;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services.Calendar;
internal static class OutlookImportAccess
{
    internal static Task<bool> IsOwnerAsync(StudioCRMDbContext db, int userId) => db.Users.AnyAsync(u => u.Id == userId &&
        u.IsActive && u.UserRoles.Any(r => r.Role.Name == "Owner"));
    internal static Task<bool> CanManageAsync(StudioCRMDbContext db, int userId, int trainerId, int locationId) =>
        db.Users.AnyAsync(u => u.Id == userId && u.IsActive &&
            (u.UserRoles.Any(r => r.Role.Name == "Owner") || u.UserRoles.Any(r => r.Role.Name == "Trainer") &&
             db.TrainerLocations.Any(t => t.TrainerId == trainerId && t.Trainer.UserId == userId && t.LocationId == locationId)));
}
