using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Services;

namespace RestaurantManagement.Web.Tests;

public class AuthApiFactory : WebApplicationFactory<Program>
{
    public IRegistrationService RegistrationService { get; } = Substitute.For<IRegistrationService>();
    public ISessionService SessionService { get; } = Substitute.For<ISessionService>();
    public IVerificationService VerificationService { get; } = Substitute.For<IVerificationService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRegistrationService>();
            services.RemoveAll<ISessionService>();
            services.RemoveAll<IVerificationService>();
            services.AddSingleton(RegistrationService);
            services.AddSingleton(SessionService);
            services.AddSingleton(VerificationService);
            services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>();
        });
    }
}
