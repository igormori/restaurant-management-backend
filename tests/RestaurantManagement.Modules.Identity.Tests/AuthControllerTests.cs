using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Controllers;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class AuthControllerTests
    {
        private static AuthController CreateSut(IVerificationService verificationService)
        {
            return new AuthController(
                Substitute.For<IRegistrationService>(),
                Substitute.For<ISessionService>(),
                verificationService);
        }

        [Fact]
        public async Task VerifyEmail_ValidRequest_ReturnsOkWithMessageBody()
        {
            // Arrange
            var verificationService = Substitute.For<IVerificationService>();
            verificationService.VerifyEmailAsync(Arg.Any<VerifyEmailRequest>())
                .Returns(new MessageResponse { Message = "UserVerified" });
            var sut = CreateSut(verificationService);

            // Act
            var result = await sut.VerifyEmail(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var body = okResult.Value.Should().BeOfType<MessageResponse>().Subject;
            body.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ResendVerification_ValidRequest_ReturnsOkWithMessageBody()
        {
            // Arrange
            var verificationService = Substitute.For<IVerificationService>();
            verificationService.ResendVerificationCodeAsync(Arg.Any<ResendVerificationRequest>())
                .Returns(new MessageResponse { Message = "VerificationCodeResent" });
            var sut = CreateSut(verificationService);

            // Act
            var result = await sut.ResendVerification(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var body = okResult.Value.Should().BeOfType<MessageResponse>().Subject;
            body.Message.Should().NotBeNullOrEmpty();
        }
    }
}
