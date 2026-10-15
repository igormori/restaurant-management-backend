using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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

    private const string HighLimit = "100000";

    public const string TestVerificationCodeHashKey = "cantina-web-test-verification-hash-key-0123456789";

    private static readonly Dictionary<string, string?> HighLimitSettings = new()
    {
        ["VerificationCode:HashKey"] = TestVerificationCodeHashKey,
        ["AuthRateLimit:Login:PermitLimit"] = HighLimit,
        ["AuthRateLimit:Register:PermitLimit"] = HighLimit,
        ["AuthRateLimit:ResendVerification:PermitLimit"] = HighLimit,
        ["AuthRateLimit:Verify:PermitLimit"] = HighLimit,
        ["AuthRateLimit:Refresh:PermitLimit"] = HighLimit,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(HighLimitSettings));

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
