using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

// One active MilestoneDefinition reduced to a tenure-start bound: achieved <=> (TrainingStartDate ?? CreatedAt) < StartBefore.
internal sealed record MilestoneThreshold(int DefinitionId, DateTime StartBefore);

// Milestone reward counts for client lists, computed from definitions and tenure on every read, so definitions added
// or changed later and lazily created ClientMilestone rows never need a backfill. Mirrors MilestoneService:
// achieved when (TrainingStartDate ?? CreatedAt).Date.AddMonths(RequiredMonths) <= UtcNow.Date (active definitions only);
// claimed = ClientMilestone.IsRewardClaimed (any definition, also deactivated ones); pending = achieved and not claimed.
internal static class ClientMilestoneRewards
{
    internal static async Task<List<MilestoneThreshold>> LoadThresholdsAsync(StudioCRMDbContext db)
    {
        var today = DateTime.UtcNow.Date;
        var definitions = await db.MilestoneDefinitions.AsNoTracking().Where(d => d.IsActive)
            .OrderBy(d => d.Id).Select(d => new { d.Id, d.RequiredMonths }).ToListAsync();
        return definitions.Select(d => new MilestoneThreshold(d.Id, StartBefore(today, d.RequiredMonths))).ToList();
    }

    // Exclusive bound such that start.Date < bound <=> start.Date.AddMonths(months) <= today (AddMonths clamps month ends).
    internal static DateTime StartBefore(DateTime today, int months)
    {
        var last = today.Date.AddMonths(-months);
        while (last.AddDays(1).AddMonths(months) <= today.Date) last = last.AddDays(1);
        return DateTime.SpecifyKind(last.AddDays(1), DateTimeKind.Utc);
    }

    internal static Expression<Func<Client, int>> Claimed(StudioCRMDbContext db) =>
        c => db.ClientMilestones.Count(m => m.ClientId == c.Id && m.IsRewardClaimed);

    internal static Expression<Func<Client, int>> Achieved(IReadOnlyList<MilestoneThreshold> thresholds) =>
        Sum(thresholds, t => c => (c.TrainingStartDate ?? c.CreatedAt) < t.StartBefore ? 1 : 0);

    internal static Expression<Func<Client, int>> Pending(StudioCRMDbContext db, IReadOnlyList<MilestoneThreshold> thresholds) =>
        Sum(thresholds, t => c => (c.TrainingStartDate ?? c.CreatedAt) < t.StartBefore &&
            !db.ClientMilestones.Any(m => m.ClientId == c.Id && m.MilestoneDefinitionId == t.DefinitionId && m.IsRewardClaimed) ? 1 : 0);

    internal static IQueryable<Client> WhereHasPending(StudioCRMDbContext db, IQueryable<Client> query, IReadOnlyList<MilestoneThreshold> thresholds)
    {
        var parameter = Expression.Parameter(typeof(Client), "c");
        var pending = Pending(db, thresholds);
        var body = Expression.GreaterThan(Rebind(pending, parameter), Expression.Constant(0));
        return query.Where(Expression.Lambda<Func<Client, bool>>(body, parameter));
    }

    // Page rows: the same rules evaluated in memory with two queries for the whole page.
    internal static async Task<Dictionary<int, (int Achieved, int Claimed, int Pending)>> ReadAsync(StudioCRMDbContext db, int[] ids)
    {
        var thresholds = await LoadThresholdsAsync(db);
        var starts = await db.Clients.IncludingOrganizationArchive(db.OrganizationId).AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, Start = c.TrainingStartDate ?? c.CreatedAt }).ToDictionaryAsync(x => x.Id, x => x.Start);
        var claimed = (await db.ClientMilestones.AsNoTracking().Where(m => ids.Contains(m.ClientId) && m.IsRewardClaimed)
            .Select(m => new { m.ClientId, m.MilestoneDefinitionId }).ToListAsync()).ToLookup(m => m.ClientId, m => m.MilestoneDefinitionId);
        var result = new Dictionary<int, (int, int, int)>();
        foreach (var id in ids)
        {
            if (!starts.TryGetValue(id, out var start)) continue;
            var achieved = thresholds.Where(t => start < t.StartBefore).ToList();
            var claimedIds = claimed[id].ToHashSet();
            result[id] = (achieved.Count, claimedIds.Count, achieved.Count(t => !claimedIds.Contains(t.DefinitionId)));
        }
        return result;
    }

    private static Expression<Func<Client, int>> Sum(IReadOnlyList<MilestoneThreshold> thresholds,
        Func<MilestoneThreshold, Expression<Func<Client, int>>> term)
    {
        var parameter = Expression.Parameter(typeof(Client), "c");
        Expression body = Expression.Constant(0);
        foreach (var threshold in thresholds)
            body = Expression.Add(body, Rebind(term(threshold), parameter));
        return Expression.Lambda<Func<Client, int>>(body, parameter);
    }

    private static Expression Rebind<T>(Expression<Func<Client, T>> lambda, ParameterExpression parameter) =>
        new ParameterReplacer(lambda.Parameters[0], parameter).Visit(lambda.Body);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
