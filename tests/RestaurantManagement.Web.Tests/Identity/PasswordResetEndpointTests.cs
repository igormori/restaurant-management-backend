using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// POST api/auth/forgot-password and POST api/auth/reset-password end to end on a real PostgreSQL
/// database (plan T9): the real reset service, refresh tokens and audit log; only the email is
/// substituted so the test can read the code. Maria is Owner of Cantina da Nonna and signed in on
/// two devices.
/// </summary>
public class PasswordResetEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string ForgotUrl = "/api/auth/forgot-password";
    private const string ResetUrl = "/api/auth/reset-password";
    private const string AuditLogUrl = "/api/audit-log";
    private const string MariaEmail = "maria@cantinadanonna.com.br";
    private const string OldPassword = "Massa@Fresca2026";
    private const string NewPassword = "Nhoque@Batata2027";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private readonly PostgresDatabaseFixture _postgres;
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private string? _lastEmailedCode;

    public PasswordResetEndpointTests(PostgresDatabaseFixture postgres)
    {
        _postgres = postgres;
        _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(call =>
            {
                _lastEmailedCode = call.ArgAt<string>(2);
                return Task.CompletedTask;
            });
    }

    private sealed record EntryRow(Guid Id, string Action, string EntityType, Guid EntityId, string? Before, string? After, string? Reason);

    private sealed record AuditPage(List<EntryRow> Items);

    /// <summary>The real app on its own database, with Maria and Cantina seeded and the email substituted.</summary>
    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(TenantApiFactory tenantFactory)
    {
        await tenantFactory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        var factory = tenantFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton(_emailService);
        }));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var maria = new User
        {
            Id = MariaId,
            Email = MariaEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(OldPassword),
            FirstName = "Maria",
            LastName = "Souza",
            IsVerified = true
        };
        maria.Roles.Add(new UserRole { OrganizationId = CantinaId, Role = Roles.Owner });
        db.Users.Add(maria);
        await db.SaveChangesAsync();
        return factory;
    }

    private static async Task<List<string>> IssueTwoRefreshTokensAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var tablet = await tokens.IssueAsync(MariaId, CantinaId, "Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1", "203.0.113.10");
        var laptop = await tokens.IssueAsync(MariaId, null, "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4) Chrome/124.0", "198.51.100.24");
        return new List<string> { tablet.Token, laptop.Token };
    }

    /// <summary>Whether the refresh token is still accepted (the refresh endpoint's own check).</summary>
    private static async Task<bool> IsRefreshAcceptedAsync(WebApplicationFactory<Program> factory, string rawToken)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenService>().RotateAsync(rawToken) is not null;
    }

    // Scenario 36
    [Fact]
    [Trait("Rule", "P1")]
    public async Task ForgotPassword_KnownAndUnknownEmail_IdenticalStatusAndBody()
    {
        // Arrange
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = await CreateFactoryAsync(tenantFactory);
        var client = factory.CreateClient();
        var known = await client.PostAsJsonAsync(ForgotUrl, new { email = MariaEmail });

        // Act
        var unknown = await client.PostAsJsonAsync(ForgotUrl, new { email = "ninguem@cantinadanonna.com.br" });

        // Assert
        known.StatusCode.ShouldBe(HttpStatusCode.OK);
        unknown.StatusCode.ShouldBe(known.StatusCode);
        (await unknown.Content.ReadAsStringAsync()).ShouldBe(await known.Content.ReadAsStringAsync());
        await _emailService.ReceivedWithAnyArgs(1).SendPasswordResetEmailAsync(default!, default!, default!, default);
    }

    // Scenario 37
    [Fact]
    [Trait("Rule", "P6")]
    public async Task ResetPassword_ValidCode_ReturnsOkWithoutSecretsAndEarlierRefreshTokensRefused()
    {
        // Arrange
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = await CreateFactoryAsync(tenantFactory);
        var earlierTokens = await IssueTwoRefreshTokensAsync(factory);
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(ForgotUrl, new { email = MariaEmail });
        var code = _lastEmailedCode!;

        // Act
        var response = await client.PostAsJsonAsync(ResetUrl, new { email = MariaEmail, code, newPassword = NewPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(code);
        body.ShouldNotContain(NewPassword);
        (await IsRefreshAcceptedAsync(factory, earlierTokens[0])).ShouldBeFalse();
        (await IsRefreshAcceptedAsync(factory, earlierTokens[1])).ShouldBeFalse();
    }

    // Scenario 38
    [Fact]
    [Trait("Rule", "P6")]
    public async Task ResetPassword_ValidCode_AuditEntryInUsersOrganizationWithoutPasswordData()
    {
        // Arrange
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = await CreateFactoryAsync(tenantFactory);
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(ForgotUrl, new { email = MariaEmail });
        var reset = await client.PostAsJsonAsync(ResetUrl, new { email = MariaEmail, code = _lastEmailedCode, newPassword = NewPassword });
        var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(factory.Services, MariaId, CantinaId, Roles.Owner));

        // Act
        var auditResponse = await ownerClient.GetAsync(AuditLogUrl);

        // Assert
        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
        auditResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auditBody = await auditResponse.Content.ReadAsStringAsync();
        auditBody.ShouldNotContain(NewPassword);
        auditBody.ShouldNotContain(OldPassword);
        var entry = (await auditResponse.Content.ReadFromJsonAsync<AuditPage>())!.Items.ShouldHaveSingleItem();
        entry.Action.ShouldBe(AuditActions.PasswordReset);
        entry.EntityId.ShouldBe(MariaId);
        entry.Before.ShouldBeNull();
        entry.After.ShouldBeNull();
        entry.Reason.ShouldBeNull();
    }
}
