using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The auth test app plus <see cref="PermissionProbeController"/>, so access rules can be checked
/// on endpoints that exist only in tests. The grid lookup and the organization lookup are
/// substituted, so a test states the grid it needs without a database; the real guard and policies run.
/// </summary>
public class PermissionProbeApiFactory : AuthApiFactory
{
    public IMemberAccessLookup MemberAccess { get; } = Substitute.For<IMemberAccessLookup>();

    public IOrganizationLookup Organizations { get; } = Substitute.For<IOrganizationLookup>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(PermissionProbeController).Assembly);
            services.RemoveAll<IMemberAccessLookup>();
            services.AddSingleton(MemberAccess);
            services.RemoveAll<IOrganizationLookup>();
            services.AddSingleton(Organizations);
        });
    }
}
