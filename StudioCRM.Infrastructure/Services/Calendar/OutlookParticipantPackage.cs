using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

// Package pre-assigned to a client added from Outlook: a group package for group classes, a personal/semi
// package otherwise, valid at the session's location and not closed. Completion still re-resolves the package
// with the full rules; this keeps the planned price and package shown before completion correct.
internal static class OutlookParticipantPackage
{
    public static Task<ClientPackage?> FindAsync(StudioCRMDbContext context, int clientId, Session session)
    {
        var group = GroupClassSessions.IsGroup(session.PlannedSessionType);
        return context.ClientPackages
            .Where(cp => cp.ClientId == clientId && cp.IsActive && cp.ClosureDisposition == null &&
                cp.BillingMode == PackageBillingMode.SessionBased &&
                (group ? cp.ExpectedBillingType == SessionBillingType.Group : cp.ExpectedBillingType != SessionBillingType.Group) &&
                (cp.LocationId == null || cp.LocationId == session.LocationId))
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();
    }
}
