using Microsoft.Extensions.DependencyInjection;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The auth test app plus <see cref="PermissionProbeController"/>, so access rules can be checked
/// on endpoints that exist only in tests.
/// </summary>
public class PermissionProbeApiFactory : AuthApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(PermissionProbeController).Assembly));
    }
}
