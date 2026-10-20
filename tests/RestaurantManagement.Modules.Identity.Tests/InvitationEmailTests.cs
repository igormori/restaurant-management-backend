using System.Text.RegularExpressions;
using NSubstitute;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.InvitationHarness;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The invitation email. Ana Souza (Owner) invites Bruno to Cantina da Nonna as a Waiter at Batel and a
    /// Cashier at Mercado Municipal on 2026-10-20 12:00 UTC, so the link expires on 2026-10-27.
    /// </summary>
    public class InvitationEmailTests
    {
        private const string BrunoEmail = "bruno@exemplo.com.br";

        private static Task SendInvitationAsync(InvitationHarness harness, string language, IInvitationService? sut = null)
            => (sut ?? harness.As(AnaId)).CreateAsync(
                Invite(BrunoEmail, (BatelId, new[] { "Waiter" }), (MercadoId, new[] { "Cashier" })), language);

        private static string WithoutLinks(string html) => Regex.Replace(html, "href=\"[^\"]*\"", "href=\"\"");

        [Fact]
        [Trait("Rule", "T64")]
        public async Task CreateAsync_EnglishRequest_EmailHasInviterOrganizationRolesPerLocationButtonAndExpiryAndNoLevel()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();

            // Act
            await SendInvitationAsync(harness, "en");

            // Assert
            var email = harness.SentEmails().ShouldHaveSingleItem("one email goes to the invited address");
            email.To.ShouldBe(BrunoEmail);
            email.Subject.ShouldBe($"You are invited to join Cantina da Nonna on {ProductName}", "the subject names the organization and the product");
            var text = WithoutLinks(email.Html);
            text.ShouldContain("Ana Souza", customMessage: "the email says who invited the person");
            text.ShouldContain("Cantina da Nonna");
            text.ShouldContain("Waiter at Batel", customMessage: "the roles are listed per location");
            text.ShouldContain("Cashier at Mercado Municipal");
            text.ShouldContain("2026", customMessage: "the expiry date is shown");
            text.ShouldContain("27", customMessage: "the link expires 7 days after the invitation");
            email.Html.ShouldContain($"{AcceptPageUrl}?token=", customMessage: "the button leads to the accept page with the token");
            text.ShouldNotContain("Manage", customMessage: "the email never shows the levels of the grid");
        }

        [Fact]
        [Trait("Rule", "T65")]
        public async Task CreateAsync_PortugueseRequest_EmailIsInPortugueseAndStillNamesOrganizationAndRoles()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();

            // Act
            await SendInvitationAsync(harness, "pt");

            // Assert
            var email = harness.SentEmails().ShouldHaveSingleItem();
            email.Subject.ShouldNotContain("invited", customMessage: "a Portuguese request gets the subject in Portuguese");
            email.Subject.ShouldContain("Cantina da Nonna");
            email.Subject.ShouldContain(ProductName);
            email.Html.ShouldContain($"{AcceptPageUrl}?token=", customMessage: "the Portuguese email has the same accept button");
            WithoutLinks(email.Html).ShouldContain("Cantina da Nonna");
        }

        [Fact]
        [Trait("Rule", "T66")]
        public async Task CreateAsync_InviterNameAndOrganizationNameWithHtml_AreShownAsText()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync("Cantina <b>& Filhos</b>", "Ana <script>alert(1)</script>");

            // Act
            await SendInvitationAsync(harness, "en");

            // Assert
            var html = harness.SentEmails().ShouldHaveSingleItem().Html;
            html.ShouldNotContain("<script>", customMessage: "a name with HTML never becomes markup in the email");
            html.ShouldNotContain("<b>& Filhos</b>");
            html.ShouldContain("&lt;script&gt;", customMessage: "the characters are encoded and shown as text");
        }

        [Fact]
        [Trait("Rule", "T67")]
        public async Task CreateAsync_EmailSendingFails_KeepsTheInvitationAnswersSuccessAndLogsNoAddressOrToken()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            var email = Substitute.For<IEmailService>();
            email.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable")));
            var sut = harness.As(AnaId, email: email);

            // Act
            var response = await sut.CreateAsync(Invite(BrunoEmail, (BatelId, new[] { "Waiter" })), "en");

            // Assert
            response.Id.ShouldNotBe(Guid.Empty, "the request still answers success");
            harness.InvitationCount().ShouldBe(1, "the invitation is kept so it can be sent again by inviting again");
            harness.Logger.Messages.ShouldNotBeEmpty("a failed send is logged");
            harness.Logger.Messages.ShouldAllBe(m => !m.Contains(BrunoEmail), "the invited address never reaches the logs");
            harness.Logger.Messages.ShouldContain(m => m.Contains(response.Id.ToString()), "the log names the invitation by its ID");
        }
    }
}
