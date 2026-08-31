using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using UserRoleEntity = StudioCRM.Domain.Entities.UserRole;

namespace StudioCRM.Infrastructure.Seed;

public static class DataSeeder
{
    private static readonly HashSet<string> ProtectedTrainerSeedEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "sgorzula@bsworkout.pl"
    };

    private const string ClientPassword = "Client123!";
    private const string TrainerPassword = "Trainer123!";
    private const string OwnerPassword = "Admin123!";
    private const string SeedNotePrefix = "SEED:";
    private const string BillingSeedNotePrefix = "SEED:BILLING:";
    private const string SettlementSeedNotePrefix = "SEED:SETTLEMENT:";

    public static async Task SeedAsync(StudioCRMDbContext context, bool seedDemoData)
    {
        await context.Database.MigrateAsync();

        var passwordHasher = new PasswordHasher<User>();

        await SeedRolesAsync(context);

        if (!seedDemoData)
            return;

        await EnsureOwnerTrainerProfilesAsync(context);
        await CleanupGeneratedDemoDataAsync(context);
        await SeedLocationsAsync(context);
        await SeedPackagesAsync(context);
        await SeedOwnerAsync(context, passwordHasher);
        await SeedTrainersAsync(context, passwordHasher);
        await SeedTrainerRatesAsync(context);
        await SeedClientsAsync(context, passwordHasher);
        await SeedBillingTestScenariosAsync(context, passwordHasher);
        await SeedTrainerSettlementTestDataAsync(context);
    }

    private static async Task CleanupGeneratedDemoDataAsync(StudioCRMDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            DELETE FROM "ClientBalanceTransactions"
            WHERE "SessionId" IN (
                SELECT "Id"
                FROM "Sessions"
                WHERE "Note" LIKE 'SEED:%'
            );

            DELETE FROM "CalendarEventLinks"
            WHERE "ExternalEventId" LIKE 'seed-outlook-%'
               OR "SessionId" IN (
                    SELECT "Id"
                    FROM "Sessions"
                    WHERE "Note" LIKE 'SEED:%'
               );

            DELETE FROM "SessionParticipants"
            WHERE "Note" LIKE 'SEED:%'
               OR "SessionId" IN (
                    SELECT "Id"
                    FROM "Sessions"
                    WHERE "Note" LIKE 'SEED:%'
               );

            DELETE FROM "Sessions"
            WHERE "Note" LIKE 'SEED:%';

            DELETE FROM "ExternalCalendarEvents"
            WHERE "ExternalEventId" LIKE 'seed-outlook-%'
               OR "BodyPreview" LIKE 'SEED:%';

            DELETE FROM "CalendarSubscriptions"
            WHERE "CalendarIntegrationId" IN (
                SELECT "Id"
                FROM "CalendarIntegrations"
                WHERE "ExternalUserId" = 'seed-outlook-user-trainer'
            );

            DELETE FROM "CalendarIntegrations"
            WHERE "ExternalUserId" = 'seed-outlook-user-trainer';
            """);
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

    private static async Task EnsureOwnerTrainerProfilesAsync(StudioCRMDbContext context)
    {
        var ownerUsers = await context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == "Owner"))
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var ownerUserId in ownerUsers)
        {
            await EnsureUserRoleAsync(context, ownerUserId, "Trainer");
            await EnsureOwnerTrainerProfileAsync(context, ownerUserId);
        }
    }

    private static async Task SeedLocationsAsync(StudioCRMDbContext context)
    {
        var niepolomiceLegalEntity = await EnsureLegalEntityAsync(
            context,
            "BS Workout Niepołomice",
            "ul. Bocheńska 12, Niepołomice",
            "BS Workout Niepołomice",
            "12 3456 7890 1234 5678 9012 3456",
            "501100098",
            "Pakiet {PackageName} - {ClientFullName}",
            "W tytule przelewu wpisz nazwę pakietu i swoje imię oraz nazwisko.");

        var klajLegalEntity = await EnsureLegalEntityAsync(
            context,
            "BS Workout Kłaj",
            "ul. Sportowa 4, Kłaj",
            "BS Workout Kłaj",
            "98 7654 3210 9876 5432 1098 7654",
            "501100098",
            "Pakiet {PackageName} - {ClientFullName}",
            "W tytule przelewu wpisz nazwę pakietu i swoje imię oraz nazwisko.");

        await EnsureLocationAsync(
            context,
            "Niepołomice",
            "Niepołomice",
            "ul. Bocheńska 12",
            "niepolomice8_studio@bsworkout.pl",
            niepolomiceLegalEntity.Id,
            "BS Workout Niepołomice",
            "12 3456 7890 1234 5678 9012 3456",
            "501100098",
            "Pakiet {PackageName} - {ClientFullName}",
            "W tytule przelewu wpisz nazwę pakietu i swoje imię oraz nazwisko.");

        await EnsureLocationAsync(
            context,
            "Kłaj",
            "Kłaj",
            "ul. Sportowa 4",
            "klaj237_studio@bsworkout.pl",
            klajLegalEntity.Id,
            "BS Workout Kłaj",
            "98 7654 3210 9876 5432 1098 7654",
            "501100098",
            "Pakiet {PackageName} - {ClientFullName}",
            "W tytule przelewu wpisz nazwę pakietu i swoje imię oraz nazwisko.");
    }

    private static async Task<LegalEntity> EnsureLegalEntityAsync(
        StudioCRMDbContext context,
        string name,
        string address,
        string paymentRecipientName,
        string bankAccountNumber,
        string blikPhoneNumber,
        string transferTitleTemplate,
        string paymentDescription)
    {
        var legalEntity = await context.LegalEntities.FirstOrDefaultAsync(l => l.Name == name);

        if (legalEntity is null)
        {
            legalEntity = new LegalEntity
            {
                Name = name,
                Address = address,
                PaymentRecipientName = paymentRecipientName,
                BankAccountNumber = bankAccountNumber,
                BlikPhoneNumber = blikPhoneNumber,
                TransferTitleTemplate = transferTitleTemplate,
                PaymentDescription = paymentDescription,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await context.LegalEntities.AddAsync(legalEntity);
        }
        else
        {
            legalEntity.Address = address;
            legalEntity.PaymentRecipientName = paymentRecipientName;
            legalEntity.BankAccountNumber = bankAccountNumber;
            legalEntity.BlikPhoneNumber = blikPhoneNumber;
            legalEntity.TransferTitleTemplate = transferTitleTemplate;
            legalEntity.PaymentDescription = paymentDescription;
            legalEntity.IsActive = true;
            legalEntity.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        return legalEntity;
    }

    private static async Task EnsureLocationAsync(
        StudioCRMDbContext context,
        string name,
        string city,
        string address,
        string calendarEmail,
        int legalEntityId,
        string paymentRecipientName,
        string bankAccountNumber,
        string blikPhoneNumber,
        string transferTitleTemplate,
        string paymentDescription)
    {
        var location = await context.Locations.FirstOrDefaultAsync(l => l.Name == name);

        if (location is null)
        {
            await context.Locations.AddAsync(new Location
            {
                Name = name,
                City = city,
                Address = address,
                CalendarEmail = calendarEmail,
                LegalEntityId = legalEntityId,
                PaymentRecipientName = paymentRecipientName,
                BankAccountNumber = bankAccountNumber,
                BlikPhoneNumber = blikPhoneNumber,
                TransferTitleTemplate = transferTitleTemplate,
                PaymentDescription = paymentDescription,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            location.City = city;
            location.Address = address;
            location.CalendarEmail = calendarEmail;
            location.LegalEntityId = legalEntityId;
            location.PaymentRecipientName = paymentRecipientName;
            location.BankAccountNumber = bankAccountNumber;
            location.BlikPhoneNumber = blikPhoneNumber;
            location.TransferTitleTemplate = transferTitleTemplate;
            location.PaymentDescription = paymentDescription;
            location.IsActive = true;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPackagesAsync(StudioCRMDbContext context)
    {
        await EnsurePackageAsync(context, "4 treningi 1:1", "Pakiet startowy treningów personalnych", 600, 4, 45);
        await EnsurePackageAsync(context, "8 treningów 1:1", "Najpopularniejszy pakiet treningów personalnych", 1120, 8, 45);
        await EnsurePackageAsync(context, "12 treningów 1:1", "Pakiet premium treningów personalnych", 1560, 12, 45);

        await EnsurePackageAsync(context, "8 treningów 2:1", "Pakiet treningów półpersonalnych dla 2 osób", 760, 8, 45);
        await EnsurePackageAsync(context, "12 treningów 2:1", "Pakiet treningów półpersonalnych dla 2 osób", 1080, 12, 45);

        await EnsurePackageAsync(context, "8 treningów 3:1", "Pakiet treningów półpersonalnych dla 3 osób", 640, 8, 45);
        await EnsurePackageAsync(context, "12 treningów 3:1", "Pakiet treningów półpersonalnych dla 3 osób", 900, 12, 45);

        await EnsurePackageAsync(context, "8 treningów 4:1", "Pakiet treningów półpersonalnych dla 4 osób", 560, 8, 45);
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
        var billingType = InferBillingType(name);
        var sessionsPerWeek = InferSessionsPerWeek(sessionsLimit);

        if (package is null)
        {
            await context.Packages.AddAsync(new Package
            {
                Name = name,
                Description = description,
                Price = price,
                Currency = "PLN",
                SessionsLimit = sessionsLimit,
                SessionsPerWeek = sessionsPerWeek,
                DurationDays = durationDays,
                BillingType = billingType,
                ParticipantsCount = (int)billingType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            });

            await context.SaveChangesAsync();
        }
        else
        {
            package.Description = description;
            package.Price = price;
            package.Currency = "PLN";
            package.SessionsLimit = sessionsLimit;
            package.SessionsPerWeek = sessionsPerWeek;
            package.DurationDays = durationDays;
            package.BillingType = billingType;
            package.ParticipantsCount = (int)billingType;
            package.IsActive = true;
            package.IsDeleted = false;
            package.DeletedAt = null;
            package.UpdatedAt = DateTime.UtcNow;

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
        await EnsureUserRoleAsync(context, owner.Id, "Trainer");
        await EnsureOwnerTrainerProfileAsync(context, owner.Id);
    }

    private static async Task EnsureOwnerTrainerProfileAsync(
        StudioCRMDbContext context,
        int ownerUserId)
    {
        var trainer = await context.Trainers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.UserId == ownerUserId);

        if (trainer is null)
        {
            trainer = new Trainer
            {
                UserId = ownerUserId,
                Bio = "Owner prowadzący własnych klientów",
                Phone = "501100098",
                Status = "Active",
                TeamJoinedDate = DateTime.UtcNow.Date,
                OutlookCategoryName = "Owner",
                OutlookCategoryColor = "preset6",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = ownerUserId
            };

            await context.Trainers.AddAsync(trainer);
        }

        trainer.IsDeleted = false;
        trainer.DeletedAt = null;
        trainer.Status = "Active";
        trainer.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        var locationIds = await context.Locations
            .Where(l => l.IsActive)
            .Select(l => l.Id)
            .ToListAsync();

        foreach (var locationId in locationIds)
        {
            var exists = await context.TrainerLocations.AnyAsync(tl =>
                tl.TrainerId == trainer.Id &&
                tl.LocationId == locationId);

            if (!exists)
            {
                await context.TrainerLocations.AddAsync(new TrainerLocation
                {
                    TrainerId = trainer.Id,
                    LocationId = locationId
                });
            }
        }

        await context.SaveChangesAsync();
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
                FirstName = "Marek",
                LastName = "Wójcik",
                Bio = "Główne konto trenera do testów CRM",
                Phone = "501100099",
                TeamJoinedDate = DateTime.UtcNow.Date.AddYears(-6),
                OutlookCategoryName = "Szymek",
                OutlookCategoryColor = "preset5",
                Locations = new[] { "Niepołomice", "Kłaj" }
            },
            new
            {
                Email = "sgorzula@bsworkout.pl",
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Trener siłowy i motoryczny",
                Phone = "501100100",
                TeamJoinedDate = DateTime.UtcNow.Date.AddYears(-5),
                OutlookCategoryName = "Jan Kowalski",
                OutlookCategoryColor = "preset7",
                Locations = new[] { "Niepołomice", "Kłaj" }
            },
            new
            {
                Email = "adam.trener@studiocrm.local",
                FirstName = "Adam",
                LastName = "Nowak",
                Bio = "Trener przygotowania motorycznego",
                Phone = "501100101",
                TeamJoinedDate = DateTime.UtcNow.Date.AddYears(-7),
                OutlookCategoryName = "Adam Nowak",
                OutlookCategoryColor = "preset4",
                Locations = new[] { "Niepołomice" }
            },
            new
            {
                Email = "karolina.trener@studiocrm.local",
                FirstName = "Karolina",
                LastName = "Wójcik",
                Bio = "Trenerka sylwetkowa i funkcjonalna",
                Phone = "501100102",
                TeamJoinedDate = DateTime.UtcNow.Date.AddYears(-3),
                OutlookCategoryName = "Karolina Wójcik",
                OutlookCategoryColor = "preset8",
                Locations = new[] { "Kłaj" }
            },
            new
            {
                Email = "bartek.trener@studiocrm.local",
                FirstName = "Bartosz",
                LastName = "Zieliński",
                Bio = "Trener personalny i rehabilitacyjny",
                Phone = "501100103",
                TeamJoinedDate = DateTime.UtcNow.Date.AddYears(-6),
                OutlookCategoryName = "Bartosz Zieliński",
                OutlookCategoryColor = "preset1",
                Locations = new[] { "Niepołomice", "Kłaj" }
            }
        };

        foreach (var seed in trainerSeeds)
        {
            var isProtectedTrainerSeed = ProtectedTrainerSeedEmails.Contains(seed.Email);

            var user = await EnsureUserAsync(
                context,
                passwordHasher,
                seed.Email,
                seed.FirstName,
                seed.LastName,
                TrainerPassword,
                updateExisting: !isProtectedTrainerSeed);

            await EnsureUserRoleAsync(context, user.Id, "Trainer");

            var trainer = await context.Trainers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            var isProtectedExistingTrainerSeed = isProtectedTrainerSeed && trainer is not null;

            if (trainer is null)
            {
                trainer = new Trainer
                {
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = 1
                };

                await context.Trainers.AddAsync(trainer);
            }

            if (!isProtectedExistingTrainerSeed)
            {
                trainer.Bio = seed.Bio;
                trainer.Phone = seed.Phone;
                trainer.Status = "Active";
                trainer.TeamJoinedDate = seed.TeamJoinedDate;
                trainer.OutlookCategoryName = seed.OutlookCategoryName;
                trainer.OutlookCategoryColor = seed.OutlookCategoryColor;
                trainer.IsDeleted = false;
                trainer.DeletedAt = null;
                trainer.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();

            if (isProtectedExistingTrainerSeed)
                continue;

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
        var trainerRates = new Dictionary<string, decimal>
        {
            ["trainer@studiocrm.local"] = 70m,
            ["adam.trener@studiocrm.local"] = 80m,
            ["karolina.trener@studiocrm.local"] = 60m,
            ["bartek.trener@studiocrm.local"] = 75m
        };

        foreach (var trainerSeed in trainerRates)
        {
            var trainer = await context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == trainerSeed.Key);

            if (trainer is null)
                continue;

            var exists = await context.TrainerRates.AnyAsync(r =>
                r.TrainerId == trainer.Id &&
                r.SessionType == "Hourly" &&
                r.IsActive);

            if (exists)
                continue;

            await context.TrainerRates.AddAsync(new TrainerRate
            {
                TrainerId = trainer.Id,
                SessionType = "Hourly",
                Rate = trainerSeed.Value,
                ValidFrom = DateTime.UtcNow.AddMonths(-6),
                ValidTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedClientsAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher)
    {
        var clients = new[]
        {
            new { Email = "anna.nowak@test.pl", FirstName = "Anna", LastName = "Nowak", Phone = "600100001", Goal = "Redukcja tkanki tłuszczowej", Location = "Niepołomice", TrainerEmail = "trainer@studiocrm.local", Package = "12 treningów 1:1", Billing = "Paid" },
            new { Email = "piotr.zielinski@test.pl", FirstName = "Piotr", LastName = "Zieliński", Phone = "600100002", Goal = "Budowa masy mięśniowej", Location = "Niepołomice", TrainerEmail = "trainer@studiocrm.local", Package = "8 treningów 1:1", Billing = "Pending" },
            new { Email = "kasia.wojcik@test.pl", FirstName = "Kasia", LastName = "Wójcik", Phone = "600100003", Goal = "Poprawa kondycji", Location = "Kłaj", TrainerEmail = "karolina.trener@studiocrm.local", Package = "8 treningów 2:1", Billing = "Paid" },
            new { Email = "michal.lis@test.pl", FirstName = "Michał", LastName = "Lis", Phone = "600100004", Goal = "Siła i hipertrofia", Location = "Kłaj", TrainerEmail = "bartek.trener@studiocrm.local", Package = "12 treningów 2:1", Billing = "Paid" },
            new { Email = "agnieszka.kaczmarek@test.pl", FirstName = "Agnieszka", LastName = "Kaczmarek", Phone = "600100005", Goal = "Powrót po kontuzji kolana", Location = "Niepołomice", TrainerEmail = "adam.trener@studiocrm.local", Package = "8 treningów 1:1", Billing = "Overdue" },
            new { Email = "pawel.mazur@test.pl", FirstName = "Paweł", LastName = "Mazur", Phone = "600100006", Goal = "Redukcja i zdrowe plecy", Location = "Niepołomice", TrainerEmail = "adam.trener@studiocrm.local", Package = "12 treningów 3:1", Billing = "Paid" },
            new { Email = "ewa.krol@test.pl", FirstName = "Ewa", LastName = "Król", Phone = "600100007", Goal = "Mobilność i sylwetka", Location = "Kłaj", TrainerEmail = "karolina.trener@studiocrm.local", Package = "8 treningów 3:1", Billing = "Pending" },
            new { Email = "tomasz.wrona@test.pl", FirstName = "Tomasz", LastName = "Wrona", Phone = "600100008", Goal = "Przygotowanie do biegu", Location = "Kłaj", TrainerEmail = "bartek.trener@studiocrm.local", Package = "8 treningów 2:1", Billing = "Paid" },
            new { Email = "magda.lewandowska@test.pl", FirstName = "Magda", LastName = "Lewandowska", Phone = "600100009", Goal = "Poprawa siły", Location = "Niepołomice", TrainerEmail = "trainer@studiocrm.local", Package = "4 treningi 1:1", Billing = "Paid" },
            new { Email = "rafal.sikora@test.pl", FirstName = "Rafał", LastName = "Sikora", Phone = "600100010", Goal = "Redukcja", Location = "Kłaj", TrainerEmail = "bartek.trener@studiocrm.local", Package = "12 treningów 1:1", Billing = "Pending" },
            new { Email = "ola.michalska@test.pl", FirstName = "Ola", LastName = "Michalska", Phone = "600100011", Goal = "Trening po ciąży", Location = "Niepołomice", TrainerEmail = "karolina.trener@studiocrm.local", Package = "8 treningów 1:1", Billing = "Paid" },
            new { Email = "dominik.sobczak@test.pl", FirstName = "Dominik", LastName = "Sobczak", Phone = "600100012", Goal = "Masa mięśniowa", Location = "Kłaj", TrainerEmail = "trainer@studiocrm.local", Package = "12 treningów 2:1", Billing = "Paid" }
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
                    TrainingStartDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 200)),
                    Notes = "Klient testowy powiązany z kontem, trenerem, lokalizacją i pakietem.",
                    Status = "Active",
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

    private static async Task SeedBillingTestScenariosAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher)
    {
        var scenarioEmails = new[]
        {
            "billing.paid@studiocrm.local",
            "billing.pending@studiocrm.local",
            "billing.overpaid@studiocrm.local",
            "billing.renewal@studiocrm.local",
            "billing.new@studiocrm.local"
        };

        await ResetBillingScenarioDataAsync(context, scenarioEmails);

        var owner = await context.Users.FirstAsync(u => u.Email == "owner@studiocrm.local");
        var trainer = await context.Trainers
            .Include(t => t.User)
            .FirstAsync(t => t.User.Email == "trainer@studiocrm.local");
        var location = await context.Locations.FirstAsync(l => l.Name == "Niepołomice");

        var paidClient = await EnsureBillingScenarioClientAsync(
            context,
            passwordHasher,
            "billing.paid@studiocrm.local",
            "Paid",
            "Active",
            "Test: opłacony cykl 8 treningów 2:1.",
            trainer.Id,
            location.Id,
            owner.Id);

        var paidCycle = await CreateBillingScenarioCycleAsync(
            context,
            paidClient,
            "8 treningów 2:1",
            PaymentStatus.Paid,
            amountPaid: 760m,
            owner.Id);

        await CreateBillingScenarioPaymentAsync(context, paidClient, paidCycle, 760m, ClientPaymentStatus.Confirmed, ClientPaymentSource.StaffEntry, owner.Id, "Pełna płatność testowa.");
        await CreateCountedBillingSessionsAsync(context, paidClient, trainer.Id, location.Id, paidCycle, 3, SessionBillingType.TwoToOne);

        var pendingClient = await EnsureBillingScenarioClientAsync(
            context,
            passwordHasher,
            "billing.pending@studiocrm.local",
            "Pending",
            "Payment",
            "Test: klient zgłosił płatność, staff musi potwierdzić.",
            trainer.Id,
            location.Id,
            owner.Id);

        var pendingCycle = await CreateBillingScenarioCycleAsync(
            context,
            pendingClient,
            "12 treningów 2:1",
            PaymentStatus.PendingConfirmation,
            amountPaid: 0m,
            owner.Id);

        await CreateBillingScenarioPaymentAsync(context, pendingClient, pendingCycle, 1080m, ClientPaymentStatus.PendingConfirmation, ClientPaymentSource.ClientRequest, pendingClient.UserId, "Zgłoszenie płatności przez klienta.");

        var overpaidClient = await EnsureBillingScenarioClientAsync(
            context,
            passwordHasher,
            "billing.overpaid@studiocrm.local",
            "Overpaid",
            "Carryover",
            "Test: nadpłata przenoszona na kolejny cykl.",
            trainer.Id,
            location.Id,
            owner.Id);

        var overpaidCycle = await CreateBillingScenarioCycleAsync(
            context,
            overpaidClient,
            "8 treningów 1:1",
            PaymentStatus.Paid,
            amountPaid: 1120m,
            owner.Id);

        await CreateBillingScenarioPaymentAsync(context, overpaidClient, overpaidCycle, 1200m, ClientPaymentStatus.Confirmed, ClientPaymentSource.StaffEntry, owner.Id, "Płatność z nadpłatą 80 PLN.");
        await context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
        {
            ClientId = overpaidClient.Id,
            ClientPackageId = overpaidCycle.Id,
            Amount = 80m,
            Type = BalanceTransactionType.PaymentOverpayment,
            Description = "SEED: Nadpłata 80 PLN do wykorzystania w następnym cyklu.",
            CreatedAt = DateTime.UtcNow
        });

        var renewalClient = await EnsureBillingScenarioClientAsync(
            context,
            passwordHasher,
            "billing.renewal@studiocrm.local",
            "Renewal",
            "AlmostDone",
            "Test: 7/8 wykorzystanych sesji, ustawiony kolejny pakiet 12 treningów 2:1.",
            trainer.Id,
            location.Id,
            owner.Id);

        var renewalCycle = await CreateBillingScenarioCycleAsync(
            context,
            renewalClient,
            "8 treningów 2:1",
            PaymentStatus.Paid,
            amountPaid: 760m,
            owner.Id);

        var nextPackage = await context.Packages.FirstAsync(p => p.Name == "12 treningów 2:1");
        renewalClient.NextPackageId = nextPackage.Id;
        await CreateBillingScenarioPaymentAsync(context, renewalClient, renewalCycle, 760m, ClientPaymentStatus.Confirmed, ClientPaymentSource.StaffEntry, owner.Id, "Cykl prawie zakończony.");
        await CreateCountedBillingSessionsAsync(context, renewalClient, trainer.Id, location.Id, renewalCycle, 7, SessionBillingType.TwoToOne);
        await CreatePlannedBillingSessionAsync(context, renewalClient, trainer.Id, location.Id, renewalCycle, "Ostatnia sesja do testu auto-renew");

        await EnsureBillingScenarioClientAsync(
            context,
            passwordHasher,
            "billing.new@studiocrm.local",
            "New",
            "NoPackage",
            "Test: klient bez aktywnego pakietu, do przypisania przez ownera/trenera.",
            trainer.Id,
            location.Id,
            owner.Id,
            status: "New",
            billingStatus: "Pending");

        await context.SaveChangesAsync();
    }

    private static async Task SeedTrainerSettlementTestDataAsync(StudioCRMDbContext context)
    {
        var trainer = await context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.User.Email == "trainer@studiocrm.local");

        if (trainer is null)
            return;

        var owner = await context.Users.FirstAsync(u => u.Email == "owner@studiocrm.local");
        var location = await context.Locations.FirstAsync(l => l.Name == "Niepołomice");
        var hourlyRate = await context.TrainerRates
            .Where(r => r.TrainerId == trainer.Id && r.SessionType == "Hourly" && r.IsActive)
            .OrderByDescending(r => r.ValidFrom)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync();

        if (hourlyRate <= 0)
            hourlyRate = 70m;

        var clients = await context.Clients
            .Where(c =>
                c.TrainerId == trainer.Id &&
                c.LocationId == location.Id &&
                c.Status == "Active")
            .OrderBy(c => c.Id)
            .Take(4)
            .ToListAsync();

        if (!clients.Any())
            return;

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        await RemoveSeedTrainerSettlementDataAsync(
            context,
            trainer.Id,
            currentMonthStart,
            previousMonthStart);

        await CreateSettlementSessionAsync(
            context,
            trainer.Id,
            location.Id,
            clients.Take(1).ToList(),
            currentMonthStart.AddDays(1).AddHours(15),
            "Rozliczenie testowe - trening 1:1",
            "OneToOne");

        await CreateSettlementSessionAsync(
            context,
            trainer.Id,
            location.Id,
            clients.Take(2).ToList(),
            currentMonthStart.AddDays(3).AddHours(17),
            "Rozliczenie testowe - trening 2:1",
            "TwoToOne");

        await CreateSettlementSessionAsync(
            context,
            trainer.Id,
            location.Id,
            clients.Take(Math.Min(3, clients.Count)).ToList(),
            currentMonthStart.AddDays(6).AddHours(18),
            "Rozliczenie testowe - trening 3:1",
            clients.Count >= 3 ? "ThreeToOne" : ResolveSessionType(clients.Count));

        await CreateSettlementSessionAsync(
            context,
            trainer.Id,
            location.Id,
            clients.Take(Math.Min(4, clients.Count)).ToList(),
            currentMonthStart.AddDays(9).AddHours(19),
            "Rozliczenie testowe - trening 4:1",
            clients.Count >= 4 ? "FourToOne" : ResolveSessionType(clients.Count));

        var previousSessions = new[]
        {
            await CreateSettlementSessionAsync(
                context,
                trainer.Id,
                location.Id,
                clients.Take(1).ToList(),
                previousMonthStart.AddDays(5).AddHours(16),
                "Poprzedni miesiąc - trening 1:1",
                "OneToOne"),
            await CreateSettlementSessionAsync(
                context,
                trainer.Id,
                location.Id,
                clients.Take(2).ToList(),
                previousMonthStart.AddDays(12).AddHours(17),
                "Poprzedni miesiąc - trening 2:1",
                "TwoToOne")
        };

        var previousHours = previousSessions.Sum(s => ResolveSeedSettlementHours(s.ActualSessionType!, s.StartAt, s.EndAt));

        await context.TrainerMonthlySettlements.AddAsync(new TrainerMonthlySettlement
        {
            TrainerId = trainer.Id,
            Year = previousMonthStart.Year,
            Month = previousMonthStart.Month,
            TotalSessions = previousSessions.Length,
            TotalHours = previousHours,
            TotalAmount = previousHours * hourlyRate,
            IsPaid = true,
            PaidAt = previousMonthStart.AddMonths(1).AddDays(1).AddHours(10),
            PaidByUserId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task RemoveSeedTrainerSettlementDataAsync(
        StudioCRMDbContext context,
        int trainerId,
        DateTime currentMonthStart,
        DateTime previousMonthStart)
    {
        var currentYear = currentMonthStart.Year;
        var currentMonth = currentMonthStart.Month;
        var previousYear = previousMonthStart.Year;
        var previousMonth = previousMonthStart.Month;

        var settlements = await context.TrainerMonthlySettlements
            .Where(s =>
                s.TrainerId == trainerId &&
                ((s.Year == currentYear && s.Month == currentMonth) ||
                 (s.Year == previousYear && s.Month == previousMonth)))
            .ToListAsync();

        context.TrainerMonthlySettlements.RemoveRange(settlements);

        var sessionIds = await context.Sessions
            .Where(s => s.Note != null && s.Note.StartsWith(SettlementSeedNotePrefix))
            .Select(s => s.Id)
            .ToListAsync();

        if (sessionIds.Any())
        {
            var participants = await context.SessionParticipants
                .Where(p => sessionIds.Contains(p.SessionId))
                .ToListAsync();

            context.SessionParticipants.RemoveRange(participants);

            var sessions = await context.Sessions
                .Where(s => sessionIds.Contains(s.Id))
                .ToListAsync();

            context.Sessions.RemoveRange(sessions);
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Session> CreateSettlementSessionAsync(
        StudioCRMDbContext context,
        int trainerId,
        int locationId,
        List<Client> clients,
        DateTime start,
        string title,
        string sessionType)
    {
        var end = start.AddHours(1);
        var session = new Session
        {
            Title = title,
            Note = $"{SettlementSeedNotePrefix} {title}",
            StartAt = start,
            EndAt = end,
            TrainerId = trainerId,
            LocationId = locationId,
            Status = "Completed",
            PlannedSessionType = sessionType,
            ActualSessionType = sessionType,
            ActualParticipantsCount = clients.Count,
            CompletedAt = end,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = 1,
            IsDeleted = false
        };

        await context.Sessions.AddAsync(session);
        await context.SaveChangesAsync();

        foreach (var client in clients)
        {
            await context.SessionParticipants.AddAsync(new SessionParticipant
            {
                SessionId = session.Id,
                ClientId = client.Id,
                PackageId = client.ActivePackageId,
                AttendanceStatus = "Present",
                CountsAgainstPackage = true,
                SessionsCharged = 1,
                IsCountedFromPackage = true,
                Note = $"{SettlementSeedNotePrefix} Uczestnik sesji do testu rozliczenia trenera.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return session;
    }

    private static decimal ResolveSeedSettlementHours(string sessionType, DateTime startAt, DateTime endAt)
    {
        return sessionType switch
        {
            "TwoToOne" => 1.6m,
            "ThreeToOne" => 2.2m,
            "FourToOne" => 2.66m,
            _ => Math.Round((decimal)(endAt - startAt).TotalHours, 2)
        };
    }

    private static async Task ResetBillingScenarioDataAsync(
        StudioCRMDbContext context,
        IReadOnlyCollection<string> scenarioEmails)
    {
        var scenarioClients = await context.Clients
            .Where(c => scenarioEmails.Contains(c.Email))
            .ToListAsync();

        if (!scenarioClients.Any())
            return;

        var clientIds = scenarioClients.Select(c => c.Id).ToList();
        var clientPackageIds = await context.ClientPackages
            .Where(cp => clientIds.Contains(cp.ClientId))
            .Select(cp => cp.Id)
            .ToListAsync();
        var sessionIds = await context.Sessions
            .Where(s => s.Note != null && s.Note.StartsWith(BillingSeedNotePrefix))
            .Select(s => s.Id)
            .ToListAsync();

        var participants = await context.SessionParticipants
            .Where(sp =>
                clientIds.Contains(sp.ClientId) ||
                sessionIds.Contains(sp.SessionId) ||
                (sp.ClientPackageId.HasValue && clientPackageIds.Contains(sp.ClientPackageId.Value)))
            .ToListAsync();
        context.SessionParticipants.RemoveRange(participants);

        var balances = await context.ClientBalanceTransactions
            .Where(t =>
                clientIds.Contains(t.ClientId) ||
                (t.ClientPackageId.HasValue && clientPackageIds.Contains(t.ClientPackageId.Value)) ||
                (t.SessionId.HasValue && sessionIds.Contains(t.SessionId.Value)))
            .ToListAsync();
        context.ClientBalanceTransactions.RemoveRange(balances);

        var payments = await context.ClientPayments
            .Where(p =>
                clientIds.Contains(p.ClientId) ||
                (p.ClientPackageId.HasValue && clientPackageIds.Contains(p.ClientPackageId.Value)))
            .ToListAsync();
        context.ClientPayments.RemoveRange(payments);

        var sessions = await context.Sessions
            .Where(s => sessionIds.Contains(s.Id))
            .ToListAsync();
        context.Sessions.RemoveRange(sessions);

        var cycles = await context.ClientPackages
            .Where(cp => clientIds.Contains(cp.ClientId))
            .ToListAsync();
        context.ClientPackages.RemoveRange(cycles);

        foreach (var client in scenarioClients)
        {
            client.ActivePackageId = null;
            client.NextPackageId = null;
            client.SubscriptionAutoRenewEnabled = true;
            client.RenewalCancellationRequestedAt = null;
            client.RenewalCancellationRequestedByUserId = null;
            client.RenewalCancelledAt = null;
            client.RenewalCancelledByUserId = null;
            client.Status = "New";
            client.BillingStatus = "Pending";
            client.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Client> EnsureBillingScenarioClientAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher,
        string email,
        string firstName,
        string lastName,
        string notes,
        int trainerId,
        int locationId,
        int ownerUserId,
        string status = "Active",
        string billingStatus = "Paid")
    {
        var user = await EnsureUserAsync(
            context,
            passwordHasher,
            email,
            firstName,
            lastName,
            ClientPassword);

        await EnsureUserRoleAsync(context, user.Id, "Client");

        var client = await context.Clients.FirstOrDefaultAsync(c => c.Email == email);

        if (client is null)
        {
            client = new Client
            {
                UserId = user.Id,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = ownerUserId
            };

            await context.Clients.AddAsync(client);
        }

        client.UserId = user.Id;
        client.FirstName = firstName;
        client.LastName = lastName;
        client.PhoneNumber = "600200" + client.Id.ToString("000");
        client.TrainerId = trainerId;
        client.LocationId = locationId;
        client.Goal = "Test billing/subskrypcji";
        client.Notes = notes;
        client.Status = status;
        client.BillingStatus = billingStatus;
        client.TrainingStartDate = DateTime.UtcNow.Date.AddDays(-20);
        client.GoogleDriveFolderId = $"seed-folder-{email}";
        client.TrainingPlanFileId = $"seed-plan-{email}";
        client.TrainingPlanFileName = "Plan treningowy - test billing";
        client.TrainingPlanUrl = "https://drive.google.com/";
        client.SubscriptionAutoRenewEnabled = true;
        client.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return client;
    }

    private static async Task<ClientPackage> CreateBillingScenarioCycleAsync(
        StudioCRMDbContext context,
        Client client,
        string packageName,
        PaymentStatus paymentStatus,
        decimal amountPaid,
        int ownerUserId)
    {
        var package = await context.Packages.FirstAsync(p => p.Name == packageName);
        var now = DateTime.UtcNow;
        var expectedUnitPrice = package.SessionsLimit > 0
            ? decimal.Round(package.Price / package.SessionsLimit, 2)
            : package.Price;

        var cycle = new ClientPackage
        {
            ClientId = client.Id,
            PackageId = package.Id,
            Name = package.Name,
            TotalSessions = package.SessionsLimit,
            SessionsPerWeek = package.SessionsPerWeek,
            UsedSessions = 0,
            TotalPrice = package.Price,
            OriginalPrice = package.Price,
            BalanceApplied = 0,
            AmountPaid = amountPaid,
            ExpectedUnitPrice = expectedUnitPrice,
            Currency = package.Currency,
            LocationId = package.LocationId ?? client.LocationId,
            ExpectedBillingType = package.BillingType,
            PaymentStatus = paymentStatus,
            PurchaseDate = now.Date.AddDays(-14),
            ValidUntil = now.Date.AddDays(31),
            PaidAt = paymentStatus == PaymentStatus.Paid ? now.Date.AddDays(-13) : null,
            PaymentDueDate = paymentStatus == PaymentStatus.Paid ? null : now.Date.AddDays(7),
            ActivatedAt = now.Date.AddDays(-14),
            ActivatedByUserId = ownerUserId,
            ActivationMode = ClientPackageActivationMode.Immediately,
            RenewalSource = "SeedScenario",
            IsActive = true
        };

        await context.ClientPackages.AddAsync(cycle);
        await context.SaveChangesAsync();

        client.ActivePackageId = package.Id;
        client.Status = "Active";
        client.BillingStatus = paymentStatus.ToString();
        client.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return cycle;
    }

    private static async Task CreateBillingScenarioPaymentAsync(
        StudioCRMDbContext context,
        Client client,
        ClientPackage cycle,
        decimal amount,
        ClientPaymentStatus status,
        ClientPaymentSource source,
        int? userId,
        string note)
    {
        var appliedToPackageAmount = status == ClientPaymentStatus.Confirmed
            ? Math.Min(amount, cycle.TotalPrice)
            : 0;

        await context.ClientPayments.AddAsync(new ClientPayment
        {
            ClientId = client.Id,
            ClientPackageId = cycle.Id,
            Amount = amount,
            AppliedToPackageAmount = appliedToPackageAmount,
            BalanceCreditAmount = status == ClientPaymentStatus.Confirmed
                ? amount - appliedToPackageAmount
                : 0,
            Currency = cycle.Currency,
            Method = PaymentMethod.BankTransfer,
            Status = status,
            Source = source,
            PaymentDate = DateTime.UtcNow.Date.AddDays(-3),
            CreatedAt = DateTime.UtcNow,
            ConfirmedAt = status == ClientPaymentStatus.Confirmed ? DateTime.UtcNow.Date.AddDays(-2) : null,
            CreatedByUserId = userId,
            ConfirmedByUserId = status == ClientPaymentStatus.Confirmed ? userId : null,
            Note = note
        });
    }

    private static async Task CreateCountedBillingSessionsAsync(
        StudioCRMDbContext context,
        Client client,
        int trainerId,
        int locationId,
        ClientPackage cycle,
        int count,
        SessionBillingType actualBillingType)
    {
        for (var i = 0; i < count; i++)
        {
            var start = DateTime.UtcNow.Date.AddDays(-count + i).AddHours(17);
            var session = await CreateBillingSessionAsync(
                context,
                client,
                trainerId,
                locationId,
                $"{BillingSeedNotePrefix} Policzona sesja {i + 1}/{count}",
                start,
                "Completed");

            var actualUnitPrice = ResolveSeedActualUnitPrice(cycle, actualBillingType);
            var balanceDifference = cycle.ExpectedUnitPrice - actualUnitPrice;

            await context.SessionParticipants.AddAsync(new SessionParticipant
            {
                SessionId = session.Id,
                ClientId = client.Id,
                PackageId = cycle.PackageId,
                ClientPackageId = cycle.Id,
                AttendanceStatus = "Present",
                CountsAgainstPackage = true,
                SessionsCharged = 1,
                PlannedBillingType = cycle.ExpectedBillingType,
                ActualBillingType = actualBillingType,
                ExpectedUnitPrice = cycle.ExpectedUnitPrice,
                ActualUnitPrice = actualUnitPrice,
                BalanceDifference = balanceDifference,
                IsCountedFromPackage = true,
                Note = $"{BillingSeedNotePrefix} Użycie cyklu subskrypcji.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            if (balanceDifference != 0)
            {
                await context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
                {
                    ClientId = client.Id,
                    ClientPackageId = cycle.Id,
                    SessionId = session.Id,
                    Amount = balanceDifference,
                    Type = BalanceTransactionType.PackageAdjustment,
                    Description = $"{BillingSeedNotePrefix} Korekta za sesję {actualBillingType} zamiast {cycle.ExpectedBillingType}.",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        cycle.UsedSessions = count;
        await context.SaveChangesAsync();
    }

    private static async Task CreatePlannedBillingSessionAsync(
        StudioCRMDbContext context,
        Client client,
        int trainerId,
        int locationId,
        ClientPackage cycle,
        string title)
    {
        var session = await CreateBillingSessionAsync(
            context,
            client,
            trainerId,
            locationId,
            $"{BillingSeedNotePrefix} {title}",
            DateTime.UtcNow.Date.AddDays(1).AddHours(17),
            "Planned");

        await context.SessionParticipants.AddAsync(new SessionParticipant
        {
            SessionId = session.Id,
            ClientId = client.Id,
            PackageId = cycle.PackageId,
            ClientPackageId = null,
            AttendanceStatus = "Planned",
            CountsAgainstPackage = false,
            SessionsCharged = 0,
            PlannedBillingType = cycle.ExpectedBillingType,
            Note = $"{BillingSeedNotePrefix} Użyj tego participantId do testu auto-renew.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task<Session> CreateBillingSessionAsync(
        StudioCRMDbContext context,
        Client client,
        int trainerId,
        int locationId,
        string note,
        DateTime start,
        string status)
    {
        var session = new Session
        {
            Title = $"{client.FirstName} {client.LastName}",
            Note = note,
            StartAt = start,
            EndAt = start.AddHours(1),
            TrainerId = trainerId,
            LocationId = locationId,
            Status = status,
            PlannedSessionType = "BillingScenario",
            ActualSessionType = status == "Completed" ? "BillingScenario" : null,
            ActualParticipantsCount = status == "Completed" ? 1 : null,
            CompletedAt = status == "Completed" ? start.AddHours(1) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = 1
        };

        await context.Sessions.AddAsync(session);
        await context.SaveChangesAsync();

        return session;
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

    private static async Task SeedOutlookMappingTestDataAsync(StudioCRMDbContext context)
    {
        var trainerUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "trainer@studiocrm.local");

        if (trainerUser is null)
            return;

        var integration = await context.CalendarIntegrations
            .FirstOrDefaultAsync(x =>
                x.UserId == trainerUser.Id &&
                x.Provider == "Outlook");

        if (integration is null)
        {
            integration = new CalendarIntegration
            {
                UserId = trainerUser.Id,
                Provider = "Outlook",
                ExternalUserId = "seed-outlook-user-trainer",
                Email = "trainer@studiocrm.local",
                AccessToken = "seed-access-token",
                RefreshToken = "seed-refresh-token",
                AccessTokenExpiresAt = DateTime.UtcNow.AddDays(7),
                IsActive = true,
                ConnectedAt = DateTime.UtcNow,
                DisconnectedAt = null
            };

            await context.CalendarIntegrations.AddAsync(integration);
            await context.SaveChangesAsync();
        }
        else
        {
            integration.IsActive = true;
            integration.DisconnectedAt = null;
            integration.AccessTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            await context.SaveChangesAsync();
        }

        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        var limitTestDay = DateTime.UtcNow.Date.AddDays(60);

        await EnsureExternalCalendarEventAsync(
            context,
            integration.Id,
            "seed-outlook-klaj-basic",
            "Test Outlook Kłaj - poprawne mapowanie",
            tomorrow.AddHours(17),
            tomorrow.AddHours(18),
            "Kłaj_Studio",
            "klaj237_studio@bsworkout.pl",
            "trainer@studiocrm.local",
            new[]
            {
                "kasia.wojcik@test.pl",
                "michal.lis@test.pl"
            });

        await EnsureExternalCalendarEventAsync(
            context,
            integration.Id,
            "seed-outlook-niepolomice-basic",
            "Test Outlook Niepołomice - poprawne mapowanie",
            tomorrow.AddHours(18),
            tomorrow.AddHours(19),
            "Niepołomice_Studio",
            "niepolomice8_studio@bsworkout.pl",
            "trainer@studiocrm.local",
            new[]
            {
                "anna.nowak@test.pl",
                "piotr.zielinski@test.pl"
            });

        await EnsureExternalCalendarEventAsync(
            context,
            integration.Id,
            "seed-outlook-unknown-client",
            "Test Outlook - nierozpoznany klient",
            tomorrow.AddDays(1).AddHours(17),
            tomorrow.AddDays(1).AddHours(18),
            "Kłaj_Studio",
            "klaj237_studio@bsworkout.pl",
            "trainer@studiocrm.local",
            new[]
            {
                "kasia.wojcik@test.pl",
                "nieznany.klient@test.pl"
            });

        await EnsureExternalCalendarEventAsync(
            context,
            integration.Id,
            "seed-outlook-unknown-location",
            "Test Outlook - nierozpoznana lokalizacja",
            tomorrow.AddDays(1).AddHours(19),
            tomorrow.AddDays(1).AddHours(20),
            "Nieznana sala",
            "unknown_room@bsworkout.pl",
            "trainer@studiocrm.local",
            new[]
            {
                "anna.nowak@test.pl"
            });

        await EnsureExternalCalendarEventAsync(
            context,
            integration.Id,
            "seed-outlook-recurring-instance",
            "Test Outlook - wystąpienie cykliczne",
            tomorrow.AddDays(2).AddHours(16),
            tomorrow.AddDays(2).AddHours(17),
            "Kłaj_Studio",
            "klaj237_studio@bsworkout.pl",
            "trainer@studiocrm.local",
            new[]
            {
                "ewa.krol@test.pl",
                "tomasz.wrona@test.pl"
            },
            seriesMasterId: "seed-series-master-klaj-001",
            isRecurring: true);

        await EnsureExternalCalendarEventAsync(
            context,
            integration.Id,
            "seed-outlook-limit-1",
            "Test limitu Kłaj 1",
            limitTestDay.AddHours(17),
            limitTestDay.AddHours(18),
            "Kłaj_Studio",
            "klaj237_studio@bsworkout.pl",
            "trainer@studiocrm.local",
            new[]
            {
                "kasia.wojcik@test.pl",
                "michal.lis@test.pl",
                "ewa.krol@test.pl",
                "tomasz.wrona@test.pl"
            });

        await EnsureExternalCalendarEventAsync(
            context,
            integration.Id,
            "seed-outlook-limit-2",
            "Test limitu Kłaj 2",
            limitTestDay.AddHours(17).AddMinutes(30),
            limitTestDay.AddHours(18).AddMinutes(30),
            "Kłaj_Studio",
            "klaj237_studio@bsworkout.pl",
            "trainer@studiocrm.local",
            new[]
            {
                "rafal.sikora@test.pl",
                "dominik.sobczak@test.pl",
                "kasia.wojcik@test.pl",
                "michal.lis@test.pl"
            });
    }

    private static async Task EnsureExternalCalendarEventAsync(
        StudioCRMDbContext context,
        int calendarIntegrationId,
        string externalEventId,
        string subject,
        DateTime startAt,
        DateTime endAt,
        string locationName,
        string locationEmail,
        string organizerEmail,
        string[] attendees,
        string? seriesMasterId = null,
        bool isRecurring = false)
    {
        var existing = await context.ExternalCalendarEvents
            .FirstOrDefaultAsync(x =>
                x.Provider == "Outlook" &&
                x.ExternalEventId == externalEventId);

        if (existing is not null)
        {
            if (!existing.IsConvertedToSession)
            {
                existing.CalendarIntegrationId = calendarIntegrationId;
                existing.Subject = subject;
                existing.BodyPreview = "SEED: Testowy event Outlook do mapowania CRM.";
                existing.StartAt = startAt;
                existing.EndAt = endAt;
                existing.LocationName = locationName;
                existing.LocationEmail = locationEmail;
                existing.OrganizerEmail = organizerEmail;
                existing.AttendeesJson = JsonSerializer.Serialize(attendees);
                existing.MappingWarningsJson = null;
                existing.SeriesMasterId = seriesMasterId;
                existing.IsRecurring = isRecurring;
                existing.ImportedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
            return;
        }

        await context.ExternalCalendarEvents.AddAsync(new ExternalCalendarEvent
        {
            CalendarIntegrationId = calendarIntegrationId,
            Provider = "Outlook",
            ExternalEventId = externalEventId,
            Subject = subject,
            BodyPreview = "SEED: Testowy event Outlook do mapowania CRM.",
            StartAt = startAt,
            EndAt = endAt,
            LocationName = locationName,
            LocationEmail = locationEmail,
            OrganizerEmail = organizerEmail,
            AttendeesJson = JsonSerializer.Serialize(attendees),
            MappingWarningsJson = null,
            SeriesMasterId = seriesMasterId,
            IsRecurring = isRecurring,
            IsConvertedToSession = false,
            SessionId = null,
            ImportedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task<User> EnsureUserAsync(
        StudioCRMDbContext context,
        PasswordHasher<User> passwordHasher,
        string email,
        string firstName,
        string lastName,
        string password,
        bool updateExisting = true)
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
        else if (updateExisting)
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
            await context.UserRoles.AddAsync(new UserRoleEntity
            {
                UserId = userId,
                RoleId = role.Id
            });

            await context.SaveChangesAsync();
        }
    }

    private static SessionBillingType InferBillingType(string packageName)
    {
        if (packageName.Contains("2:1", StringComparison.OrdinalIgnoreCase))
            return SessionBillingType.TwoToOne;

        if (packageName.Contains("3:1", StringComparison.OrdinalIgnoreCase))
            return SessionBillingType.ThreeToOne;

        if (packageName.Contains("4:1", StringComparison.OrdinalIgnoreCase))
            return SessionBillingType.FourToOne;

        return SessionBillingType.OneToOne;
    }

    private static int InferSessionsPerWeek(int sessionsLimit)
    {
        return sessionsLimit switch
        {
            >= 12 => 3,
            >= 8 => 2,
            _ => 1
        };
    }

    private static decimal ResolveSeedActualUnitPrice(
        ClientPackage cycle,
        SessionBillingType actualBillingType)
    {
        if (actualBillingType == cycle.ExpectedBillingType)
            return cycle.ExpectedUnitPrice;

        var totalSessions = cycle.TotalSessions;
        var totalPrice = (totalSessions, actualBillingType) switch
        {
            (8, SessionBillingType.OneToOne) => 1120m,
            (8, SessionBillingType.TwoToOne) => 760m,
            (8, SessionBillingType.ThreeToOne) => 640m,
            (8, SessionBillingType.FourToOne) => 560m,
            (12, SessionBillingType.OneToOne) => 1560m,
            (12, SessionBillingType.TwoToOne) => 1080m,
            (12, SessionBillingType.ThreeToOne) => 900m,
            (12, SessionBillingType.FourToOne) => 780m,
            _ => cycle.OriginalPrice
        };

        return decimal.Round(totalPrice / Math.Max(1, totalSessions), 2);
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
