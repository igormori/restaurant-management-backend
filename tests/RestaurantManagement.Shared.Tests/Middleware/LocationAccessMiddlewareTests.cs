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
    private readonly IUserRoleLookup _userRoleLookup = Substitute.For<IUserRoleLookup>();
    private readonly SelectedLocation _selectedLocation = new();
    private bool _nextCalled;

    public LocationAccessMiddlewareTests()
    {
        _organizationLookup.GetLocationAsync(BatelLocationId)
            .Returns(new LocationSummaryDto { Id = BatelLocationId, OrganizationId = CantinaId });
        _userRoleLookup.HasLocationAccessAsync(AnaId, CantinaId, BatelLocationId).Returns(true);
        // Mercado Municipal belongs to Cantina, but Ana has no role for it (the lookup answers false).
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
        return sut.InvokeAsync(context, currentUser, _organizationLookup, _userRoleLookup, _selectedLocation, EchoLocalizer());
    }

    // Scenario 32
    [Theory]
    [InlineData("batel")]
    [InlineData("12345")]
    public async Task InvokeAsync_HeaderIsNotAValidId_ThrowsBadRequestAndStops(string headerValue)
    {
        // Arrange
        var context = SignedInRequest(headerValue);

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => InvokeAsync(context, CurrentUser(AnaId, CantinaId)));

        // Assert
        thrown.StatusCode.ShouldBe(400);
        _nextCalled.ShouldBeFalse();
        _selectedLocation.LocationId.ShouldBeNull();
    }

    // Scenario 33
    [Fact]
    public async Task InvokeAsync_NoOrganizationInToken_ThrowsForbiddenAndStops()
    {
        // Arrange
        var context = SignedInRequest(BatelLocationId.ToString());

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => InvokeAsync(context, CurrentUser(AnaId, organizationId: null)));

        // Assert
        thrown.StatusCode.ShouldBe(403);
        _nextCalled.ShouldBeFalse();
        _selectedLocation.LocationId.ShouldBeNull();
    }

    // Scenario 34
    [Theory]
    [InlineData(UnknownLocation)]
    [InlineData(MercadoMunicipalLocation)]
    public async Task InvokeAsync_LocationUserMayNotWorkIn_ThrowsForbiddenAndStops(string location)
    {
        // Arrange
        var context = SignedInRequest(location);

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => InvokeAsync(context, CurrentUser(AnaId, CantinaId)));

        // Assert
        thrown.StatusCode.ShouldBe(403);
        _nextCalled.ShouldBeFalse();
        _selectedLocation.LocationId.ShouldBeNull();
    }

    // Scenario 35
    [Fact]
    public async Task InvokeAsync_NoHeader_ContinuesWithoutLocationLimit()
    {
        // Arrange
        var context = SignedInRequest(locationHeader: null);

        // Act
        await InvokeAsync(context, CurrentUser(AnaId, CantinaId));

        // Assert
        _nextCalled.ShouldBeTrue();
        _selectedLocation.LocationId.ShouldBeNull();
    }

    // Scenario 36
    [Fact]
    public async Task InvokeAsync_AnonymousRequestWithHeader_IgnoresHeaderAndContinues()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = BatelLocationId.ToString();

        // Act
        await InvokeAsync(context, CurrentUser(userId: null, organizationId: null));

        // Assert
        _nextCalled.ShouldBeTrue();
        _selectedLocation.LocationId.ShouldBeNull();
        await _organizationLookup.DidNotReceiveWithAnyArgs().GetLocationAsync(default);
    }

    // Scenario 37
    [Fact]
    public async Task InvokeAsync_UserAssignedToTheLocation_StoresLocationAndContinues()
    {
        // Arrange
        var context = SignedInRequest(BatelLocationId.ToString());

        // Act
        await InvokeAsync(context, CurrentUser(AnaId, CantinaId));

        // Assert
        _nextCalled.ShouldBeTrue();
        _selectedLocation.LocationId.ShouldBe(BatelLocationId);
    }
}
