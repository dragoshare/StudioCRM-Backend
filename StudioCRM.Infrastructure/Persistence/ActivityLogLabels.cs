using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Persistence;

// Resolve at write time, before deletes are executed. Never used by the history reader.
internal static class ActivityLogLabels
{
    internal static readonly IReadOnlyDictionary<string, string> TypeLabels = new Dictionary<string, string>
    {
        [nameof(Session)] = "Trening",
        [nameof(SessionParticipant)] = "Uczestnictwo w treningu",
        [nameof(Client)] = "Klient",
        [nameof(Trainer)] = "Trener",
        [nameof(User)] = "Użytkownik",
        [nameof(TrainerLocation)] = "Lokalizacja trenera",
        [nameof(ClientLocationMembership)] = "Dostęp klienta do lokalizacji",
        [nameof(Package)] = "Pakiet",
        [nameof(ClientPackage)] = "Pakiet klienta",
        [nameof(ClientPayment)] = "Płatność",
        [nameof(ClientBalanceTransaction)] = "Zmiana salda",
        [nameof(TrainerRate)] = "Stawka trenera",
        [nameof(TrainerContract)] = "Umowa trenera",
        [nameof(TrainerContractLocation)] = "Lokalizacja umowy trenera",
        [nameof(TrainerMonthlySettlement)] = "Rozliczenie trenera"
    };

    internal static void Resolve(StudioCRMDbContext db, IReadOnlyList<ActivityLogCapture> changes) =>
        ResolveCore(db, changes, asynchronous: false, CancellationToken.None).GetAwaiter().GetResult();

    internal static Task ResolveAsync(StudioCRMDbContext db, IReadOnlyList<ActivityLogCapture> changes, CancellationToken ct) =>
        ResolveCore(db, changes, asynchronous: true, ct);

    private static async Task ResolveCore(StudioCRMDbContext db, IReadOnlyList<ActivityLogCapture> changes, bool asynchronous, CancellationToken ct)
    {
        // EF temporary keys are read from entries, not CLR Id properties (new graphs have Id=0).
        var tracked = db.ChangeTracker.Entries().ToList();
        var clients = Tracked<Client, string>(tracked, e => FullName(Read(e, "FirstName"), Read(e, "LastName")));
        var locations = Tracked<Location, string>(tracked, e => Read(e, "Name"));
        var trainers = Tracked<Trainer, int>(tracked, e => ReadInt(e, "UserId"));
        var users = Tracked<User, string>(tracked, e => UserName(Read(e, "FirstName"), Read(e, "LastName"), Read(e, "Email")));
        var contracts = Tracked<TrainerContract, ContractInfo>(tracked, e => new(ReadInt(e, "TrainerId"), Read(e, "ContractNumber")));

        var missing = Missing(changes.Select(c => c.ReferenceId("ClientId")), clients);
        if (missing.Length > 0)
            foreach (var row in await ReadRows(db.Clients.IgnoreQueryFilters().AsNoTracking()
                .Where(c => missing.Contains(c.Id)).Select(c => new NameRow(c.Id, c.FirstName + " " + c.LastName)), asynchronous, ct))
                clients[row.Id] = row.Name.Trim();

        missing = Missing(changes.Select(c => c.ReferenceId("LocationId")), locations);
        if (missing.Length > 0)
            foreach (var row in await ReadRows(db.Locations.AsNoTracking()
                .Where(l => missing.Contains(l.Id)).Select(l => new NameRow(l.Id, l.Name)), asynchronous, ct))
                locations[row.Id] = row.Name.Trim();

        missing = Missing(changes.Select(c => c.ReferenceId("TrainerContractId")), contracts);
        if (missing.Length > 0)
            foreach (var row in await ReadRows(db.TrainerContracts.IgnoreQueryFilters().AsNoTracking()
                .Where(c => missing.Contains(c.Id)).Select(c => new ContractRow(c.Id, c.TrainerId, c.ContractNumber)), asynchronous, ct))
                contracts[row.Id] = new(row.TrainerId, row.Number);

        var relevantContractIds = changes.Select(c => c.ReferenceId("TrainerContractId")).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        missing = Missing(changes.Select(c => c.ReferenceId("TrainerId"))
            .Concat(contracts.Where(c => relevantContractIds.Contains(c.Key)).Select(c => (int?)c.Value.TrainerId)), trainers);
        if (missing.Length > 0)
            foreach (var row in await ReadRows(db.Trainers.IgnoreQueryFilters().AsNoTracking()
                .Where(t => missing.Contains(t.Id)).Select(t => new TrainerRow(t.Id, t.UserId)), asynchronous, ct))
                trainers[row.Id] = row.UserId;

        var relevantTrainerIds = changes.Select(c => c.ReferenceId("TrainerId"))
            .Concat(contracts.Where(c => relevantContractIds.Contains(c.Key)).Select(c => (int?)c.Value.TrainerId))
            .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        missing = Missing(changes.Where(c => c.EntityType == nameof(Trainer)).Select(c => c.ReferenceId("UserId"))
            .Concat(trainers.Where(t => relevantTrainerIds.Contains(t.Key)).Select(t => (int?)t.Value)), users);
        if (missing.Length > 0)
            foreach (var row in await ReadRows(db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => missing.Contains(u.Id)).Select(u => new UserRow(u.Id, u.FirstName, u.LastName, u.Email)), asynchronous, ct))
                users[row.Id] = UserName(row.FirstName, row.LastName, row.Email);

