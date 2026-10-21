using RestaurantManagement.Modules.Identity.Services;
using Shouldly;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class InvitationOptionsValidatorTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("/aceitar-convite")]
        [Trait("Rule", "T68")]
        public void Validate_AcceptPageAddressMissingOrNotAbsolute_FailsNamingTheSetting(string address)
        {
            // Arrange
            var sut = new InvitationOptionsValidator();

            // Act
            var result = sut.Validate(null, new InvitationOptions { AcceptPageUrl = address });

            // Assert
            result.Failed.ShouldBeTrue("the app refuses to start without an absolute accept page address");
            result.FailureMessage.ShouldContain("Invitation:AcceptPageUrl", customMessage: "the failure names the setting so the operator knows what to fix");
        }
    }
}
