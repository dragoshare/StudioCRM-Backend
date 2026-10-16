using System.Collections;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;
using StudioCRM.Tests.IntegrationTests;

namespace StudioCRM.Tests.UnitTests;

// Runs whole Owner panel reads against PostgreSQL SQL generation without a database: the connection is never opened
// and every command returns an empty result. Proves translation (no client evaluation errors) and a fixed number of
// SQL commands per request, independent of page size.
public class OwnerPanelSqlTranslationTests
{
    [Theory]
    [InlineData("payments", 2)]
    [InlineData("clientPage", 5)]
    [InlineData("clientPagePhone", 5)]
    [InlineData("filterOptions", 2)]
    [InlineData("settlementList", 5)]
    [InlineData("sessionsPage", 3)]
    [InlineData("groupList", 4)]
    public async Task ReadTranslatesWithFixedCommandCount(string read, int expectedCommands)
    {
        foreach (var pageSize in new[] { 1, 100 })
        {
            var log = new List<string>();
            await using var db = new StudioCRMDbContext(new DbContextOptionsBuilder<StudioCRMDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only").AddInterceptors(new NoConnection(), new EmptyResults(log)).Options,
                null, new PortalSecurityTests.Actor(1, "Owner"), null);
            var owner = new PortalSecurityTests.Actor(1, "Owner");
            Task task = read switch
            {
                "payments" => PortalSecurityTests.Payments(db, owner).GetPaymentsAsync(new ClientPaymentFilterDto
                    { LocationId = 1, LocationMatch = "effective", Overpayment = true, Sort = "clientName", Order = "desc", PageSize = pageSize }),
                "clientPage" => new ClientService(db, owner, null!, null!).GetPageAsync(new ClientPageFilter
                    { TrainerId = 1, LocationId = 1, PackageStatus = "active", Status = "Active", Sort = "packageUsageDesc", PageSize = pageSize }),
                "clientPagePhone" => new ClientService(db, owner, null!, null!).GetPageAsync(new ClientPageFilter
                    { Search = "+48 600 100 200", IsArchived = true, Sort = "archivedAtDesc", PageSize = pageSize }),
                "filterOptions" => new ClientService(db, owner, null!, null!).GetFilterOptionsAsync(new OwnerFilterOptionsQuery
                    { LocationId = 1, GroupAccess = true, ClientStatus = "Active", TrainerStatus = "Active", Search = "600", IncludeTrainerDetails = true, Limit = pageSize }),
                "settlementList" => new TrainerSettlementService(db, owner).GetMonthlySettlementsAsync(new TrainerSettlementListFilter
                    { Year = 2026, Month = 10, LocationId = 1, Search = "a", PageSize = pageSize }),
                "sessionsPage" => new SessionService(db, owner, null!, null!, null!, null!, NullLogger<SessionService>.Instance)
                    .GetFilteredPageAsync(new SessionFilterDto { ClientId = 1, LocationId = 1, Status = "Completed", Page = 1, PageSize = pageSize }),
                "groupList" => new GroupEnrollmentService(db, owner, null!).ListAsync(1, 1, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(read))
            };
            await task;
            Assert.Equal(expectedCommands, log.Count);
        }
    }

    private sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult.Suppress());
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            InterceptionResult.Suppress();
    }

    // COUNT returns one row with 0; everything else returns no rows.
    private sealed class EmptyResults(List<string> log) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            var count = command.CommandText.TrimStart().StartsWith("SELECT count(*)", StringComparison.OrdinalIgnoreCase);
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(new Rows(count ? 1 : 0)));
        }
    }

    private sealed class Rows(int rows) : DbDataReader
    {
        private int read;
        public override bool Read() => read++ < rows;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override int FieldCount => 1;
        public override bool HasRows => rows > 0;
        public override bool IsDBNull(int ordinal) => false;
        public override T GetFieldValue<T>(int ordinal) => default!;
        public override int GetInt32(int ordinal) => 0;
        public override long GetInt64(int ordinal) => 0;
        public override object GetValue(int ordinal) => 0;
        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => ' ';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => "integer";
        public override DateTime GetDateTime(int ordinal) => default;
        public override decimal GetDecimal(int ordinal) => 0;
        public override double GetDouble(int ordinal) => 0;
        public override Type GetFieldType(int ordinal) => typeof(int);
        public override float GetFloat(int ordinal) => 0;
        public override Guid GetGuid(int ordinal) => default;
        public override short GetInt16(int ordinal) => 0;
        public override string GetName(int ordinal) => "count";
        public override int GetOrdinal(string name) => 0;
        public override string GetString(int ordinal) => "";
        public override int GetValues(object[] values) => 0;
        public override object this[int ordinal] => 0;
        public override object this[string name] => 0;
        public override int Depth => 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override bool NextResult() => false;
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }
}
