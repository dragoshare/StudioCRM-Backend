using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Seed;

public static class DataSeeder
{
    private const string ClientPassword = "Client123!";
    private const string TrainerPassword = "Trainer123!";
    private const string OwnerPassword = "Admin123!";
    private const string SeedNotePrefix = "SEED:";

    public static async Task SeedAsync(StudioCRMDbContext context)
    {
        await context.Database.MigrateAsync();

        var passwordHasher = new PasswordHasher<User>();

        await SeedRolesAsync(context);
        await SeedLocationsAsync(context);
        await SeedPackagesAsync(context);
        await SeedOwnerAsync(context, passwordHasher);
        await SeedTrainersAsync(context, passwordHasher);
        await SeedTrainerRatesAsync(context);
        await SeedClientsAsync(context, passwordHasher);
        await SeedSessionsAsync(context);
    }

    private static async Task SeedRolesAsync(StudioCRMDbContext context)
    {
        var roles = new[] { "Owner", "Trainer", "Client" };

        foreach (var roleName in roles)
        {
            if (!await context.Roles.AnyAsync(r => r.Name == roleName))
            {
                await context.Roles.AddAsync(new Role
                {
                    Name = roleName
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedLocationsAsync(StudioCRMDbContext context)
    {
        await EnsureLocationAsync(context, "Niepołomice", "Niepołomice", "ul. Bocheńska 12");
        await EnsureLocationAsync(context, "Kłaj", "Kłaj", "ul. Sportowa 4");
    }

    private static async Task EnsureLocationAsync(
        StudioCRMDbContext context,
        string name,
        string city,
        string address)
    {
        var location = await context.Locations.FirstOrDefaultAsync(l => l.Name == name);

        if (location is null)
        {
            await context.Locations.AddAsync(new Location
            {
                Name = name,
                City = city,
                Address = address,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedPackagesAsync(StudioCRMDbContext context)
    {
        await EnsurePackageAsync(context, "4 treningi 1:1", "Pakiet startowy treningów personalnych", 600, 4, 30);
        await EnsurePackageAsync(context, "8 treningów 1:1", "Najpopularniejszy pakiet treningów personalnych", 1120, 8, 35);
        await EnsurePackageAsync(context, "12 treningów 1:1", "Pakiet premium treningów personalnych", 1560, 12, 45);

        await EnsurePackageAsync(context, "8 treningów 2:1", "Pakiet treningów półpersonalnych dla 2 osób", 760, 8, 35);
        await EnsurePackageAsync(context, "12 treningów 2:1", "Pakiet treningów półpersonalnych dla 2 osób", 1080, 12, 45);

        await EnsurePackageAsync(context, "8 treningów 3:1", "Pakiet treningów półpersonalnych dla 3 osób", 640, 8, 35);
        await EnsurePackageAsync(context, "12 treningów 3:1", "Pakiet treningów półpersonalnych dla 3 osób", 900, 12, 45);

        await EnsurePackageAsync(context, "8 treningów 4:1", "Pakiet treningów półpersonalnych dla 4 osób", 560, 8, 35);
        await EnsurePackageAsync(context, "12 treningów 4:1", "Pakiet treningów półpersonalnych dla 4 osób", 780, 12, 45);
    }

    private static async Task EnsurePackageAsync(
        StudioCRMDbContext context,
        string name,
        string description,
        decimal price,
        int sessionsLimit,
        int durationDays)
    {
        var package = await context.Packages.FirstOrDefaultAsync(p => p.Name == name);

        if (package is null)
        {
            await context.Packages.AddAsync(new Package
            {
                Name = name,
                Description = description,
                Price = price,
                Currency = "PLN",
                SessionsLimit = sessionsLimit,
                DurationDays = durationDays,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedOwnerAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher)
    {
        var owner = await EnsureUserAsync(
            context,
            passwordHasher,
            "owner@studiocrm.local",
            "System",
            "Owner",
            OwnerPassword);

        await EnsureUserRoleAsync(context, owner.Id, "Owner");
    }

    private static async Task SeedTrainersAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher)
    {
        var trainerSeeds = new[]
        {
            new
            {
                Email = "trainer@studiocrm.local",
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Trener siłowy i motoryczny",
                Phone = "501100100",
                Experience = 5,
                Locations = new[] { "Niepołomice", "Kłaj" }
            },
            new
            {
                Email = "adam.trener@studiocrm.local",
                FirstName = "Adam",
                LastName = "Nowak",
                Bio = "Trener przygotowania motorycznego",
                Phone = "501100101",
                Experience = 7,
                Locations = new[] { "Niepołomice" }
            },
            new
            {
                Email = "karolina.trener@studiocrm.local",
                FirstName = "Karolina",
                LastName = "Wójcik",
                Bio = "Trenerka sylwetkowa i funkcjonalna",
                Phone = "501100102",
                Experience = 3,
                Locations = new[] { "Kłaj" }
            },
            new
            {
                Email = "bartek.trener@studiocrm.local",
                FirstName = "Bartosz",
                LastName = "Zieliński",
                Bio = "Trener personalny i rehabilitacyjny",
                Phone = "501100103",
                Experience = 6,
                Locations = new[] { "Niepołomice", "Kłaj" }
            }
        };

        foreach (var seed in trainerSeeds)
        {
            var user = await EnsureUserAsync(
                context,
                passwordHasher,
                seed.Email,
                seed.FirstName,
                seed.LastName,
                TrainerPassword);

            await EnsureUserRoleAsync(context, user.Id, "Trainer");

            var trainer = await context.Trainers.FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (trainer is null)
            {
                trainer = new Trainer
                {
                    UserId = user.Id,
                    Bio = seed.Bio,
                    Phone = seed.Phone,
                    AvatarUrl = null,
                    Status = "Active",
                    ExperienceYears = seed.Experience,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = 1
                };

                await context.Trainers.AddAsync(trainer);
                await context.SaveChangesAsync();
            }

            foreach (var locationName in seed.Locations)
            {
                var location = await context.Locations.FirstAsync(l => l.Name == locationName);

                var exists = await context.TrainerLocations.AnyAsync(tl =>
                    tl.TrainerId == trainer.Id &&
                    tl.LocationId == location.Id);

                if (!exists)
                {
                    await context.TrainerLocations.AddAsync(new TrainerLocation
                    {
                        TrainerId = trainer.Id,
                        LocationId = location.Id
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedTrainerRatesAsync(StudioCRMDbContext context)
    {
        var trainerRates = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["trainer@studiocrm.local"] = new()
            {
                ["OneToOne"] = 70m,
                ["TwoToOne"] = 85m,
                ["ThreeToOne"] = 95m,
                ["FourToOne"] = 105m
            },
            ["adam.trener@studiocrm.local"] = new()
            {
                ["OneToOne"] = 80m,
                ["TwoToOne"] = 95m,
                ["ThreeToOne"] = 105m,
                ["FourToOne"] = 115m
            },
            ["karolina.trener@studiocrm.local"] = new()
            {
                ["OneToOne"] = 60m,
                ["TwoToOne"] = 75m,
                ["ThreeToOne"] = 85m,
                ["FourToOne"] = 95m
            },
            ["bartek.trener@studiocrm.local"] = new()
            {
                ["OneToOne"] = 75m,
                ["TwoToOne"] = 90m,
                ["ThreeToOne"] = 100m,
                ["FourToOne"] = 110m
            }
        };

        foreach (var trainerSeed in trainerRates)
        {
            var trainer = await context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == trainerSeed.Key);

            if (trainer is null)
                continue;

            foreach (var rateSeed in trainerSeed.Value)
            {
                var exists = await context.TrainerRates.AnyAsync(r =>
                    r.TrainerId == trainer.Id &&
                    r.SessionType == rateSeed.Key &&
                    r.IsActive);

                if (exists)
                    continue;

                await context.TrainerRates.AddAsync(new TrainerRate
                {
                    TrainerId = trainer.Id,
                    SessionType = rateSeed.Key,
                    Rate = rateSeed.Value,
                    ValidFrom = DateTime.UtcNow.AddMonths(-6),
                    ValidTo = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedClientsAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher)
    {
        var clients = new[]
        {
            new { Email = "anna.nowak@test.pl", FirstName = "Anna", LastName = "Nowak", Phone = "600100001", Goal = "Redukcja tkanki tłuszczowej", Location = "Niepołomice", TrainerEmail = "trainer@studiocrm.local", Package = "12 treningów 1:1", Billing = "Paid", Progress = 65 },
            new { Email = "piotr.zielinski@test.pl", FirstName = "Piotr", LastName = "Zieliński", Phone = "600100002", Goal = "Budowa masy mięśniowej", Location = "Niepołomice", TrainerEmail = "trainer@studiocrm.local", Package = "8 treningów 1:1", Billing = "Pending", Progress = 45 },
            new { Email = "kasia.wojcik@test.pl", FirstName = "Kasia", LastName = "Wójcik", Phone = "600100003", Goal = "Poprawa kondycji", Location = "Kłaj", TrainerEmail = "karolina.trener@studiocrm.local", Package = "8 treningów 2:1", Billing = "Paid", Progress = 72 },
            new { Email = "michal.lis@test.pl", FirstName = "Michał", LastName = "Lis", Phone = "600100004", Goal = "Siła i hipertrofia", Location = "Kłaj", TrainerEmail = "bartek.trener@studiocrm.local", Package = "12 treningów 2:1", Billing = "Paid", Progress = 38 },
            new { Email = "agnieszka.kaczmarek@test.pl", FirstName = "Agnieszka", LastName = "Kaczmarek", Phone = "600100005", Goal = "Powrót po kontuzji kolana", Location = "Niepołomice", TrainerEmail = "adam.trener@studiocrm.local", Package = "8 treningów 1:1", Billing = "Overdue", Progress = 25 },
            new { Email = "pawel.mazur@test.pl", FirstName = "Paweł", LastName = "Mazur", Phone = "600100006", Goal = "Redukcja i zdrowe plecy", Location = "Niepołomice", TrainerEmail = "adam.trener@studiocrm.local", Package = "12 treningów 3:1", Billing = "Paid", Progress = 55 },
            new { Email = "ewa.krol@test.pl", FirstName = "Ewa", LastName = "Król", Phone = "600100007", Goal = "Mobilność i sylwetka", Location = "Kłaj", TrainerEmail = "karolina.trener@studiocrm.local", Package = "8 treningów 3:1", Billing = "Pending", Progress = 41 },
            new { Email = "tomasz.wrona@test.pl", FirstName = "Tomasz", LastName = "Wrona", Phone = "600100008", Goal = "Przygotowanie do biegu", Location = "Kłaj", TrainerEmail = "bartek.trener@studiocrm.local", Package = "8 treningów 2:1", Billing = "Paid", Progress = 80 },
            new { Email = "magda.lewandowska@test.pl", FirstName = "Magda", LastName = "Lewandowska", Phone = "600100009", Goal = "Poprawa siły", Location = "Niepołomice", TrainerEmail = "trainer@studiocrm.local", Package = "4 treningi 1:1", Billing = "Paid", Progress = 30 },
            new { Email = "rafal.sikora@test.pl", FirstName = "Rafał", LastName = "Sikora", Phone = "600100010", Goal = "Redukcja", Location = "Kłaj", TrainerEmail = "bartek.trener@studiocrm.local", Package = "12 treningów 1:1", Billing = "Pending", Progress = 50 },
            new { Email = "ola.michalska@test.pl", FirstName = "Ola", LastName = "Michalska", Phone = "600100011", Goal = "Trening po ciąży", Location = "Niepołomice", TrainerEmail = "karolina.trener@studiocrm.local", Package = "8 treningów 1:1", Billing = "Paid", Progress = 60 },
            new { Email = "dominik.sobczak@test.pl", FirstName = "Dominik", LastName = "Sobczak", Phone = "600100012", Goal = "Masa mięśniowa", Location = "Kłaj", TrainerEmail = "trainer@studiocrm.local", Package = "12 treningów 2:1", Billing = "Paid", Progress = 68 }
        };

        foreach (var seed in clients)
        {
            var user = await EnsureUserAsync(
                context,
                passwordHasher,
                seed.Email,
                seed.FirstName,
                seed.LastName,
                ClientPassword);

            await EnsureUserRoleAsync(context, user.Id, "Client");

            var trainer = await context.Trainers
                .Include(t => t.User)
                .FirstAsync(t => t.User.Email == seed.TrainerEmail);

            var location = await context.Locations.FirstAsync(l => l.Name == seed.Location);
            var package = await context.Packages.FirstAsync(p => p.Name == seed.Package);

            var client = await context.Clients.FirstOrDefaultAsync(c => c.Email == seed.Email);

            if (client is null)
            {
                client = new Client
                {
                    UserId = user.Id,
                    FirstName = seed.FirstName,
                    LastName = seed.LastName,
                    Email = seed.Email,
                    PhoneNumber = seed.Phone,
                    TrainerId = trainer.Id,
                    ActivePackageId = package.Id,
                    LocationId = location.Id,
                    Goal = seed.Goal,
                    Notes = "Klient testowy powiązany z kontem, trenerem, lokalizacją i pakietem.",
                    Status = "Active",
                    ProgressPercent = seed.Progress,
                    BillingStatus = seed.Billing,
                    NextSessionAt = DateTime.UtcNow.AddDays(Random.Shared.Next(1, 10)),
                    CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(20, 120)),
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = 1
                };

                await context.Clients.AddAsync(client);
            }
            else
            {
                client.UserId = user.Id;
                client.TrainerId = trainer.Id;
                client.ActivePackageId = package.Id;
                client.LocationId = location.Id;
                client.Status = "Active";
                client.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSessionsAsync(StudioCRMDbContext context)
    {
        var existingSeedSessions = await context.Sessions
            .IgnoreQueryFilters()
            .CountAsync(s => s.Note != null && s.Note.StartsWith(SeedNotePrefix));

        if (existingSeedSessions >= 70)
            return;

        var trainers = await context.Trainers
            .Include(t => t.User)
            .Include(t => t.TrainerLocations)
            .ToListAsync();

        var clients = await context.Clients
            .Include(c => c.ActivePackage)
            .ToListAsync();

        if (!trainers.Any() || !clients.Any())
            return;

        var startDate = DateTime.UtcNow.Date.AddDays(-21);

        for (int day = 0; day < 42; day++)
        {
            var date = startDate.AddDays(day);

            if (date.DayOfWeek == DayOfWeek.Sunday)
                continue;

            var sessionsPerDay = Random.Shared.Next(2, 5);

            for (int slot = 0; slot < sessionsPerDay; slot++)
            {
                var trainer = trainers[Random.Shared.Next(trainers.Count)];

                var trainerLocationIds = trainer.TrainerLocations
                    .Select(tl => tl.LocationId)
                    .ToList();

                if (!trainerLocationIds.Any())
                    continue;

                var locationId = trainerLocationIds[Random.Shared.Next(trainerLocationIds.Count)];

                var compatibleClients = clients
                    .Where(c => c.LocationId == locationId && c.Status == "Active")
                    .OrderBy(_ => Guid.NewGuid())
                    .ToList();

                if (!compatibleClients.Any())
                    continue;

                var participantsCount = Random.Shared.Next(1, 101) switch
                {
                    <= 55 => 1,
                    <= 82 => 2,
                    <= 95 => 3,
                    _ => 4
                };

                var selectedClients = compatibleClients
                    .Take(Math.Min(participantsCount, compatibleClients.Count))
                    .ToList();

                if (!selectedClients.Any())
                    continue;

                var startHour = new[] { 7, 8, 9, 10, 15, 16, 17, 18, 19, 20 }[Random.Shared.Next(10)];
                var start = date.AddHours(startHour);
                var end = start.AddHours(1);

                var isPast = start < DateTime.UtcNow;
                var status = isPast ? RandomPastSessionStatus() : "Planned";

                var plannedType = ResolveSessionType(selectedClients.Count);

                var actualPresentCount = status == "Completed"
                    ? Math.Max(1, selectedClients.Count(c => Random.Shared.Next(1, 101) <= 85))
                    : (int?)null;

                var session = new Session
                {
                    Title = BuildSessionTitle(selectedClients),
                    Note = $"{SeedNotePrefix} Sesja testowa {plannedType}.",
                    StartAt = start,
                    EndAt = end,
                    TrainerId = trainer.Id,
                    LocationId = locationId,
                    StudioRoom = $"Sala {Random.Shared.Next(1, 4)}",
                    Status = status,
                    PlannedSessionType = plannedType,
                    ActualSessionType = status == "Completed"
                        ? ResolveSessionType(actualPresentCount ?? selectedClients.Count)
                        : null,
                    ActualParticipantsCount = status == "Completed"
                        ? actualPresentCount
                        : null,
                    CompletedAt = status == "Completed"
                        ? end.AddMinutes(Random.Shared.Next(5, 90))
                        : null,
                    CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(10, 90)),
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = 1,
                    IsDeleted = false
                };

                await context.Sessions.AddAsync(session);
                await context.SaveChangesAsync();

                foreach (var client in selectedClients)
                {
                    var attendance = status switch
                    {
                        "Completed" => RandomAttendance(),
                        "Cancelled" => "CancelledInTime",
                        _ => "Planned"
                    };

                    await context.SessionParticipants.AddAsync(new SessionParticipant
                    {
                        SessionId = session.Id,
                        ClientId = client.Id,
                        PackageId = client.ActivePackageId,
                        AttendanceStatus = attendance,
                        CountsAgainstPackage = attendance is "Present" or "NoShow" or "CancelledLate",
                        SessionsCharged = attendance is "Present" or "NoShow" or "CancelledLate" ? 1 : 0,
                        Note = $"{SeedNotePrefix} Uczestnik sesji testowej.",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task<User> EnsureUserAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher,
        string email,
        string firstName,
        string lastName,
        string password)
    {
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

            user.PasswordHash = passwordHasher.HashPassword(user, password);

            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }
        else
        {
            user.FirstName = firstName;
            user.LastName = lastName;
            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        return user;
    }

    private static async Task EnsureUserRoleAsync(
        StudioCRMDbContext context,
        int userId,
        string roleName)
    {
        var role = await context.Roles.FirstAsync(r => r.Name == roleName);

        var exists = await context.UserRoles.AnyAsync(ur =>
            ur.UserId == userId &&
            ur.RoleId == role.Id);

        if (!exists)
        {
            await context.UserRoles.AddAsync(new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            });

            await context.SaveChangesAsync();
        }
    }

    private static string RandomPastSessionStatus()
    {
        return Random.Shared.Next(1, 101) switch
        {
            <= 75 => "Completed",
            <= 90 => "Cancelled",
            _ => "Planned"
        };
    }

    private static string RandomAttendance()
    {
        return Random.Shared.Next(1, 101) switch
        {
            <= 82 => "Present",
            <= 93 => "NoShow",
            <= 97 => "CancelledLate",
            _ => "CancelledInTime"
        };
    }

    private static string ResolveSessionType(int count)
    {
        return count switch
        {
            1 => "OneToOne",
            2 => "TwoToOne",
            3 => "ThreeToOne",
            _ => "FourToOne"
        };
    }

    private static string BuildSessionTitle(List<Client> clients)
    {
        var ordered = clients
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .ToList();

        if (ordered.Count == 1)
            return ShortClientName(ordered[0]);

        if (ordered.Count == 2)
            return $"{ShortClientName(ordered[0])} + {ShortClientName(ordered[1])}";

        return $"{ShortClientName(ordered[0])} + {ordered.Count - 1} os.";
    }

    private static string ShortClientName(Client client)
    {
        var initial = string.IsNullOrWhiteSpace(client.LastName)
            ? string.Empty
            : client.LastName[0].ToString();

        return $"{client.FirstName} {initial}".Trim();
    }
}