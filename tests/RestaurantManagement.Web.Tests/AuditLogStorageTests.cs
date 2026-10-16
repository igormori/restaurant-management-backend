using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Audit log storage on a real PostgreSQL database (spec Rules 1–3, Outcome #4): what an entry keeps,
/// where its organization comes from, and that a stored entry can never be changed or removed.
/// Ana is Owner of Cantina da Nonna and is signed in to it.
/// </summary>
public class AuditLogStorageTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string AnaIpAddress = "203.0.113.25";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid CantinaBatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
    private static readonly Guid CantinaSettingsId = Guid.Parse("88888888-0000-4000-8000-000000000001");

    private const string SettingsBefore = "{\"serviceChargeRate\":10,\"discountApprovalLimit\":5,\"businessDayCutoff\":\"04:00\"}";
    private const string SettingsAfter = "{\"serviceChargeRate\":12,\"discountApprovalLimit\":5,\"businessDayCutoff\":\"04:00\"}";

    private static AuditEntryDto SettingsEntry => new()
    {
        Action = AuditActions.SettingsChanged,
        EntityType = "OrganizationSettings",
        EntityId = CantinaSettingsId,
        Before = SettingsBefore,
        After = SettingsAfter
    };

    // How a test tries to tamper with a stored entry; the name is shown in the test results.
    private static readonly Dictionary<string, Action<AuditLogDbContext, AuditLogEntry>> Tampering = new()
    {
        ["change the after value"] = (_, entry) => entry.After = SettingsBefore,
        ["remove the entry"] = (db, entry) => db.AuditLogEntries.Remove(entry)
    };

    public static TheoryData<string> TamperingNames => new() { "change the after value", "remove the entry" };

    private readonly PostgresDatabaseFixture _postgres;

    public AuditLogStorageTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    /// <summary>Records an entry the way a request would: Ana's token for Cantina and her network address.</summary>
    private static async Task RecordAsAnaInCantinaAsync(TenantApiFactory factory, AuditEntryDto entry)
    {
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, AnaId.ToString()),
            new Claim(AppClaimTypes.OrganizationId, CantinaId.ToString())
        }, "Test"));
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(AnaIpAddress);
        accessor.HttpContext = httpContext;
        try
        {
            await scope.ServiceProvider.GetRequiredService<IAuditLog>().RecordAsync(entry);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    private static Task<List<AuditLogEntry>> ReadAllEntriesAsync(TenantApiFactory factory)
        => factory.InOrganizationAsync<AuditLogDbContext, List<AuditLogEntry>>(CantinaId,
            db => db.AuditLogEntries.IgnoreQueryFilters().AsNoTracking().ToListAsync());

    // Scenario 4
    [Theory]
    [MemberData(nameof(TamperingNames))]
    public async Task SaveChanges_StoredEntryChangedOrRemoved_IsRefusedAndRowUnchanged(string tampering)
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await RecordAsAnaInCantinaAsync(factory, SettingsEntry);
        var storedId = (await ReadAllEntriesAsync(factory)).ShouldHaveSingleItem().Id;

        // Act
        var save = () => factory.InOrganizationAsync<AuditLogDbContext, int>(CantinaId, async db =>
        {
            var entry = await db.AuditLogEntries.SingleAsync(e => e.Id == storedId);
            Tampering[tampering](db, entry);
            return await db.SaveChangesAsync();
        });

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(save);
        var stored = (await ReadAllEntriesAsync(factory)).ShouldHaveSingleItem();
        stored.Id.ShouldBe(storedId);
        stored.After.ShouldBe(SettingsAfter);
    }

    // Scenario 1
    [Fact]
    public async Task RecordAsync_AllFieldsGiven_StoresEveryField()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        var entry = new AuditEntryDto
        {
            Action = AuditActions.LocationStatusChanged,
            EntityType = "Location",
            EntityId = CantinaBatelId,
            LocationId = CantinaBatelId,
            Before = "Active",
            After = "TemporarilyClosed",
            Reason = "Reforma da cozinha"
        };

        // Act
        await RecordAsAnaInCantinaAsync(factory, entry);

        // Assert
        var stored = (await ReadAllEntriesAsync(factory)).ShouldHaveSingleItem();
        stored.OrganizationId.ShouldBe(CantinaId);
        stored.LocationId.ShouldBe(CantinaBatelId);
        stored.CreatedByUserId.ShouldBe(AnaId);
        stored.Action.ShouldBe(AuditActions.LocationStatusChanged);
        stored.EntityType.ShouldBe("Location");
        stored.EntityId.ShouldBe(CantinaBatelId);
        stored.Before.ShouldBe("Active");
        stored.After.ShouldBe("TemporarilyClosed");
        stored.Reason.ShouldBe("Reforma da cozinha");
        stored.IpAddress.ShouldBe(AnaIpAddress);
        stored.CreatedAt.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    // Scenario 2
    [Fact]
    public async Task RecordAsync_NoLocationReasonOrBeforeValue_StoresEntryWithThoseEmpty()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        var entry = new AuditEntryDto
        {
            Action = AuditActions.SettingsChanged,
            EntityType = "OrganizationSettings",
            EntityId = CantinaSettingsId,
            After = SettingsAfter
        };

        // Act
        await RecordAsAnaInCantinaAsync(factory, entry);

        // Assert
        var stored = (await ReadAllEntriesAsync(factory)).ShouldHaveSingleItem();
        stored.LocationId.ShouldBeNull();
        stored.Before.ShouldBeNull();
        stored.Reason.ShouldBeNull();
        stored.After.ShouldBe(SettingsAfter);
    }

    // Scenario 3
    [Fact]
    public async Task RecordAsync_ChangedItemBelongsToAnotherOrganization_StoresEntryInTokenOrganization()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        var entry = new AuditEntryDto
        {
            Action = AuditActions.LocationStatusChanged,
            EntityType = "Location",
            EntityId = TrattoriaCentroId,
            LocationId = TrattoriaCentroId,
            Before = "Active",
            After = "Closed"
        };

        // Act
        await RecordAsAnaInCantinaAsync(factory, entry);

        // Assert
        var stored = (await ReadAllEntriesAsync(factory)).ShouldHaveSingleItem();
        stored.OrganizationId.ShouldBe(CantinaId);
    }

    // Scenario 7
    [Fact]
    public async Task ReadEntries_EntryRecordedInCantinaReadInTrattoriaScope_FindsNothing()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await RecordAsAnaInCantinaAsync(factory, SettingsEntry);

        // Act
        var visibleInTrattoria = await factory.InOrganizationAsync<AuditLogDbContext, List<AuditLogEntry>>(TrattoriaId,
            db => db.AuditLogEntries.AsNoTracking().ToListAsync());

        // Assert
        visibleInTrattoria.ShouldBeEmpty();
    }

    // Scenario 5
    [Fact]
    public async Task Migrate_AuditLogMigrations_CreatesOrganizationAndTimeIndex()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);

        // Act
        var indexDefinitions = await factory.InOrganizationAsync<AuditLogDbContext, List<string>>(CantinaId, db =>
            db.Database.SqlQuery<string>(
                $"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'audit_log_entries'").ToListAsync());

        // Assert
        indexDefinitions.ShouldContain(definition => definition.Contains("(organization_id, created_at"));
    }

    // Scenario 6
    [Fact]
    public async Task Migrate_AuditLogMigrations_ModelHasNoPendingChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseNpgsql(await _postgres.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var context = new AuditLogDbContext(options, NoTenant.Context);

        // Act
        await context.Database.MigrateAsync();

        // Assert
        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}
