using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Builds the real VerificationService from whatever constructor it has: resend no longer sends email,
    /// so the service stops taking an email service, and these tests must compile before and after that.
    /// </summary>
    public static class TestVerificationService
    {
        public static VerificationService Create(
            IdentityDbContext context,
            IOptions<SecurityOptions>? securityOptions = null,
            ILogger<VerificationService>? logger = null,
            IVerificationCodeHasher? hasher = null)
        {
            return ActivatorUtilities.CreateInstance<VerificationService>(
                new ServiceCollection().BuildServiceProvider(),
                context,
                securityOptions ?? TestSecurityOptions.Create(),
                TestLocalizer.Create(),
                Substitute.For<Shared.Services.Email.IEmailService>(),
                logger ?? Substitute.For<ILogger<VerificationService>>(),
                hasher ?? TestVerificationCodeHasher.Create());
        }
    }
}
