using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientService
{
    public async Task<ClientPageDto> GetPageAsync(ClientPageFilter filter)
    {
        filter.Validate();
        var query = ApplyClientAccessControl(filter.IsArchived ? ReadableClients() : _context.Clients)
            .AsNoTracking()
            .Where(c => c.IsDeleted == filter.IsArchived);

        if (filter.TrainerId.HasValue)
            query = query.Where(c => c.TrainerId == filter.TrainerId.Value);
        if (filter.LocationId.HasValue)
            query = query.Where(c => c.LocationId == filter.LocationId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(c => c.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = WhereMatchesSearch(query, filter.Search);
        if (filter.PackageStatus is not null)
            query = TrainerPortalService.WhereHasActivePackage(_context, query, filter.PackageStatus == "active");
        if (filter.HasAmountDue == true)
            query = query.Where(c => _context.ClientPackages.Any(p => p.ClientId == c.Id && p.TotalPrice > p.AmountPaid));

        var totalCount = await query.CountAsync();
        var activeCount = await query.CountAsync(c => c.Status == "Active");
        var withPackageCount = await TrainerPortalService.WhereHasActivePackage(_context, query, true).CountAsync();

        var ids = await OrderClientPage(_context, query, filter.ResolveSort())
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(c => c.Id).ToListAsync();
        var rows = await BuildClientQuery(includeArchived: filter.IsArchived).Where(c => ids.Contains(c.Id)).ToListAsync();
        var items = ids.Select(id => rows.Single(c => c.Id == id)).ToList();

        // A page past the end (e.g. after data changed) returns empty items with the current totals.
        return new ClientPageDto
        {
            Items = await EnrichClientListSummariesAsync(items),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (decimal)filter.PageSize),
            Summary = new ClientPageSummaryDto
            {
                ActiveClientsCount = activeCount,
                WithActivePackageCount = withPackageCount
            }
        };
    }

    // Balance and packageUsage reuse the trainer portal definitions; every sort ends with Id for stable paging.
    internal static IOrderedQueryable<Client> OrderClientPage(StudioCRMDbContext db, IQueryable<Client> query, string sort) => sort switch
    {
        "trainer" => query.OrderBy(c => c.TrainerId == null)
            .ThenBy(c => c.Trainer!.User.LastName).ThenBy(c => c.Trainer!.User.FirstName)
            .ThenBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id),
        "trainerDesc" => query.OrderBy(c => c.TrainerId == null)
            .ThenByDescending(c => c.Trainer!.User.LastName).ThenByDescending(c => c.Trainer!.User.FirstName)
            .ThenBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id),
        "archivedAt" => query.OrderBy(c => c.DeletedAt == null).ThenBy(c => c.DeletedAt).ThenBy(c => c.Id),
        "archivedAtDesc" => query.OrderBy(c => c.DeletedAt == null).ThenByDescending(c => c.DeletedAt).ThenByDescending(c => c.Id),
        "name" => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id),
        // Amount due = sum of max(0, TotalPrice - AmountPaid) over all the client's packages.
        "amountDue" => query.OrderBy(c => db.ClientPackages.Where(p => p.ClientId == c.Id && p.TotalPrice > p.AmountPaid)
            .Sum(p => p.TotalPrice - p.AmountPaid)).ThenBy(c => c.Id),
        "amountDueDesc" => query.OrderByDescending(c => db.ClientPackages.Where(p => p.ClientId == c.Id && p.TotalPrice > p.AmountPaid)
            .Sum(p => p.TotalPrice - p.AmountPaid)).ThenByDescending(c => c.Id),
        _ => TrainerPortalService.OrderPortalClients(db, query, sort)
    };

    // Name/email contain the term; with 3+ digits the term also matches the phone number with separators
    // (space - + ( ) . /) removed in SQL. Npgsql does not translate Regex.Replace, hence the explicit list.
    internal static IQueryable<Client> WhereMatchesSearch(IQueryable<Client> query, string search)
    {
        var term = search.Trim().ToLower();
        var digits = ClientPageFilter.NormalizePhoneSearch(search);
        return digits is null
            ? query.Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(term) || c.Email.ToLower().Contains(term))
            : query.Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(term) || c.Email.ToLower().Contains(term) ||
                (c.PhoneNumber != null && c.PhoneNumber.Replace(" ", "").Replace("-", "").Replace("+", "")
                    .Replace("(", "").Replace(")", "").Replace(".", "").Replace("/", "").Contains(digits)));
    }

    private IQueryable<Client> ApplyClientAccessControl(IQueryable<Client> query)
    {
        if (_currentUser.IsOwner)
            return query;
        if (_currentUser.IsTrainer && _currentUser.UserId.HasValue)
            return query.Where(c => c.Trainer != null && c.Trainer.UserId == _currentUser.UserId.Value);
        return query.Where(c => false);
    }

    public async Task<OwnerFilterOptionsDto> GetFilterOptionsAsync(OwnerFilterOptionsQuery request)
    {
        if (!_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Owner required.");
        request.Validate();
        var term = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim().ToLower();
        var locationId = request.LocationId;

        var clients = _context.Clients.AsNoTracking();
        if (request.GroupAccess.HasValue)
            clients = clients.Where(c => _context.ClientLocationMemberships.Any(m =>
                m.ClientId == c.Id && m.LocationId == locationId!.Value && m.GroupAccessEnabled) == request.GroupAccess.Value);
        else if (locationId.HasValue)
            clients = clients.Where(c => c.LocationId == locationId.Value ||
                _context.ClientLocationMemberships.Any(m => m.ClientId == c.Id && m.LocationId == locationId.Value));
        // Exact filters run before Limit, so the limit never hides matching rows behind non-matching ones.
        if (!string.IsNullOrWhiteSpace(request.ClientStatus))
            clients = clients.Where(c => c.Status == request.ClientStatus);
        if (term is not null)
            clients = WhereMatchesSearch(clients, request.Search!);

        var trainers = _context.Trainers.AsNoTracking();
        if (locationId.HasValue)
            trainers = trainers.Where(t => t.TrainerLocations.Any(l => l.LocationId == locationId.Value));
        if (!string.IsNullOrWhiteSpace(request.TrainerStatus))
            trainers = trainers.Where(t => t.Status == request.TrainerStatus);
        if (term is not null)
            trainers = trainers.Where(t => (t.User.FirstName + " " + t.User.LastName).ToLower().Contains(term) || t.User.Email.ToLower().Contains(term));

        var clientOptions = await clients.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id).Take(request.Limit)
            .Select(c => new FilterOptionDto(c.Id, c.FirstName + " " + c.LastName) { Status = c.Status, IsArchived = false }).ToListAsync();
        var trainerOptions = await SelectTrainerOptions(
            trainers.OrderBy(t => t.User.LastName).ThenBy(t => t.User.FirstName).ThenBy(t => t.Id).Take(request.Limit),
            request.IncludeTrainerDetails).ToListAsync();

        // The currently assigned person stays selectable even when outside the filter, the limit or the archive.
        if (request.SelectedClientId is int selectedClient)
        {
            var existing = clientOptions.FindIndex(o => o.Id == selectedClient);
            if (existing >= 0) clientOptions[existing] = clientOptions[existing] with { IsSelected = true };
            else
            {
                var selected = await _context.Clients.IncludingOrganizationArchive(_context.OrganizationId).AsNoTracking()
                    .Where(c => c.Id == selectedClient)
                    .Select(c => new FilterOptionDto(c.Id, c.FirstName + " " + c.LastName) { Status = c.Status, IsArchived = c.IsDeleted, IsSelected = true })
                    .SingleOrDefaultAsync();
                if (selected is not null) clientOptions.Add(selected);
            }
        }
        if (request.SelectedTrainerId is int selectedTrainer)
        {
            var existing = trainerOptions.FindIndex(o => o.Id == selectedTrainer);
            if (existing >= 0) trainerOptions[existing] = trainerOptions[existing] with { IsSelected = true };
            else
            {
                var selected = await SelectTrainerOptions(_context.Trainers.IncludingOrganizationArchive(_context.OrganizationId).AsNoTracking()
                    .Where(t => t.Id == selectedTrainer), request.IncludeTrainerDetails).SingleOrDefaultAsync();
                if (selected is not null) trainerOptions.Add(selected with { IsSelected = true });
            }
        }

        return new OwnerFilterOptionsDto { Clients = clientOptions, Trainers = trainerOptions };
    }

    private static IQueryable<FilterOptionDto> SelectTrainerOptions(IQueryable<Trainer> trainers, bool details) => details
        ? trainers.Select(t => new FilterOptionDto(t.Id, t.User.FirstName + " " + t.User.LastName)
        {
            Status = t.Status, IsArchived = t.IsDeleted, LocationIds = t.TrainerLocations.Select(l => l.LocationId).ToList(),
            AvatarUrl = t.User.AvatarUrl, Color = t.OutlookCategoryColor
        })
        : trainers.Select(t => new FilterOptionDto(t.Id, t.User.FirstName + " " + t.User.LastName) { Status = t.Status, IsArchived = t.IsDeleted });
}
