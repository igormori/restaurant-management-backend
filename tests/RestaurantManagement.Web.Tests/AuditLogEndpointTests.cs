using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// GET api/audit-log on a real PostgreSQL database (spec Rules 4–5, Outcomes #2 and #3): only the token
/// organization's entries, only with the reports.view permission, filtered by date and action, newest
/// first, paged. Entries are seeded at fixed times; Ana is signed in to Cantina da Nonna.
/// </summary>
public class AuditLogEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string Cantina = "a1a1a1a1-0000-4000-8000-000000000001";
    private const string AuditLogUrl = "/api/audit-log";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse(Cantina);
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid CantinaBatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");

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

    // Scenario 9
    [Theory]
    [InlineData(Roles.Cashier, Cantina)]
    [InlineData(Roles.Waiter, Cantina)]
    [InlineData(Roles.Kitchen, Cantina)]
    [InlineData(Roles.Owner, null)]
    public async Task GetAuditLog_WithoutReportsViewInSelectedOrganization_ReturnsForbidden(string role, string? organization)
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, ParseOrganization(organization), role);

        // Act
        var response = await client.GetAsync(AuditLogUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Scenario 10
    [Fact]
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

    // Scenario 15
    [Theory]
    [InlineData("from=2026-10-14T00:00:00Z&to=2026-10-10T00:00:00Z", "AuditLogInvalidDateRange")]
    [InlineData("action=order.cancelled", "AuditLogUnknownAction")]
    public async Task GetAuditLog_InvalidFilter_ReturnsBadRequestWithLocalizedMessage(string query, string messageKey)
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync($"{AuditLogUrl}?{query}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MessageAsync(response)).ShouldBe(factory.Message(messageKey));
    }

    // Scenario 8
    [Fact]
    public async Task GetAuditLog_EntriesInTwoOrganizationsAndQueryNamesTheOther_ReturnsOnlyTokenOrganizationsEntries()
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync($"{AuditLogUrl}?organizationId={TrattoriaId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadPageAsync(response)).Items.Select(e => e.Id).ShouldBe(
            new[] { CantinaSettingsOct01, CantinaStatusOct10, CantinaSettingsOct12, CantinaStatusOct14, CantinaStatusOct15 },
            ignoreOrder: true);
    }

    // Scenario 11
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    public async Task GetAuditLog_RoleWithReportsView_ReturnsOk(string role)
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId, role);

        // Act
        var response = await client.GetAsync(AuditLogUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public static TheoryData<string, Guid[]> Filters => new()
    {
        { "from=2026-10-10T09:00:00Z&to=2026-10-14T18:00:00Z", new[] { CantinaStatusOct10, CantinaSettingsOct12, CantinaStatusOct14 } },
        { $"action={AuditActions.SettingsChanged}", new[] { CantinaSettingsOct01, CantinaSettingsOct12 } }
    };

    // Scenario 12
    [Theory]
    [MemberData(nameof(Filters))]
    public async Task GetAuditLog_FilterGiven_ReturnsOnlyMatchingEntriesIncludingRangeEnds(string query, Guid[] expectedIds)
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync($"{AuditLogUrl}?{query}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadPageAsync(response)).Items.Select(e => e.Id).ShouldBe(expectedIds, ignoreOrder: true);
    }

    // Scenario 13
    [Fact]
    public async Task GetAuditLog_NoFilters_ReturnsEntriesNewestFirst()
    {
        // Arrange
        await using var factory = await CreateFactoryWithEntriesAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync(AuditLogUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadPageAsync(response)).Items.Select(e => e.Id).ShouldBe(
            new[] { CantinaStatusOct15, CantinaStatusOct14, CantinaSettingsOct12, CantinaStatusOct10, CantinaSettingsOct01 });
    }

    // Scenario 14
    [Theory]
    [InlineData("pageSize=500", 100)]
    [InlineData("page=1", 50)]
    public async Task GetAuditLog_PageSizeAboveLimitOrNotGiven_ReturnsCappedOrDefaultPage(string query, int expectedPageSize)
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedAuditEntriesAsync(Enumerable.Range(0, 101)
            .Select(minute => Entry(Guid.NewGuid(), CantinaId, AuditActions.LocationStatusChanged, Oct01.AddMinutes(minute)))
            .ToArray());
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

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
