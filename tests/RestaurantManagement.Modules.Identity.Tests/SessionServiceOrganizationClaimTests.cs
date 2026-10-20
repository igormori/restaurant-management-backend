using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Auth;
using Sentry;
using Shouldly;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class SessionServiceOrganizationClaimTests
    {
        private const string Password = "Password1!";
        private const string UserEmail = "maria@cantinadanonna.com.br";

        private static readonly Guid CantinaOrganizationId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaOrganizationId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

        private static SessionService CreateSut(IdentityDbContext context)
        {
            var jwtOptions = TestJwtOptions.Create();
            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), TestPasswordHasher.Create());
        }

        private static async Task SeedUserAsync(IdentityTestDatabase database, params OrganizationMember[] members)
        {
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = UserEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true
            };
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
            foreach (var member in members)
            {
                member.UserId = user.Id;
                seedContext.Set<OrganizationMember>().Add(member);
            }

            await seedContext.SaveChangesAsync();
        }

        private static async Task<SessionResult> LoginAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await CreateSut(context).LoginAsync(new LoginRequest { Email = UserEmail, Password = Password }, TestClients.KitchenTablet, null);
        }

        private static string? OrganizationClaimOf(string? token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .SingleOrDefault(c => c.Type == AppClaimTypes.OrganizationId)?.Value;
        }

        private static List<string> RoleClaimsOf(string? token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }

        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserInTwoOrganizations_TokenHasNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                TestMembers.Owner(Guid.Empty, CantinaOrganizationId),
                TestMembers.Member(Guid.Empty, TrattoriaOrganizationId));

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Auth.Token).ShouldBeNull("a user in two organizations gets no organization in the token until they choose one");
        }

        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserWithNoMemberships_TokenHasNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database);

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Auth.Token).ShouldBeNull("a user who is a member of no organization gets no organization in the token");
        }

        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserInOneOrganization_TokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, TestMembers.Owner(Guid.Empty, CantinaOrganizationId));

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Auth.Token).ShouldBe(CantinaOrganizationId.ToString(), "a user with a single organization gets that organization in the token");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_OwnerOrMemberOfOneOrganization_TokenCarriesTheOrganizationAndNoRoleClaim(bool isOwner)
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, isOwner
                ? TestMembers.Owner(Guid.Empty, CantinaOrganizationId)
                : TestMembers.Member(Guid.Empty, CantinaOrganizationId));

            // Act
            var response = await LoginAsync(database);

            // Assert
            RoleClaimsOf(response.Auth.Token).ShouldBeEmpty("the token holds only the user and the organization: the Owner is a flag in the stored membership and what a member may do is read from the stored grid on every request");
            OrganizationClaimOf(response.Auth.Token).ShouldBe(CantinaOrganizationId.ToString(), "the user still belongs to the organization");
        }

        [Fact]
        [Trait("Rule", "F6")]
        public async Task RefreshTokenAsync_UserInOneOrganization_NewTokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, TestMembers.Owner(Guid.Empty, CantinaOrganizationId));
            var loginResponse = await LoginAsync(database);
            using var refreshContext = database.CreateContext();
            var sut = CreateSut(refreshContext);

            // Act
            var refreshed = await sut.RefreshTokenAsync(loginResponse.RefreshToken);

            // Assert
            OrganizationClaimOf(refreshed.Auth.Token).ShouldBe(CantinaOrganizationId.ToString(), "a refreshed token keeps the user's single organization");
        }

        [Fact]
        [Trait("Rule", "F10")]
        [Trait("Rule", "F6")]
        public async Task RefreshTokenAsync_TwoOrganizationsNoneChosen_NewTokenHasNoRolesAndNoOrganization()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                TestMembers.Owner(Guid.Empty, CantinaOrganizationId),
                TestMembers.Member(Guid.Empty, TrattoriaOrganizationId));
            var loginResponse = await LoginAsync(database);
            using var refreshContext = database.CreateContext();

            // Act
            var refreshed = await CreateSut(refreshContext).RefreshTokenAsync(loginResponse.RefreshToken);

            // Assert
            RoleClaimsOf(refreshed.Auth.Token).ShouldBeEmpty("refreshing without a chosen organization gives a two-organization user no roles");
            OrganizationClaimOf(refreshed.Auth.Token).ShouldBeNull("refreshing without a chosen organization gives a two-organization user no organization");
        }
    }
}
