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

        var passwordHasher = new PasswordHasher<User>();

        await SeedRolesAsync(context);
        await SeedLocationsAsync(context);
        await SeedOwnerAsync(context, passwordHasher);
        await SeedMainTrainerAsync(context, passwordHasher);
        await SeedPackagesAsync(context);

        await SeedMainClientsAsync(context, passwordHasher);
        await SeedMainSessionsAsync(context);

        await SeedExtraTrainersAsync(context, passwordHasher);
        await SeedExtraClientsAsync(context, passwordHasher);

        await LinkExistingClientsToUsersAsync(context, passwordHasher);

        await SeedExtraSessionsAsync(context);
    }

    private static async Task SeedRolesAsync(StudioCRMDbContext context)
    {
        var requiredRoles = new[] { "Owner", "Trainer", "Client" };

        foreach (var roleName in requiredRoles)
        {
            if (!await context.Roles.AnyAsync(r => r.Name == roleName))
            {
                await context.Roles.AddAsync(new Role { Name = roleName });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedLocationsAsync(StudioCRMDbContext context)
    {
        if (!await context.Locations.AnyAsync(l => l.Name == "Niepołomice"))
        {
            await context.Locations.AddAsync(new Location
            {
                Name = "Niepołomice",
                City = "Niepołomice",
                Address = "ul. Przykładowa 1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await context.Locations.AnyAsync(l => l.Name == "Kłaj"))
        {
            await context.Locations.AddAsync(new Location
            {
                Name = "Kłaj",
                City = "Kłaj",
                Address = "ul. Przykładowa 2",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedOwnerAsync(StudioCRMDbContext context, PasswordHasher<User> passwordHasher)
    {
        var owner = await context.Users.FirstOrDefaultAsync(u => u.Email == "owner@studiocrm.local");
        var ownerRole = await context.Roles.FirstAsync(r => r.Name == "Owner");

        if (owner is null)
        {
            owner = new User
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
        }

        if (!await context.UserRoles.AnyAsync(ur => ur.UserId == owner.Id && ur.RoleId == ownerRole.Id))
        {
            await context.UserRoles.AddAsync(new UserRole
            {
                UserId = owner.Id,
                RoleId = ownerRole.Id
            });

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedMainTrainerAsync(StudioCRMDbContext context, PasswordHasher<User> passwordHasher)
    {
        var trainerUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "trainer@studiocrm.local");
        var trainerRole = await context.Roles.FirstAsync(r => r.Name == "Trainer");

        if (trainerUser is null)
        {
            trainerUser = new User
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
        }

        if (!await context.UserRoles.AnyAsync(ur => ur.UserId == trainerUser.Id && ur.RoleId == trainerRole.Id))
        {
            await context.UserRoles.AddAsync(new UserRole
            {
                UserId = trainerUser.Id,
                RoleId = trainerRole.Id
            });

            await context.SaveChangesAsync();
        }

        var trainer = await context.Trainers.FirstOrDefaultAsync(t => t.UserId == trainerUser.Id);

        if (trainer is null)
        {
            trainer = new Trainer
            {
                UserId = trainerUser.Id,
                Bio = "Trener siłowy i motoryczny",
                Phone = "123456789",
                AvatarUrl = null,
                Status = "Active",
                ExperienceYears = 5,
                HourlyRate = 120,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            };

            await context.Trainers.AddAsync(trainer);
            await context.SaveChangesAsync();
        }

        var locations = await context.Locations.OrderBy(l => l.Id).ToListAsync();

        var existingLocationIds = await context.TrainerLocations
            .Where(tl => tl.TrainerId == trainer.Id)
            .Select(tl => tl.LocationId)
            .ToListAsync();

        var missingTrainerLocations = locations
            .Where(l => !existingLocationIds.Contains(l.Id))
            .Select(l => new TrainerLocation
            {
                TrainerId = trainer.Id,
                LocationId = l.Id
            })
            .ToList();

        if (missingTrainerLocations.Any())
        {
            await context.TrainerLocations.AddRangeAsync(missingTrainerLocations);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedPackagesAsync(StudioCRMDbContext context)
    {
        if (!await context.Packages.AnyAsync(p => p.Name == "4 treningi"))
        {
            await context.Packages.AddAsync(new Package
            {
                Name = "4 treningi",
                Description = "Pakiet podstawowy",
                Price = 400,
                Currency = "PLN",
                SessionsLimit = 4,
                DurationDays = 30,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });
        }

        if (!await context.Packages.AnyAsync(p => p.Name == "8 treningów"))
        {
            await context.Packages.AddAsync(new Package
            {
                Name = "8 treningów",
                Description = "Pakiet standardowy",
                Price = 720,
                Currency = "PLN",
                SessionsLimit = 8,
                DurationDays = 30,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });
        }

        if (!await context.Packages.AnyAsync(p => p.Name == "12 treningów"))
        {
            await context.Packages.AddAsync(new Package
            {
                Name = "12 treningów",
                Description = "Pakiet premium",
                Price = 960,
                Currency = "PLN",
                SessionsLimit = 12,
                DurationDays = 45,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedMainClientsAsync(StudioCRMDbContext context, PasswordHasher<User> passwordHasher)
    {
        var trainer = await context.Trainers
            .Include(t => t.User)
            .FirstAsync(t => t.User.Email == "trainer@studiocrm.local");

        var package = await context.Packages.OrderBy(p => p.Id).FirstAsync();
        var defaultLocation = await context.Locations.OrderBy(l => l.Id).FirstAsync();

     

        

        var piotrUser = await EnsureClientUserAsync(context, passwordHasher, "piotr@test.pl", "Piotr", "Zieliński");

        var piotrClient = await context.Clients.FirstOrDefaultAsync(c => c.Email == "piotr@test.pl");
        if (piotrClient is null)
        {
            await context.Clients.AddAsync(new Client
            {
                UserId = piotrUser.Id,
                FirstName = "Piotr",
                LastName = "Zieliński",
                Email = "piotr@test.pl",
                PhoneNumber = "444555666",
                TrainerId = trainer.Id,
                ActivePackageId = package.Id,
                LocationId = defaultLocation.Id,
                Goal = "Masa mięśniowa",
                Notes = "Klient testowy",
                Status = "Active",
                ProgressPercent = 50,
                BillingStatus = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });
        }
        else if (piotrClient.UserId is null)
        {
            piotrClient.UserId = piotrUser.Id;
            piotrClient.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedMainSessionsAsync(StudioCRMDbContext context)
    {
        if (await context.Sessions.AnyAsync(s => s.Title == "Trening FBW" || s.Title == "Trening siłowy"))
            return;

        var trainer = await context.Trainers
            .Include(t => t.User)
            .FirstAsync(t => t.User.Email == "trainer@studiocrm.local");

        var client = await context.Clients.FirstAsync(c => c.Email == "anna@test.pl");
        var package = await context.Packages.OrderBy(p => p.Id).FirstAsync();

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
                StudioRoom = "Sala 1",
                LocationId = client.LocationId,
                Status = "Planned",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            },
            new Session
            {
                Title = "Trening siłowy",
                StartAt = DateTime.UtcNow.AddDays(2),
                EndAt = DateTime.UtcNow.AddDays(2).AddHours(1),
                TrainerId = trainer.Id,
                ClientId = client.Id,
                PackageId = package.Id,
                StudioRoom = "Sala 2",
                LocationId = client.LocationId,
                Status = "Planned",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            }
        };

        await context.Sessions.AddRangeAsync(sessions);
        await context.SaveChangesAsync();
    }

    private static async Task SeedExtraTrainersAsync(StudioCRMDbContext context, PasswordHasher<User> passwordHasher)
    {
        var trainerRole = await context.Roles.FirstAsync(r => r.Name == "Trainer");
        var locations = await context.Locations.OrderBy(l => l.Id).ToListAsync();

        for (int i = 1; i <= 3; i++)
        {
            var email = $"trainer{i}@test.pl";

            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
            {
                user = new User
                {
                    Email = email,
                    FirstName = $"Trainer{i}",
                    LastName = "Test",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                user.PasswordHash = passwordHasher.HashPassword(user, "Test123!");

                await context.Users.AddAsync(user);
                await context.SaveChangesAsync();
            }

            if (!await context.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == trainerRole.Id))
            {
                await context.UserRoles.AddAsync(new UserRole
                {
                    UserId = user.Id,
                    RoleId = trainerRole.Id
                });

                await context.SaveChangesAsync();
            }

            var trainer = await context.Trainers.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (trainer is null)
            {
                trainer = new Trainer
                {
                    UserId = user.Id,
                    Bio = $"Trener testowy {i}",
                    Phone = $"50000000{i}",
                    AvatarUrl = null,
                    Status = "Active",
                    ExperienceYears = Random.Shared.Next(1, 10),
                    HourlyRate = Random.Shared.Next(80, 200),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = 1
                };

                await context.Trainers.AddAsync(trainer);
                await context.SaveChangesAsync();
            }

            var existingLocationIds = await context.TrainerLocations
                .Where(tl => tl.TrainerId == trainer.Id)
                .Select(tl => tl.LocationId)
                .ToListAsync();

            if (!existingLocationIds.Any())
            {
                var assignedLocations = locations
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(Math.Min(2, locations.Count))
                    .ToList();

                foreach (var loc in assignedLocations)
                {
                    await context.TrainerLocations.AddAsync(new TrainerLocation
                    {
                        TrainerId = trainer.Id,
                        LocationId = loc.Id
                    });
                }

                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task SeedExtraClientsAsync(StudioCRMDbContext context, PasswordHasher<User> passwordHasher)
    {
        var packages = await context.Packages.OrderBy(p => p.Id).ToListAsync();
        var locations = await context.Locations.OrderBy(l => l.Id).ToListAsync();
        var trainers = await context.Trainers
            .Include(t => t.TrainerLocations)
            .ToListAsync();

        var clientsToAdd = new List<Client>();

        for (int i = 1; i <= 10; i++)
        {
            var email = $"client{i}@test.pl";

            var user = await EnsureClientUserAsync(
                context,
                passwordHasher,
                email,
                $"Client{i}",
                "Test");

            var existingClient = await context.Clients.FirstOrDefaultAsync(c => c.Email == email);

            if (existingClient is not null)
            {
                if (existingClient.UserId is null)
                {
                    existingClient.UserId = user.Id;
                    existingClient.UpdatedAt = DateTime.UtcNow;
                }

                continue;
            }

            var location = locations[Random.Shared.Next(locations.Count)];

            var trainersInLocation = trainers
                .Where(t => t.TrainerLocations.Any(tl => tl.LocationId == location.Id))
                .ToList();

            var trainer = trainersInLocation.Any()
                ? trainersInLocation[Random.Shared.Next(trainersInLocation.Count)]
                : trainers[Random.Shared.Next(trainers.Count)];

            var package = packages[Random.Shared.Next(packages.Count)];

            clientsToAdd.Add(new Client
            {
                UserId = user.Id,
                FirstName = $"Client{i}",
                LastName = "Test",
                Email = email,
                PhoneNumber = $"6000000{i:00}",
                TrainerId = trainer.Id,
                ActivePackageId = package.Id,
                LocationId = location.Id,
                Goal = RandomGoal(),
                Notes = "Wygenerowany klient testowy",
                ProgressPercent = Random.Shared.Next(5, 95),
                BillingStatus = RandomBillingStatus(),
                Status = RandomClientStatus(),
                NextSessionAt = DateTime.UtcNow.AddDays(Random.Shared.Next(1, 14)),
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 60)),
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });
        }

        if (clientsToAdd.Any())
        {
            await context.Clients.AddRangeAsync(clientsToAdd);
        }

        await context.SaveChangesAsync();
    }

    private static async Task LinkExistingClientsToUsersAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher)
    {
        var clients = await context.Clients
            .Where(c => c.UserId == null)
            .ToListAsync();

        foreach (var client in clients)
        {
            if (string.IsNullOrWhiteSpace(client.Email))
                continue;

            var user = await EnsureClientUserAsync(
                context,
                passwordHasher,
                client.Email,
                client.FirstName,
                client.LastName);

            client.UserId = user.Id;
            client.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    private static async Task<User> EnsureClientUserAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher,
        string email,
        string firstName,
        string lastName)
    {
        var clientRole = await context.Roles.FirstAsync(r => r.Name == "Client");

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash = passwordHasher.HashPassword(user, "Client123!");

            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        if (!await context.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == clientRole.Id))
        {
            await context.UserRoles.AddAsync(new UserRole
            {
                UserId = user.Id,
                RoleId = clientRole.Id
            });

            await context.SaveChangesAsync();
        }

        return user;
    }

    private static async Task SeedExtraSessionsAsync(StudioCRMDbContext context)
    {
        var existingExtraSessions = await context.Sessions.CountAsync(s => s.CreatedBy == 1);
        if (existingExtraSessions >= 50)
            return;

        var trainers = await context.Trainers
            .Include(t => t.TrainerLocations)
            .ToListAsync();

        var clients = await context.Clients.ToListAsync();
        var packages = await context.Packages.ToListAsync();

        var sessionsToAdd = new List<Session>();

        for (int i = 0; i < 60; i++)
        {
            var trainer = trainers[Random.Shared.Next(trainers.Count)];
            var trainerLocationIds = trainer.TrainerLocations.Select(tl => tl.LocationId).ToList();

            if (!trainerLocationIds.Any())
                continue;

            var compatibleClients = clients
                .Where(c => c.LocationId != 0 && trainerLocationIds.Contains(c.LocationId))
                .ToList();

            if (!compatibleClients.Any())
                continue;

            var client = compatibleClients[Random.Shared.Next(compatibleClients.Count)];
            var package = packages[Random.Shared.Next(packages.Count)];
            var start = RandomSessionStart();

            sessionsToAdd.Add(new Session
            {
                Title = RandomSessionName(),
                Note = "Wygenerowana sesja testowa",
                StartAt = start,
                EndAt = start.AddHours(1),
                TrainerId = trainer.Id,
                ClientId = client.Id,
                PackageId = package.Id,
                StudioRoom = $"Sala {Random.Shared.Next(1, 5)}",
                LocationId = client.LocationId,
                Status = RandomSessionStatus(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });
        }

        if (sessionsToAdd.Any())
        {
            await context.Sessions.AddRangeAsync(sessionsToAdd);
            await context.SaveChangesAsync();
        }
    }

    private static string RandomSessionName()
    {
        var names = new[]
        {
            "Trening FBW",
            "Trening siłowy",
            "Cardio",
            "Mobilność",
            "HIIT",
            "Rehabilitacja",
            "Core",
            "Stretching",
            "Trening personalny",
            "Trening funkcjonalny"
        };

        return names[Random.Shared.Next(names.Length)];
    }

    private static string RandomSessionStatus()
    {
        var statuses = new[]
        {
            "Planned",
            "Completed",
            "Cancelled"
        };

        return statuses[Random.Shared.Next(statuses.Length)];
    }

    private static string RandomBillingStatus()
    {
        var statuses = new[]
        {
            "Paid",
            "Pending",
            "Overdue"
        };

        return statuses[Random.Shared.Next(statuses.Length)];
    }

    private static string RandomClientStatus()
    {
        var statuses = new[]
        {
            "Active",
            "New",
            "Suspended"
        };

        return statuses[Random.Shared.Next(statuses.Length)];
    }

    private static string RandomGoal()
    {
        var goals = new[]
        {
            "Redukcja",
            "Masa mięśniowa",
            "Poprawa kondycji",
            "Powrót po kontuzji",
            "Mobilność",
            "Sylwetka"
        };

        return goals[Random.Shared.Next(goals.Length)];
    }

    private static DateTime RandomSessionStart()
    {
        return DateTime.UtcNow.Date
            .AddDays(Random.Shared.Next(-14, 14))
            .AddHours(Random.Shared.Next(6, 21));
    }
}