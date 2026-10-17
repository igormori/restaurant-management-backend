using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Web.Auth;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

public class ClaimsCurrentUserTests
{
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaOrganizationId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private static ClaimsCurrentUser CreateSut(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new ClaimsCurrentUser(accessor);
    }

    private static ClaimsCurrentUser CreateSutWithoutHttpContext()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        return new ClaimsCurrentUser(accessor);
    }

    // Scenario 35
    [Fact]
    [Trait("Rule", "W11")]
    public void UserId_NoHttpContext_IsNull()
    {
        // Arrange
        var sut = CreateSutWithoutHttpContext();

        // Act
        var userId = sut.UserId;

        // Assert
        userId.ShouldBeNull();
    }

    // Scenario 36
    [Fact]
    [Trait("Rule", "W11")]
    public void OrganizationId_NoHttpContext_IsNull()
    {
        // Arrange
        var sut = CreateSutWithoutHttpContext();

        // Act
        var organizationId = sut.OrganizationId;

        // Assert
        organizationId.ShouldBeNull();
    }

    // Scenario 37
    [Fact]
    [Trait("Rule", "W11")]
    public void UserId_NameIdentifierClaimIsNotAGuid_IsNull()
    {
        // Arrange
        var sut = CreateSut(new Claim(ClaimTypes.NameIdentifier, "maria-souza"));

        // Act
        var userId = sut.UserId;

        // Assert
        userId.ShouldBeNull();
    }

    // Scenario 38
    [Fact]
    [Trait("Rule", "W11")]
    public void OrganizationId_OrganizationClaimIsNotAGuid_IsNull()
    {
        // Arrange
        var sut = CreateSut(new Claim(AppClaimTypes.OrganizationId, "cantina-da-nonna"));

        // Act
        var organizationId = sut.OrganizationId;

        // Assert
        organizationId.ShouldBeNull();
    }

    // Scenario 39
    [Fact]
    [Trait("Rule", "W11")]
    public void OrganizationId_NoOrganizationClaim_IsNull()
    {
        // Arrange
        var sut = CreateSut(new Claim(ClaimTypes.NameIdentifier, MariaId.ToString()));

        // Act
        var organizationId = sut.OrganizationId;

        // Assert
        organizationId.ShouldBeNull();
    }

    // Scenario 40
    [Fact]
    [Trait("Rule", "W11")]
    public void UserId_ValidNameIdentifierClaim_ReturnsTheUserId()
    {
        // Arrange
        var sut = CreateSut(new Claim(ClaimTypes.NameIdentifier, MariaId.ToString()));

        // Act
        var userId = sut.UserId;

        // Assert
        userId.ShouldBe(MariaId);
    }

    // Scenario 41
    [Fact]
    [Trait("Rule", "W11")]
    public void OrganizationId_ValidOrganizationClaim_ReturnsTheOrganizationId()
    {
        // Arrange
        var sut = CreateSut(
            new Claim(ClaimTypes.NameIdentifier, MariaId.ToString()),
            new Claim(AppClaimTypes.OrganizationId, CantinaOrganizationId.ToString()));

        // Act
        var organizationId = sut.OrganizationId;

        // Assert
        organizationId.ShouldBe(CantinaOrganizationId);
    }
}
