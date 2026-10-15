using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientService
{
    public async Task<ClientPageDto> GetPageAsync(ClientPageFilter filter)
    {
        filter.Validate();
        var query = ApplyAccessControl(BuildClientQuery(includeArchived: filter.IsArchived))
            .Where(c => c.IsArchived == filter.IsArchived);

        if (filter.TrainerId.HasValue)
            query = query.Where(c => c.TrainerId == filter.TrainerId.Value);
        if (filter.LocationId.HasValue)
            query = query.Where(c => c.LocationId == filter.LocationId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(c => c.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(term) || c.Email.ToLower().Contains(term));
        }
        if (filter.PackageStatus is not null)
        {
            var active = filter.PackageStatus == "active";
            query = query.Where(c => _context.ClientPackages.Any(p => p.ClientId == c.Id && p.IsActive && p.ClosureDisposition == null) == active);
        }

        var totalCount = await query.CountAsync();
        var activeCount = await query.CountAsync(c => c.Status == "Active");
        var withPackageCount = await query.CountAsync(c =>
            _context.ClientPackages.Any(p => p.ClientId == c.Id && p.IsActive && p.ClosureDisposition == null));

        var ordered = filter.ResolveSort() switch
        {
            "created" => query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),
            "name" => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id),
            "nameDesc" => query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName).ThenByDescending(c => c.Id),
            _ => query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
        };
        var items = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();

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

    public async Task<OwnerFilterOptionsDto> GetFilterOptionsAsync(OwnerFilterOptionsQuery request)
    {
        if (!_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Owner required.");
        request.Validate();
        var term = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim().ToLower();
        var locationId = request.LocationId;

        var clients = _context.Clients.AsNoTracking();
        if (locationId.HasValue)
            clients = clients.Where(c => c.LocationId == locationId.Value ||
                _context.ClientLocationMemberships.Any(m => m.ClientId == c.Id && m.LocationId == locationId.Value));
        if (term is not null)
            clients = clients.Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(term) || c.Email.ToLower().Contains(term));

        var trainers = _context.Trainers.AsNoTracking();
        if (locationId.HasValue)
            trainers = trainers.Where(t => t.TrainerLocations.Any(l => l.LocationId == locationId.Value));
        if (term is not null)
            trainers = trainers.Where(t => (t.User.FirstName + " " + t.User.LastName).ToLower().Contains(term) || t.User.Email.ToLower().Contains(term));

        return new OwnerFilterOptionsDto
        {
            Clients = await clients.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id).Take(request.Limit)
                .Select(c => new FilterOptionDto(c.Id, c.FirstName + " " + c.LastName)).ToListAsync(),
            Trainers = await trainers.OrderBy(t => t.User.LastName).ThenBy(t => t.User.FirstName).ThenBy(t => t.Id).Take(request.Limit)
                .Select(t => new FilterOptionDto(t.Id, t.User.FirstName + " " + t.User.LastName)).ToListAsync()
        };
    }
}
