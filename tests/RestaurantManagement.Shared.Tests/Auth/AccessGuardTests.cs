using Microsoft.Extensions.Localization;
using NSubstitute;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Auth;

/// <summary>
/// The request-scoped access guard: reads the token user's grid in the token organization (substituted
/// lookups here) and refuses with 403 when the grid does not allow the action. Cantina da Nonna has
/// Batel and Mercado Municipal.
/// </summary>
public class AccessGuardTests
{
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid MercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");

    private static IAccessGuard CreateGuard(AccessGridDto grid, Guid? tokenOrganization, params Guid[] nonClosedLocations)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(MariaId);
        currentUser.OrganizationId.Returns(tokenOrganization);
        var memberAccess = Substitute.For<IMemberAccessLookup>();
        memberAccess.GetAccessGridAsync(MariaId, CantinaId).Returns(grid);
        var organizations = Substitute.For<IOrganizationLookup>();
        organizations.GetNonClosedLocationIdsAsync(CantinaId).Returns(nonClosedLocations.ToList());
        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        return new AccessGuard(currentUser, memberAccess, organizations, localizer);
    }

    private static AccessGridDto Owner => new() { IsOwner = true };

    private static AccessGridDto NoCells => new();

    // The checks a service can ask for; the name is shown in the test results.
    private static readonly Dictionary<string, Func<IAccessGuard, Task>> Checks = new()
    {
        ["owner only"] = guard => guard.RequireOwnerAsync(),
        ["a level at one location"] = guard => guard.RequireAtLocationAsync(AccessModule.Menus, AccessLevel.Manage, BatelId),
        ["a level at a set of locations"] = guard => guard.RequireAtLocationsAsync(AccessModule.Menus, AccessLevel.Manage, new[] { BatelId, MercadoId }),
        ["a level at no location at all"] = guard => guard.RequireAtLocationsAsync(AccessModule.Menus, AccessLevel.Read, Array.Empty<Guid>()),
        ["a level at every non-closed location"] = guard => guard.RequireAtEveryLocationAsync(AccessModule.Reports, AccessLevel.Read),
        ["any access"] = guard => guard.RequireAnyAccessAsync()
    };

    public static TheoryData<string> CheckNames => new(Checks.Keys);

    [Theory]
    [MemberData(nameof(CheckNames))]
    [Trait("Rule", "SH39")]
    public async Task Require_OwnerWithNoStoredCells_PassesEveryCheck(string check)
    {
        // Arrange
        var guard = CreateGuard(Owner, CantinaId, BatelId, MercadoId);

        // Act
        var act = () => Checks[check](guard);

        // Assert
        await Should.NotThrowAsync(act, "the Owner has Manage on everything at every location with no cell, and the empty set passes the Owner too");
    }

    [Theory]
    [MemberData(nameof(CheckNames))]
    [Trait("Rule", "SH40")]
    public async Task Require_MemberWithNoStoredCells_IsRefusedEverywhere(string check)
    {
        // Arrange
        var guard = CreateGuard(NoCells, CantinaId, BatelId, MercadoId);

        // Act
        var act = () => Checks[check](guard);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403, "a new Admin has no access anywhere until the Owner grants it");
        exception.Message.ShouldBe("AccessDenied", "the refusal carries the translated access denied message");
    }

    [Fact]
    [Trait("Rule", "SH41")]
    public async Task Require_NoOrganizationInTheToken_Throws403OrganizationRequired()
    {
        // Arrange
        var guard = CreateGuard(Owner, tokenOrganization: null);

        // Act
        var act = () => guard.RequireAnyAccessAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403, "without a selected organization nothing is allowed, not even for an Owner of other organizations");
        exception.Message.ShouldBe("OrganizationRequired", "the refusal says an organization is required");
    }

    [Fact]
    [Trait("Rule", "SH42")]
    public async Task RequireAtEveryLocationAsync_OrganizationWithNoNonClosedLocation_RefusesAMemberWithLevels()
    {
        // Arrange
        var grid = new AccessGridDto
        {
            Cells = new[] { new AccessCellDto { LocationId = BatelId, Module = AccessModule.Reports, Level = AccessLevel.Manage } }
        };
        var guard = CreateGuard(grid, CantinaId);

        // Act
        var act = () => guard.RequireAtEveryLocationAsync(AccessModule.Reports, AccessLevel.Read);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403, "an 'every location' check with no non-closed location to hold a level at passes only the Owner");
    }
}
