using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Invitations;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// <c>POST api/invitations</c> on a real PostgreSQL database. Ana (Owner of Cantina da Nonna) invites
/// bruno@exemplo.com.br; Trattoria do Sol is the other organization.
/// </summary>
public class InvitationEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public InvitationEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "T60")]
    public async Task CreateInvitation_OwnerInvitesAWaiterAtBatel_StoresItForSevenDaysWithNoMembershipAndAuditsIt()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", InviteBody("Bruno@Exemplo.com.br", BatelId, new[] { "Waiter" }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reply = (await response.Content.ReadFromJsonAsync<InvitationReply>())!;
        reply.Email.ShouldBe(BrunoEmail, "the email is stored in lower case");
        reply.ExpiresAt.ShouldBeInRange(DateTime.UtcNow.AddDays(7).AddMinutes(-5), DateTime.UtcNow.AddDays(7).AddMinutes(5), "the link expires 7 days after creation");
        var stored = (await InvitationsAsync(factory, CantinaId)).ShouldHaveSingleItem();
        stored.Email.ShouldBe(BrunoEmail);
        stored.InvitedByUserId.ShouldBe(AnaId);
        var entry = (await AuditEntriesAsync(factory, CantinaId, AuditActions.MemberInvited)).ShouldHaveSingleItem("an invitation writes one audit entry");
        entry.After.ShouldContain(BrunoEmail, customMessage: "the entry records who was invited");
        entry.After.ShouldContain("Waiter", customMessage: "the entry records the roles offered");
    }

    [Fact]
    [Trait("Rule", "T61")]
    public async Task CreateInvitation_Created_TokenIsNeverInTheResponseTheAuditEntryOrTheDatabaseInReadableForm()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", InviteBody(BrunoEmail, BatelId, new[] { "Waiter" }));

        // Assert
        var token = LatestTokenSentTo(factory, BrunoEmail);
        token.ShouldNotBeNullOrEmpty("the link in the email carries the one-time token");
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(token, customMessage: "the response never carries the token");
        var rows = await InvitationsAsync(factory, CantinaId);
        JsonSerializer.Serialize(rows).ShouldNotContain(token, customMessage: "only a fingerprint of the token is stored");
        var entry = (await AuditEntriesAsync(factory, CantinaId, AuditActions.MemberInvited)).Single();
        (entry.Before + entry.After).ShouldNotContain(token, customMessage: "the audit entry never carries the token");
    }

    [Fact]
    [Trait("Rule", "T62")]
    public async Task SaveInvitation_SecondOpenInvitationForOneEmailInOneOrganization_IsRefusedByTheDatabaseButAnotherOrganizationMayInvite()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await AddInvitationAsync(factory, CantinaId);
        await AddInvitationAsync(factory, TrattoriaId);

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(() => AddInvitationAsync(factory, CantinaId));

        // Assert
        (exception.InnerException as PostgresException)?.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation,
            "an organization has at most one open invitation per email, even for two requests at the same moment");
    }

    [Theory]
    [InlineData("location")]
    [InlineData("role")]
    [Trait("Rule", "T63")]
    public async Task CreateInvitation_LocationOrRoleOfAnotherOrganization_ReturnsNotFoundAndStoresNothing(string kind)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);
        var body = kind == "location"
            ? InviteBody(BrunoEmail, CentroId, new[] { "Waiter" })
            : InviteBody(BrunoEmail, BatelId, new[] { "Sommelier" });

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's location or role is answered as if it did not exist");
        (await InvitationsAsync(factory, CantinaId)).ShouldBeEmpty("nothing is stored");
        (await InvitationsAsync(factory, TrattoriaId)).ShouldBeEmpty("nothing is stored in the other organization either");
    }

    private static Task AddInvitationAsync(TenantApiFactory factory, Guid organizationId)
        => factory.InOrganizationAsync<IdentityDbContext, bool>(organizationId, async db =>
        {
            db.Set<OrganizationInvitation>().Add(new OrganizationInvitation
            {
                OrganizationId = organizationId,
                Email = BrunoEmail,
                InvitedByUserId = organizationId == CantinaId ? AnaId : TerezaId,
                Language = "en",
                TokenHash = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            await db.SaveChangesAsync();
            return true;
        });
}
