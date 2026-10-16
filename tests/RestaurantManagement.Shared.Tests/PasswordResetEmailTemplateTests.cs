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
        public void Build_FirstNameWithHtmlCharacters_IsHtmlEncoded()
        {
            // Arrange
            const string firstName = "<b>Maria</b>";

            // Act
            var body = PasswordResetEmailTemplate.Build(Recipient, firstName, "482913", 15);

            // Assert
            body.ShouldContain("&lt;b&gt;Maria&lt;/b&gt;");
            body.ShouldNotContain("<b>Maria</b>");
        }

        // Scenario 1
        [Fact]
        public void Build_ValidInput_ContainsNameCodeExpiryAndIgnoreNotice()
        {
            // Arrange
            const string firstName = "Maria";

            // Act
            var body = PasswordResetEmailTemplate.Build(Recipient, firstName, "482913", 20);

            // Assert
            body.ShouldContain("Maria");
            body.ShouldContain("482913");
            body.ShouldContain("20 minutes");
            body.ShouldContain("password", Case.Insensitive);
            body.ShouldContain("ignore", Case.Insensitive);
            body.ShouldNotContain("{{");
        }
    }
}
