using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(StudioCRMDbContext context)
    {
        await context.Database.MigrateAsync();

        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role { Name = "Owner" },
                new Role { Name = "Trainer" },
                new Role { Name = "Client" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        if (!await context.Users.AnyAsync(u => u.Email == "owner@studiocrm.local"))
        {
            var passwordHasher = new PasswordHasher<User>();

            var owner = new User
            {
                Email = "owner@studiocrm.local",
                FirstName = "System",
                LastName = "Owner",
                PhoneNumber = "000000000",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            owner.PasswordHash = passwordHasher.HashPassword(owner, "Admin123!");

            await context.Users.AddAsync(owner);
            await context.SaveChangesAsync();

            var ownerRole = await context.Roles.FirstAsync(r => r.Name == "Owner");

            var userRole = new UserRole
            {
                UserId = owner.Id,
                RoleId = ownerRole.Id
            };

            await context.UserRoles.AddAsync(userRole);
            await context.SaveChangesAsync();
        }
    }
}