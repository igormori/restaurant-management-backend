using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using NSubstitute;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Middleware;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Middleware;

/// <summary>
/// The X-Location-Id check on signed-in requests (Rule 4). Endpoint behaviour through the real
/// pipeline is covered in Web.Tests (LocationAccessEndpointTests).
/// </summary>
public class LocationAccessMiddlewareTests
{
    private const string HeaderName = "X-Location-Id";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid BatelLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private const string MercadoMunicipalLocation = "44444444-dddd-4ddd-8ddd-000000000002";
    private const string UnknownLocation = "44444444-dddd-4ddd-8ddd-0000000000ff";
    private static readonly Guid MercadoMunicipalLocationId = Guid.Parse(MercadoMunicipalLocation);

    private readonly IOrganizationLookup _organizationLookup = Substitute.For<IOrganizationLookup>();
    private readonly IMemberAccessLookup _memberAccess = Substitute.For<IMemberAccessLookup>();
    private readonly SelectedLocation _selectedLocation = new();
    private bool _nextCalled;

    public LocationAccessMiddlewareTests()
    {
        _organizationLookup.GetLocationAsync(BatelLocationId)
            .Returns(new LocationSummaryDto { Id = BatelLocationId, OrganizationId = CantinaId });
        // Ana's grid holds Menus Read at Batel and nothing at Mercado Municipal.
        _memberAccess.GetAccessGridAsync(AnaId, CantinaId).Returns(new AccessGridDto
        {
            Cells = new[] { new AccessCellDto { LocationId = BatelLocationId, Module = AccessModule.Menus, Level = AccessLevel.Read } }
        });
        // Mercado Municipal belongs to Cantina, but Ana has no cell there.
        _organizationLookup.GetLocationAsync(MercadoMunicipalLocationId)
            .Returns(new LocationSummaryDto { Id = MercadoMunicipalLocationId, OrganizationId = CantinaId });
    }

    private static IStringLocalizer<SharedResource> EchoLocalizer()
    {
        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        return localizer;
    }

    private static ICurrentUser CurrentUser(Guid? userId, Guid? organizationId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.OrganizationId.Returns(organizationId);
        return currentUser;
    }

    private static HttpContext SignedInRequest(string? locationHeader)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, AnaId.ToString()) }, authenticationType: "Bearer"))
        };
        if (locationHeader is not null)
            context.Request.Headers[HeaderName] = locationHeader;
        return context;
    }

    private Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        var sut = new LocationAccessMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        });
        return sut.InvokeAsync(context, currentUser, _organizationLookup, _memberAccess, _selectedLocation, EchoLocalizer());
    }

    [Theory]
    [InlineData("batel")]
    [InlineData("12345")]
    [Trait("Rule", "SH10")]
    public async Task InvokeAsync_HeaderIsNotAValidId_ThrowsBadRequestAndStops(string headerValue)
    {
        // Arrange
        var context = SignedInRequest(headerValue);

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => InvokeAsync(context, CurrentUser(AnaId, CantinaId)));

        // Assert
        thrown.StatusCode.ShouldBe(400, "the request is refused with the right status");
        _nextCalled.ShouldBeFalse("a refused request never reaches the endpoint");
        _selectedLocation.LocationId.ShouldBeNull("a refused request selects no location");
    }

    [Fact]
    [Trait("Rule", "SH10")]
    public async Task InvokeAsync_NoOrganizationInToken_ThrowsForbiddenAndStops()
    {
        // Arrange
        var context = SignedInRequest(BatelLocationId.ToString());

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => InvokeAsync(context, CurrentUser(AnaId, organizationId: null)));

        // Assert
        thrown.StatusCode.ShouldBe(403, "the request is refused with the right status");
        _nextCalled.ShouldBeFalse("a refused request never reaches the endpoint");
        _selectedLocation.LocationId.ShouldBeNull("a refused request selects no location");
    }

    [Theory]
    [InlineData(UnknownLocation)]
    [InlineData(MercadoMunicipalLocation)]
    [Trait("Rule", "SH10")]
    public async Task InvokeAsync_LocationWhereUserHasNoLevelOrNoSuchLocation_ThrowsForbiddenAndStops(string location)
    {
        // Arrange
        var context = SignedInRequest(location);

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => InvokeAsync(context, CurrentUser(AnaId, CantinaId)));

        // Assert
        thrown.StatusCode.ShouldBe(403, "the request is refused with the right status");
        _nextCalled.ShouldBeFalse("a refused request never reaches the endpoint");
        _selectedLocation.LocationId.ShouldBeNull("a refused request selects no location");
    }

    [Fact]
    [Trait("Rule", "SH11")]
    public async Task InvokeAsync_NoHeader_ContinuesWithoutLocationLimit()
    {
        // Arrange
        var context = SignedInRequest(locationHeader: null);

        // Act
        await InvokeAsync(context, CurrentUser(AnaId, CantinaId));

        // Assert
        _nextCalled.ShouldBeTrue("a request without a location header goes on to the endpoint");
        _selectedLocation.LocationId.ShouldBeNull("no header means no location limit");
    }

    [Fact]
    [Trait("Rule", "SH11")]
    public async Task InvokeAsync_AnonymousRequestWithHeader_IgnoresHeaderAndContinues()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = BatelLocationId.ToString();

        // Act
        await InvokeAsync(context, CurrentUser(userId: null, organizationId: null));

        // Assert
        _nextCalled.ShouldBeTrue("an anonymous request goes on to the endpoint");
        _selectedLocation.LocationId.ShouldBeNull("the location header is ignored for anonymous requests");
        await _organizationLookup.DidNotReceiveWithAnyArgs().GetLocationAsync(default);
    }

    [Fact]
    [Trait("Rule", "SH11")]
    public async Task InvokeAsync_UserWithALevelAtTheLocation_StoresLocationAndContinues()
    {
        // Arrange
        var context = SignedInRequest(BatelLocationId.ToString());

        // Act
        await InvokeAsync(context, CurrentUser(AnaId, CantinaId));

        // Assert
        _nextCalled.ShouldBeTrue("a user with a level at the location goes on to the endpoint");
        _selectedLocation.LocationId.ShouldBe(BatelLocationId, "the location chosen in the header is the one the request works in");
    }

    [Fact]
    [Trait("Rule", "SH11")]
    public async Task InvokeAsync_OwnerWithNoStoredCells_StoresAnyLocationOfTheOrganizationAndContinues()
    {
        // Arrange
        _memberAccess.GetAccessGridAsync(AnaId, CantinaId).Returns(new AccessGridDto { IsOwner = true });
        var context = SignedInRequest(MercadoMunicipalLocation);

        // Act
        await InvokeAsync(context, CurrentUser(AnaId, CantinaId));

        // Assert
        _nextCalled.ShouldBeTrue("the Owner may work at any location of the organization");
        _selectedLocation.LocationId.ShouldBe(MercadoMunicipalLocationId, "the location chosen in the header is the one the request works in");
    }
}
