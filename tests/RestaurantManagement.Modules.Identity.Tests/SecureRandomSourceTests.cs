using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Codes and refresh tokens must never come from a predictable generator such as System.Random.
    /// </summary>
    public class SecureRandomSourceTests
    {
        private const int RefreshTokenBytes = 64;
        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

        [Fact]
        [Trait("Rule", "T19")]
        public async Task IssueAsync_TwoTokens_AreDifferentAnd64RandomBytesLong()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User { Id = MariaId, Email = "maria@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
            await seedContext.SaveChangesAsync();
            using var context = database.CreateContext();
            var sut = new RefreshTokenService(context, TestJwtOptions.Create());

            // Act
            var first = await sut.IssueAsync(MariaId, null, "Chrome", "203.0.113.10");
            var second = await sut.IssueAsync(MariaId, null, "Chrome", "203.0.113.10");

            // Assert
            Convert.FromBase64String(first.Token).Length.ShouldBe(RefreshTokenBytes, "a refresh token is 64 random bytes");
            second.Token.ShouldNotBe(first.Token, "two refresh tokens are never the same");
        }

        [Fact]
        [Trait("Rule", "T19")]
        public void IdentityServices_SourceCode_NeverUsesSystemRandom()
        {
            // Arrange
            var servicesFolder = Path.Combine(RepositoryRoot(), "src", "RestaurantManagement.Modules.Identity", "Services");

            // Act
            var filesUsingSystemRandom = Directory.GetFiles(servicesFolder, "*.cs")
                .Where(file => File.ReadAllText(file).Contains("new Random(") || File.ReadAllText(file).Contains("Random.Shared"))
                .Select(Path.GetFileName)
                .ToList();

            // Assert
            filesUsingSystemRandom.ShouldBeEmpty("codes and tokens come from RandomNumberGenerator, never from System.Random");
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RestaurantManagement.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("RestaurantManagement.sln not found above the test output folder.");
        }
    }
}
