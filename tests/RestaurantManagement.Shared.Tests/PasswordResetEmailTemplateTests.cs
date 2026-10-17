using RestaurantManagement.Shared.Services.Email;
using Shouldly;

namespace RestaurantManagement.Shared.Tests
{
    /// <summary>
    /// The password reset email (plan T1): it carries the code, greets the user and says how long the
    /// code lasts, and treats the first name as text, never as HTML.
    /// </summary>
    public class PasswordResetEmailTemplateTests
    {
        private const string Recipient = "maria@cantinadanonna.com.br";

        // Scenario 2
        [Fact]
        [Trait("Rule", "SH25")]
        public void Build_FirstNameWithHtmlCharacters_IsHtmlEncoded()
        {
            // Arrange
            const string firstName = "<b>Maria</b>";

            // Act
            var body = PasswordResetEmailTemplate.Build(Recipient, firstName, "482913", 15);

            // Assert
            body.ShouldContain("&lt;b&gt;Maria&lt;/b&gt;", customMessage: "a first name with HTML is shown as text in the reset email");
            body.ShouldNotContain("<b>Maria</b>", customMessage: "a first name must never become markup in the reset email");
        }

        // Scenario 1
        [Fact]
        [Trait("Rule", "SH24")]
        public void Build_ValidInput_ContainsNameCodeExpiryAndIgnoreNotice()
        {
            // Arrange
            const string firstName = "Maria";

            // Act
            var body = PasswordResetEmailTemplate.Build(Recipient, firstName, "482913", 20);

            // Assert
            body.ShouldContain("Maria", customMessage: "the reset email greets the user by first name");
            body.ShouldContain("482913", customMessage: "the reset email carries the code the user must type");
            body.ShouldContain("20 minutes", customMessage: "the reset email says how long the code lasts");
            body.ShouldContain("password", Case.Insensitive);
            body.ShouldContain("ignore", Case.Insensitive, customMessage: "the reset email tells the user to ignore it if they did not ask for it");
            body.ShouldNotContain("{{");
        }
    }
}
