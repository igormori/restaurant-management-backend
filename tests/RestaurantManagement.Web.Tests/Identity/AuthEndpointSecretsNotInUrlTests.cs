using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Passwords, codes and tokens must never travel in a URL, where logs and browser history keep them.
/// </summary>
public class AuthEndpointSecretsNotInUrlTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private List<ControllerActionDescriptor> AuthActions() =>
        _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(action => action.AttributeRouteInfo!.Template!.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase))
            .ToList();

    // Matrix T23
    [Fact]
    [Trait("Rule", "T23")]
    public void AuthEndpoints_AllActions_AcceptOnlyPostWithNoQueryOrRouteParameters()
    {
        // Arrange
        var actions = AuthActions();

        // Act
        var notPost = actions
            .Where(action => !action.EndpointMetadata.OfType<HttpMethodMetadata>().All(m => m.HttpMethods.SequenceEqual(["POST"])))
            .Select(action => action.DisplayName)
            .ToList();
        var parametersInUrl = actions
            .SelectMany(action => action.Parameters
                .Where(p => p.BindingInfo?.BindingSource == BindingSource.Query || p.BindingInfo?.BindingSource == BindingSource.Path)
                .Select(p => $"{action.AttributeRouteInfo!.Template}: {p.Name}"))
            .ToList();

        // Assert
        actions.ShouldNotBeEmpty("the auth endpoints must be found");
        notPost.ShouldBeEmpty("every Identity endpoint accepts POST only, so no secret can be sent in a link");
        parametersInUrl.ShouldBeEmpty("no Identity endpoint reads a value from the query string or the route; secrets go in the body or the cookie");
    }
}
