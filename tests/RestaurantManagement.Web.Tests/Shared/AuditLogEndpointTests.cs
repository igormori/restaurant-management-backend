using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;
using static RestaurantManagement.Web.Tests.LocalizedResponses;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// GET api/audit-log on a real PostgreSQL database: only the token organization's entries, only
/// with Reports (Read or Manage) at every non-closed location of that organization or as Owner,
/// filtered by date and action, newest first, paged. Entries are seeded at fixed times; Ana is the
/// Owner of Cantina da Nonna and is signed in to it.
/// </summary>
public class AuditLogEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string Cantina = "a1a1a1a1-0000-4000-8000-000000000001";
    private const string AuditLogUrl = "/api/audit-log";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse(Cantina);
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid CantinaBatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid CantinaMercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
    private static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
    private static readonly Guid BiaId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");

    private static readonly Guid CantinaSettingsOct01 = Guid.Parse("cccccccc-0000-4000-8000-000000000001");
    private static readonly Guid CantinaStatusOct10 = Guid.Parse("cccccccc-0000-4000-8000-000000000002");
    private static readonly Guid CantinaSettingsOct12 = Guid.Parse("cccccccc-0000-4000-8000-000000000003");
    private static readonly Guid CantinaStatusOct14 = Guid.Parse("cccccccc-0000-4000-8000-000000000004");
    private static readonly Guid CantinaStatusOct15 = Guid.Parse("cccccccc-0000-4000-8000-000000000005");
    private static readonly Guid TrattoriaSettingsOct12 = Guid.Parse("dddddddd-0000-4000-8000-000000000001");

    private static readonly DateTime Oct01 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct10 = new(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct12 = new(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct14 = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct15 = new(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _postgres;

    public AuditLogEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private sealed record EntryRow(Guid Id, string Action, DateTime CreatedAt);

    private sealed record AuditPage(List<EntryRow> Items, int Page, int PageSize, int TotalCount);

    private static AuditLogEntry Entry(Guid id, Guid organizationId, string action, DateTime at) => new()
    {
        Id = id,
        OrganizationId = organizationId,
        LocationId = action == AuditActions.LocationStatusChanged ? CantinaBatelId : null,
        Action = action,
        EntityType = action == AuditActions.LocationStatusChanged ? "Location" : "OrganizationSettings",
        EntityId = Guid.NewGuid(),
        Before = "Active",
        After = "TemporarilyClosed",
        IpAddress = "203.0.113.25",
        CreatedByUserId = AnaId,
        CreatedAt = at,
        UpdatedAt = at
    };

    /// <summary>Five Cantina entries (seeded out of time order) and one Trattoria entry.</summary>
    private async Task<TenantApiFactory> CreateFactoryWithEntriesAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana", TenantApiFactory.OwnerOf(CantinaId));
        await factory.SeedAuditEntriesAsync(
            Entry(CantinaStatusOct14, CantinaId, AuditActions.LocationStatusChanged, Oct14),
            Entry(CantinaSettingsOct01, CantinaId, AuditActions.SettingsChanged, Oct01),
            Entry(CantinaStatusOct15, CantinaId, AuditActions.LocationStatusChanged, Oct15),
            Entry(CantinaStatusOct10, CantinaId, AuditActions.LocationStatusChanged, Oct10),
            Entry(CantinaSettingsOct12, CantinaId, AuditActions.SettingsChanged, Oct12),
            Entry(TrattoriaSettingsOct12, TrattoriaId, AuditActions.SettingsChanged, Oct12));
        return factory;
    }

    private static async Task<AuditPage> ReadPageAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<AuditPage>())!;

    private static Guid? ParseOrganization(string? organization) => organization is null ? null : Guid.Parse(organization);

    /// <summary>
    /// Cantina da Nonna with its two locations (Batel and Mercado Municipal), Bia as a member
    /// without the Owner flag, and the audit entries. What Bia may do comes only from her stored levels.
    /// </summary>
    private async Task<TenantApiFactory> CreateFactoryWithBiaAsMemberAsync()
    {
        var factory = await CreateFactoryWithEntriesAsync();
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, CantinaBatelId, "Unidade Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(CantinaId, CantinaMercadoId, "Unidade Mercado Municipal", "MERCADO", "America/Sao_Paulo");
        await factory.SeedLocationAsync(TrattoriaId, TrattoriaCentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedUserAsync(BiaId, "bia@cantinadanonna.com.br", "Bia",
            TenantApiFactory.MemberOf(CantinaId), TenantApiFactory.MemberOf(TrattoriaId));
        return factory;
    }

    private static (AccessModule, AccessLevel)[] CashierPresetLevels => new[]
    {
        (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read),
        (AccessModule.Cashier, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read)
    };

    private static (AccessModule, AccessLevel)[] WaiterPresetLevels => new[]
    {
        (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Orders, AccessLevel.Manage)
    };

    private static (AccessModule, AccessLevel)[] KitchenPresetLevels => new[]
    {
        (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read),
        (AccessModule.Orders, AccessLevel.Read), (AccessModule.Kitchen, AccessLevel.Manage)
    };

    // Who is refused and what they hold: the Reports level is None or missing at one of the two locations.
    private static readonly Dictionary<string, Func<TenantApiFactory, Task>> MembersWithoutReports = new()
    {
        ["Cashier"] = f => GrantAtBothLocationsAsync(f, CashierPresetLevels),
        ["Waiter"] = f => GrantAtBothLocationsAsync(f, WaiterPresetLevels),
        ["Kitchen"] = f => GrantAtBothLocationsAsync(f, KitchenPresetLevels),
        ["NewAdminWithNoAccess"] = _ => Task.CompletedTask,
        ["ManagerWithReportsAtBatelOnly"] = f => f.GrantAsync(BiaId, CantinaId, CantinaBatelId, (AccessModule.Reports, AccessLevel.Manage))
    };

    public static TheoryData<string> PresetsWithoutReports => new(MembersWithoutReports.Keys);

    private static async Task GrantAtBothLocationsAsync(TenantApiFactory factory, (AccessModule, AccessLevel)[] levels)
    {
        await factory.GrantAsync(BiaId, CantinaId, CantinaBatelId, levels);
        await factory.GrantAsync(BiaId, CantinaId, CantinaMercadoId, levels);
    }

    [Theory]
    [MemberData(nameof(PresetsWithoutReports))]
    [Trait("Rule", "SH12")]
    public async Task GetAuditLog_MemberWithoutReportsAtEveryNonClosedLocation_ReturnsForbidden(string member)
    {
        // Arrange
        await using var factory = await CreateFactoryWithBiaAsMemberAsync();
        await MembersWithoutReports[member](factory);
        var client = factory.ClientFor(BiaId, CantinaId);

        // Act
        var response = await client.GetAsync(AuditLogUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a member whose Reports level is None, who has no access at all, or who has Reports at only one of two locations, may not read the audit log");
    }

    [Fact]
    [Trait("Rule", "SH13")]
    public async Task GetAuditLog_ReportsManageEverywhereOnlyInAnotherOrganization_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryWithBiaAsMemberAsync();
        await factory.GrantAsync(BiaId, TrattoriaId, TrattoriaCentroId, (AccessModule.Reports, AccessLevel.Manage));
        var client = factory.ClientFor(BiaId, CantinaId);
        var otherOrganizationOnly = await client.GetAsync(AuditLogUrl);
        await factory.GrantAsync(BiaId, CantinaId, CantinaBatelId, (AccessModule.Reports, AccessLevel.Manage));
        await factory.GrantAsync(BiaId, CantinaId, CantinaMercadoId, (AccessModule.Reports, AccessLevel.Manage));

        // Act
        var afterGrantHere = await client.GetAsync(AuditLogUrl);

        // Assert
        otherOrganizationOnly.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "Reports Manage everywhere in Trattoria do Sol gives nothing in Cantina da Nonna");
        afterGrantHere.StatusCode.ShouldBe(HttpStatusCode.OK, "the same token reads the audit log once Reports is granted at every location of Cantina da Nonna");
    }

    [Fact]
    [Trait("Rule", "SH12")]
    public async Task GetAuditLog_NoToken_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(AuditLogUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("from=2026-10-14T00:00:00Z&to=2026-10-10T00:00:00Z", "AuditLogInvalidDateRange")]
    [InlineData("action=order.cancelled", "AuditLogUnknownAction")]
    [Trait("Rule", "SH14")]
    public async Task GetAuditLog_InvalidFilter_ReturnsBadRequestWithLocalizedMessage(string query, string messageKey)
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.GetAsync($"{AuditLogUrl}?{query}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MessageAsync(response)).ShouldBe(factory.Message(messageKey));
    }

    [Fact]
    [Trait("Rule", "SH13")]
    public async Task GetAuditLog_EntriesInTwoOrganizationsAndQueryNamesTheOther_ReturnsOnlyTokenOrganizationsEntries()
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.GetAsync($"{AuditLogUrl}?organizationId={TrattoriaId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadPageAsync(response)).Items.Select(e => e.Id).ShouldBe(
            new[] { CantinaSettingsOct01, CantinaStatusOct10, CantinaSettingsOct12, CantinaStatusOct14, CantinaStatusOct15 },
            ignoreOrder: true);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("AdminWithReportsManage")]
    [InlineData("ManagerWithReportsRead")]
    [Trait("Rule", "SH12")]
    public async Task GetAuditLog_OwnerOrReportsAtEveryLocation_ReturnsOk(string member)
    {
        // Arrange
        await using var factory = await CreateFactoryWithBiaAsMemberAsync();
        var userId = member == "Owner" ? AnaId : BiaId;
        var level = member == "AdminWithReportsManage" ? AccessLevel.Manage : AccessLevel.Read;
        foreach (var location in new[] { CantinaBatelId, CantinaMercadoId })
            await factory.GrantAsync(BiaId, CantinaId, location, (AccessModule.Reports, level));
        var client = factory.ClientFor(userId, CantinaId);

        // Act
        var response = await client.GetAsync(AuditLogUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the Owner, and a member with Reports Read or Manage at every non-closed location, read the audit log");
    }

    public static TheoryData<string, Guid[]> Filters => new()
    {
        { "from=2026-10-10T09:00:00Z&to=2026-10-14T18:00:00Z", new[] { CantinaStatusOct10, CantinaSettingsOct12, CantinaStatusOct14 } },
        { $"action={AuditActions.SettingsChanged}", new[] { CantinaSettingsOct01, CantinaSettingsOct12 } }
    };

    [Theory]
    [MemberData(nameof(Filters))]
    [Trait("Rule", "SH14")]
    public async Task GetAuditLog_FilterGiven_ReturnsOnlyMatchingEntriesIncludingRangeEnds(string query, Guid[] expectedIds)
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.GetAsync($"{AuditLogUrl}?{query}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadPageAsync(response)).Items.Select(e => e.Id).ShouldBe(expectedIds, ignoreOrder: true);
    }

    [Fact]
    [Trait("Rule", "SH14")]
    public async Task GetAuditLog_NoFilters_ReturnsEntriesNewestFirst()
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.GetAsync(AuditLogUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadPageAsync(response)).Items.Select(e => e.Id).ShouldBe(
            new[] { CantinaStatusOct15, CantinaStatusOct14, CantinaSettingsOct12, CantinaStatusOct10, CantinaSettingsOct01 });
    }

    [Theory]
    [InlineData("pageSize=500", 100)]
    [InlineData("page=1", 50)]
    [Trait("Rule", "SH14")]
    public async Task GetAuditLog_PageSizeAboveLimitOrNotGiven_ReturnsCappedOrDefaultPage(string query, int expectedPageSize)
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana", TenantApiFactory.OwnerOf(CantinaId));
        await factory.SeedAuditEntriesAsync(Enumerable.Range(0, 101)
            .Select(minute => Entry(Guid.NewGuid(), CantinaId, AuditActions.LocationStatusChanged, Oct01.AddMinutes(minute)))
            .ToArray());
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.GetAsync($"{AuditLogUrl}?{query}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadPageAsync(response);
        page.Items.Count.ShouldBe(expectedPageSize);
        page.PageSize.ShouldBe(expectedPageSize);
        page.TotalCount.ShouldBe(101);
    }
}
