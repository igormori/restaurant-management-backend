using System.Net;
using System.Net.Http.Headers;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using NSubstitute;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// One access policy per module and level (for example "reports.read"): met only when the pass
/// names the selected organization and the stored grid gives at least that level at every non-closed
/// location of it, or the user is the Owner. The grid lookup is substituted; the real guard decides.
/// </summary>
public class PermissionPolicyTests : IDisposable
{
    private const string MembersUrl = "/api/test-probe/members";
    private const string MenuUrl = "/api/test-probe/menu";
    private const string ReportsUrl = "/api/test-probe/reports";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid MercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");

    private readonly PermissionProbeApiFactory _factory = new();

    public PermissionPolicyTests()
    {
        _factory.Organizations.GetNonClosedLocationIdsAsync(CantinaId).Returns(new List<Guid> { BatelId, MercadoId });
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static AccessGridDto Grid(params (Guid Location, AccessModule Module, AccessLevel Level)[] cells) => new()
    {
        Cells = cells.Select(c => new AccessCellDto { LocationId = c.Location, Module = c.Module, Level = c.Level }).ToList()
    };

    private static AccessGridDto Owner => new() { IsOwner = true };

    public static TheoryData<string, AccessGridDto> GridsThatAreRefused => new()
    {
        { MembersUrl, Grid() },
        { MembersUrl, Grid((BatelId, AccessModule.Members, AccessLevel.Read), (MercadoId, AccessModule.Members, AccessLevel.Read)) },
        { MembersUrl, Grid((BatelId, AccessModule.Members, AccessLevel.Manage)) },
        { ReportsUrl, Grid((BatelId, AccessModule.Locations, AccessLevel.Read), (BatelId, AccessModule.Orders, AccessLevel.Manage), (MercadoId, AccessModule.Orders, AccessLevel.Manage)) }
    };

    public static TheoryData<string, AccessGridDto> GridsThatAreAllowed => new()
    {
        { MembersUrl, Owner },
        { MembersUrl, Grid((BatelId, AccessModule.Members, AccessLevel.Manage), (MercadoId, AccessModule.Members, AccessLevel.Manage)) },
        { ReportsUrl, Grid((BatelId, AccessModule.Reports, AccessLevel.Read), (MercadoId, AccessModule.Reports, AccessLevel.Manage)) },
        { MenuUrl, Grid((BatelId, AccessModule.Menus, AccessLevel.Manage), (MercadoId, AccessModule.Menus, AccessLevel.Manage)) }
    };

    private HttpRequestMessage SignedInGet(string url, Guid? organizationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, MariaId, organizationId));
        return request;
    }

    [Fact]
    [Trait("Rule", "W5")]
    public async Task MembersManagePolicy_NoAccessToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(MembersUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a caller with no access token is not signed in and must be refused");
    }

    [Fact]
    [Trait("Rule", "W6")]
    public async Task MembersManagePolicy_OwnerButNoOrganizationSelected_ReturnsForbidden()
    {
        // Arrange: a user of two organizations who has not selected one gets a token with no organization
        _factory.MemberAccess.GetAccessGridAsync(MariaId, Arg.Any<Guid>()).Returns(Owner);
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(MembersUrl, organizationId: null));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "access only counts inside the selected organization, so with none selected it is refused even for an Owner");
    }

    [Theory]
    [MemberData(nameof(GridsThatAreRefused))]
    [Trait("Rule", "W7")]
    [Trait("Rule", "W8")]
    public async Task Policy_GridWithoutTheLevelAtEveryLocation_ReturnsForbidden(string url, AccessGridDto grid)
    {
        // Arrange
        _factory.MemberAccess.GetAccessGridAsync(MariaId, CantinaId).Returns(grid);
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(url, CantinaId));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "no access, a lower level, or the level at only one of two locations must be refused");
    }

    [Theory]
    [MemberData(nameof(GridsThatAreAllowed))]
    [Trait("Rule", "W9")]
    public async Task Policy_OwnerOrLevelAtEveryNonClosedLocation_ReturnsOk(string url, AccessGridDto grid)
    {
        // Arrange
        _factory.MemberAccess.GetAccessGridAsync(MariaId, CantinaId).Returns(grid);
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(url, CantinaId));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the Owner, or the needed level or more at every non-closed location, must get access");
    }
}