        string ClientName(ActivityLogCapture c) => Name(clients, c.ReferenceId("ClientId"), "klient");
        string LocationName(ActivityLogCapture c) => Name(locations, c.ReferenceId("LocationId"), "lokalizacja");
        string TrainerName(int? id) => id.HasValue && trainers.TryGetValue(id.Value, out var userId)
            ? Name(users, userId, "trener") : Fallback("trener", id);
        string ContractName(ActivityLogCapture c) => c.ReferenceId("TrainerContractId") is int id && contracts.TryGetValue(id, out var contract)
            ? Join(TrainerName(contract.TrainerId), contract.Number) : Fallback("umowa", c.ReferenceId("TrainerContractId"));

        foreach (var c in changes)
        {
            string Text(string key) => c.Value(key)?.ToString()?.Trim() ?? "";
            var subject = c.EntityType switch
            {
                nameof(Session) => Text("Title"),
                nameof(Client) => FullName(Text("FirstName"), Text("LastName")),
                nameof(User) => UserName(Text("FirstName"), Text("LastName"), Text("Email")),
                nameof(Package) => Text("Name"),
                nameof(Trainer) => Name(users, c.ReferenceId("UserId"), "trener"),
                nameof(SessionParticipant) or nameof(ClientPayment) or nameof(ClientBalanceTransaction) => ClientName(c),
                nameof(ClientPackage) => Join(ClientName(c), Text("Name")),
                nameof(ClientLocationMembership) => Join(ClientName(c), LocationName(c)),
                nameof(TrainerLocation) => Join(TrainerName(c.ReferenceId("TrainerId")), LocationName(c)),
                nameof(TrainerRate) => TrainerName(c.ReferenceId("TrainerId")),
                nameof(TrainerContract) => Join(TrainerName(c.ReferenceId("TrainerId")), Text("ContractNumber")),
                nameof(TrainerContractLocation) => Join(ContractName(c), LocationName(c)),
                nameof(TrainerMonthlySettlement) => Join(TrainerName(c.ReferenceId("TrainerId")),
                    $"{Convert.ToInt32(c.Value("Year"), CultureInfo.InvariantCulture):D4}-{Convert.ToInt32(c.Value("Month"), CultureInfo.InvariantCulture):D2}"),
                _ => throw new InvalidOperationException($"Missing activity label for {c.EntityType}.")
            };
            // Preserve the existing natural names for directly named objects.
            c.EntityLabel = string.IsNullOrWhiteSpace(subject) ? null :
                c.EntityType is nameof(Session) or nameof(Client) or nameof(User) or nameof(Package)
                    ? subject : Join(TypeLabels[c.EntityType], subject);
        }
    }

    private static Task<List<T>> ReadRows<T>(IQueryable<T> query, bool asynchronous, CancellationToken ct) =>
        asynchronous ? query.ToListAsync(ct) : Task.FromResult(query.ToList());

    private static Dictionary<int, TValue> Tracked<TEntity, TValue>(IEnumerable<EntityEntry> entries, Func<EntityEntry, TValue> value)
        => entries.Where(e => e.Entity is TEntity).ToDictionary(e => ReadInt(e, "Id"), value);

    private static object? ReadValue(EntityEntry e, string name) => e.State == EntityState.Deleted
        ? e.Property(name).OriginalValue : e.Property(name).CurrentValue;
    private static string Read(EntityEntry e, string name) => ReadValue(e, name)?.ToString()?.Trim() ?? "";
    private static int ReadInt(EntityEntry e, string name) => (int)ReadValue(e, name)!;
    private static int[] Missing<T>(IEnumerable<int?> ids, Dictionary<int, T> loaded) =>
        ids.Where(id => id > 0 && !loaded.ContainsKey(id.Value)).Select(id => id!.Value).Distinct().ToArray();
    private static string FullName(string first, string last) => $"{first.Trim()} {last.Trim()}".Trim();
    private static string UserName(string first, string last, string email) => FullName(first, last) is { Length: > 0 } name ? name : email.Trim();
    private static string Name(Dictionary<int, string> names, int? id, string fallback) =>
        id.HasValue && names.TryGetValue(id.Value, out var name) && !string.IsNullOrWhiteSpace(name) ? name : Fallback(fallback, id);
    private static string Fallback(string kind, int? id) => id > 0 ? $"{kind} #{id}" : $"{kind} bez nazwy";
    private static string Join(string first, string second) => string.IsNullOrWhiteSpace(second) ? first : $"{first} — {second.Trim()}";

    private sealed record NameRow(int Id, string Name);
    private sealed record UserRow(int Id, string FirstName, string LastName, string Email);
    private sealed record TrainerRow(int Id, int UserId);
    private sealed record ContractRow(int Id, int TrainerId, string Number);
    private sealed record ContractInfo(int TrainerId, string Number);
}
