using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Options;
using Sentry;
using Shouldly;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class SessionServiceOrganizationClaimTests
    {
        private const string Password = "Password1!";
        private const string UserEmail = "maria@cantinadanonna.com.br";
        private const string OwnerRole = "Owner";
        private const string StaffRole = "Staff";

        private static readonly Guid CantinaOrganizationId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaOrganizationId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid CentroLocationId = Guid.Parse("d4d4d4d4-0000-4000-8000-000000000004");

        private static SessionService CreateSut(IdentityDbContext context)
        {
            var jwtOptions = Options.Create(new JwtOptions
            {
                Key = "test-signing-key-at-least-32-characters-long",
                Issuer = "RestaurantManagementTests",
                Audience = "RestaurantManagementTests",
                ExpireMinutes = 60,
                RefreshTokenExpireDays = 30
            });

            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>());
        }

        private static async Task SeedUserAsync(IdentityTestDatabase database, params UserRole[] roles)
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
            foreach (var role in roles)
                user.Roles.Add(role);

            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        private static async Task<AuthResponse> LoginAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await CreateSut(context).LoginAsync(new LoginRequest { Email = UserEmail, Password = Password });
        }

        private static string? OrganizationClaimOf(string token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .SingleOrDefault(c => c.Type == AppClaimTypes.OrganizationId)?.Value;
        }

        // Scenario 29
        [Fact]
        public async Task LoginAsync_UserWithRolesInTwoOrganizations_TokenHasNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId },
                new UserRole { Role = StaffRole, OrganizationId = TrattoriaOrganizationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBeNull();
        }

        // Scenario 30
        [Fact]
        public async Task LoginAsync_UserWithNoRoles_TokenHasNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database);

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBeNull();
        }

        // Scenario 31
        [Fact]
        public async Task LoginAsync_UserWithOnlyRolesWithoutOrganization_TokenHasNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, new UserRole { Role = StaffRole, OrganizationId = null });

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBeNull();
        }

        // Scenario 32
        [Fact]
        public async Task LoginAsync_UserWithOneRoleInOneOrganization_TokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBe(CantinaOrganizationId.ToString());
        }

        // Scenario 33
        [Fact]
        public async Task LoginAsync_UserWithTwoRolesInSameOrganization_TokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId },
                new UserRole { Role = StaffRole, OrganizationId = CantinaOrganizationId, LocationId = CentroLocationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBe(CantinaOrganizationId.ToString());
        }

        // Scenario 34
        [Fact]
        public async Task RefreshTokenAsync_UserWithOneOrganization_NewTokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId });
            var loginResponse = await LoginAsync(database);
            using var refreshContext = database.CreateContext();
            var sut = CreateSut(refreshContext);

            // Act
            var refreshed = await sut.RefreshTokenAsync(new RefreshRequest { Email = UserEmail, RefreshToken = loginResponse.RefreshToken });

            // Assert
            OrganizationClaimOf(refreshed.Token).ShouldBe(CantinaOrganizationId.ToString());
        }
    }
}
