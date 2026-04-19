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
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
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
        if (!await context.Trainers.AnyAsync())
        {
            var passwordHasher = new PasswordHasher<User>();

            var trainerUser = new User
            {
                Email = "trainer@studiocrm.local",
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            trainerUser.PasswordHash = passwordHasher.HashPassword(trainerUser, "Trainer123!");

            await context.Users.AddAsync(trainerUser);
            await context.SaveChangesAsync();

            var trainerRole = await context.Roles.FirstAsync(r => r.Name == "Trainer");

            await context.UserRoles.AddAsync(new UserRole
            {
                UserId = trainerUser.Id,
                RoleId = trainerRole.Id
            });

            var trainer = new Trainer
            {
                UserId = trainerUser.Id,
                Bio = "Trener siłowy i motoryczny",
                Phone = "123456789",
                Status = "Active",
                ExperienceYears = 5,
                HourlyRate = 120,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await context.Trainers.AddAsync(trainer);
            await context.SaveChangesAsync();
        }
        if (!await context.Packages.AnyAsync())
        {
            var packages = new List<Package>
    {
        new Package
        {
            Name = "4 treningi",
            Price = 400,
            Currency = "PLN",
            SessionsLimit = 4,
            DurationDays = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new Package
        {
            Name = "8 treningów",
            Price = 720,
            Currency = "PLN",
            SessionsLimit = 8,
            DurationDays = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };

            await context.Packages.AddRangeAsync(packages);
            await context.SaveChangesAsync();
        }
        if (!await context.Clients.AnyAsync())
        {
            var trainer = await context.Trainers.FirstAsync();
            var package = await context.Packages.FirstAsync();

            var clients = new List<Client>
    {
        new Client
        {
            FirstName = "Anna",
            LastName = "Nowak",
            Email = "anna@test.pl",
            PhoneNumber = "111222333",
            TrainerId = trainer.Id,
            ActivePackageId = package.Id,
            Status = "Active",
            ProgressPercent = 20,
            BillingStatus = "Paid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new Client
        {
            FirstName = "Piotr",
            LastName = "Zielinski",
            Email = "piotr@test.pl",
            PhoneNumber = "444555666",
            TrainerId = trainer.Id,
            ActivePackageId = package.Id,
            Status = "Active",
            ProgressPercent = 50,
            BillingStatus = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };

            await context.Clients.AddRangeAsync(clients);
            await context.SaveChangesAsync();
        }
        if (!await context.Sessions.AnyAsync())
        {
            var trainer = await context.Trainers.FirstAsync();
            var client = await context.Clients.FirstAsync();
            var package = await context.Packages.FirstAsync();

            var sessions = new List<Session>
    {
        new Session
        {
            Title = "Trening FBW",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(1),
            TrainerId = trainer.Id,
            ClientId = client.Id,
            PackageId = package.Id,
            Location = "Studio Niepołomice",
            Status = "Planned",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new Session
        {
            Title = "Trening siłowy",
            StartAt = DateTime.UtcNow.AddDays(2),
            EndAt = DateTime.UtcNow.AddDays(2).AddHours(1),
            TrainerId = trainer.Id,
            ClientId = client.Id,
            PackageId = package.Id,
            Location = "Studio Kłaj",
            Status = "Planned",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };

            await context.Sessions.AddRangeAsync(sessions);
            await context.SaveChangesAsync();
        }
    }
}