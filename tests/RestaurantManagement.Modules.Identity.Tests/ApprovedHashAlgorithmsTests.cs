using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using Sentry;
using Shouldly;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Only approved algorithms protect sessions: SHA-256 for refresh token fingerprints, HS256
    /// (HMAC with SHA-256) for access token signatures.
    /// </summary>
    public class ApprovedHashAlgorithmsTests
    {
        private const string Password = "Password1!";
        private const string UserEmail = "maria@cantinadanonna.com.br";

        // Matrix T25
        [Fact]
        [Trait("Rule", "T25")]
        public async Task IssueAsync_NewSignIn_StoredFingerprintIsSha256OfTheIssuedToken()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
            using (var seed = database.CreateContext())
            {
                seed.Users.Add(new User { Id = userId, Email = UserEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
                await seed.SaveChangesAsync();
            }

            // Act
            IssuedRefreshToken issued;
            using (var context = database.CreateContext())
                issued = await new RefreshTokenService(context, TestJwtOptions.Create()).IssueAsync(userId, null, null, null);

            // Assert
            using var readContext = database.CreateContext();
            var stored = await readContext.Set<RefreshToken>().AsNoTracking().SingleAsync();
            var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token)));
            stored.TokenHash.ShouldBe(expected, "refresh token fingerprints are the SHA-256 hash of the token");
        }

        // Matrix T25
        [Fact]
        [Trait("Rule", "T25")]
        public async Task LoginAsync_ValidCredentials_AccessTokenIsSignedWithHs256()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using (var seed = database.CreateContext())
            {
                seed.Users.Add(new User { Email = UserEmail, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), FirstName = "Maria", LastName = "Souza", IsVerified = true });
                await seed.SaveChangesAsync();
            }
            using var context = database.CreateContext();
            var jwtOptions = TestJwtOptions.Create();
            var sut = new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), TestPasswordHasher.Create());

            // Act
            var result = await sut.LoginAsync(new LoginRequest { Email = UserEmail, Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            new JwtSecurityTokenHandler().ReadJwtToken(result.Auth.Token).Header.Alg
                .ShouldBe("HS256", "access tokens are signed with HS256 (HMAC with SHA-256)");
        }
    }
}
