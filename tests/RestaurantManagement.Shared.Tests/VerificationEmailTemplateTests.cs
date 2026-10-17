using FluentAssertions;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;

namespace RestaurantManagement.Shared.Tests
{
    public class VerificationEmailTemplateTests
    {
        private const string ValidEmail = "a@b.com";
        private const string ValidFirstName = "Giulia";
        private const string ValidCode = "123456";
        private const int ValidExpiryMinutes = 15;

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        [Trait("Rule", "SH24")]
        public void Build_FirstNameIsBlank_UsesGenericGreeting(string? firstName)
        {
            // Act
            var body = VerificationEmailTemplate.Build(ValidEmail, firstName!, ValidCode, ValidExpiryMinutes);

            // Assert
            body.Should().Contain("Hi there,", "a blank first name gets the generic greeting");
            body.Should().NotContain("Hi ,", "the greeting never shows an empty name");
        }

        [Fact]
        [Trait("Rule", "SH24")]
        public void Build_NonDefaultExpiryMinutes_StatesConfiguredMinutes()
        {
            // Act
            var body = VerificationEmailTemplate.Build(ValidEmail, ValidFirstName, ValidCode, 30);

            // Assert
            body.Should().Contain("30 minutes", "the email states the configured code lifetime");
            body.Should().NotContain("15 minutes", "the default lifetime is not shown when another one is configured");
        }

        [Fact]
        [Trait("Rule", "SH24")]
        public void Build_WithCode_BodyContainsTheCode()
        {
            // Act
            var body = VerificationEmailTemplate.Build(ValidEmail, ValidFirstName, "482913", ValidExpiryMinutes);

            // Assert
            body.Should().Contain("482913", "the email carries the code the user must type");
        }

        [Fact]
        [Trait("Rule", "SH24")]
        public void Build_WithValidInput_LeavesNoUnsubstitutedTokens()
        {
            // Act
            var body = VerificationEmailTemplate.Build(ValidEmail, ValidFirstName, ValidCode, ValidExpiryMinutes);

            // Assert
            body.Should().NotContain("{{", "the email never shows unfilled template placeholders");
        }

        [Fact]
        [Trait("Rule", "SH24")]
        public void Build_WithRecipient_FooterContainsRecipientAddress()
        {
            // Act
            var body = VerificationEmailTemplate.Build("diner@example.com", ValidFirstName, ValidCode, ValidExpiryMinutes);

            // Assert
            body.Should().Contain("This message was sent to diner@example.com", "the footer says which address the email was sent to");
        }

        [Fact]
        [Trait("Rule", "SH24")]
        public void Build_WithFirstName_GreetingContainsFirstName()
        {
            // Act
            var body = VerificationEmailTemplate.Build(ValidEmail, "Giulia", ValidCode, ValidExpiryMinutes);

            // Assert
            body.Should().Contain("Hi Giulia,", "the greeting uses the user first name");
        }

        [Fact]
        [Trait("Rule", "SH24")]
        public void Build_WithValidInput_ContainsAllDesignContentElements()
        {
            // Act
            var body = VerificationEmailTemplate.Build(ValidEmail, ValidFirstName, ValidCode, ValidExpiryMinutes);

            // Assert
            body.Should().Contain("RISTORANTE");
            body.Should().Contain("Confirm your email");
            body.Should().Contain("15 minutes");
            body.Should().Contain(ValidCode);
            body.Should().Contain("didn't create an account");
            body.Should().Contain("The Ristorante team");
            body.Should().Contain("Reply to this email");
            body.Should().Contain("This message was sent to");
        }

        [Fact]
        [Trait("Rule", "SH26")]
        public void Build_NameAndAddressWithHtmlCharacters_AreShownAsTextNeverAsMarkup()
        {
            // Arrange
            const string firstName = "<script>alert(1)</script>Ana";
            const string recipient = "ana+\"x\"@cantina.com.br";

            // Act
            var body = VerificationEmailTemplate.Build(recipient, firstName, ValidCode, ValidExpiryMinutes);

            // Assert
            body.ShouldContain("&lt;script&gt;", customMessage: "a name with HTML must be shown as text");
            body.ShouldNotContain("<script>", customMessage: "a name must never become markup in the email");
            body.ShouldContain("&quot;", customMessage: "an address with quotes must be shown as text");
            body.ShouldNotContain(recipient, customMessage: "the raw address with quotes must not appear in the email");
        }

        [Fact]
        [Trait("Rule", "SH24")]
        public void Subject_IsUnchanged()
        {
            // Assert
            VerificationEmailTemplate.Subject.Should().Be("Verify your email", "the email subject keeps its agreed wording");
        }
    }
}
