using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Auth;
using RestaurantManagement.Api.Services.Auth;
using RestaurantManagement.Api.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Tests.Controller.Auth
{
    public class AuthServiceTest
    {
        private AuthService GetService(out RestaurantDbContext context)
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            context = new RestaurantDbContext(options);

            // Fake config
            var inMemorySettings = new Dictionary<string, string> {
                {"Jwt:Key", "test_secret_key_12345678901234567890"},
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"}
            };
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            return new AuthService(context, config);
        }

        [Fact]
        public async Task RegisterAsync_ShouldCreateUser()
        {
            // Arrange
            var service = GetService(out var context);
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Password = "Password123!",
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = await service.RegisterAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test@example.com", result.Email);
            Assert.Single(context.Users); // user saved in DB
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenEmailExists()
        {
            // Arrange
            var service = GetService(out var context);
            context.Users.Add(new User
            {
                Email = "test@example.com",
                PasswordHash = "fake",
                FirstName = "Existing",
                LastName = "User"
            });
            await context.SaveChangesAsync();

            var request = new RegisterRequest
            {
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Password = "Password123!",
                FirstName = "John",
                LastName = "Doe"
            };

            // Act & Assert
            await Assert.ThrowsAsync<BusinessException>(() => service.RegisterAsync(request));
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnToken_WhenCredentialsValid()
        {
            // Arrange
            var service = GetService(out var context);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = passwordHash,
                FirstName = "John",
                LastName = "Doe"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrow_WhenInvalidCredentials()
        {
            // Arrange
            var service = GetService(out var context);
            var request = new LoginRequest
            {
                Email = "wrong@example.com",
                Password = "badpassword"
            };

            // Act & Assert
            await Assert.ThrowsAsync<BusinessException>(() => service.LoginAsync(request));
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldReturnNewToken_WhenValidRefreshToken()
        {
            // Arrange
            var service = GetService(out var context);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
            var refreshToken = "refresh_token_value";
            var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = passwordHash,
                FirstName = "John",
                LastName = "Doe",
                RefreshTokenHash = refreshTokenHash,
                RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(10)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new RefreshRequest
            {
                Email = "test@example.com",
                RefreshToken = refreshToken
            };

            // Act
            var result = await service.RefreshTokenAsync(request);

            // Assert
            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenExpired()
        {
            // Arrange
            var service = GetService(out var context);
            var refreshToken = "refresh_token_value";
            var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "fake",
                FirstName = "John",
                LastName = "Doe",
                RefreshTokenHash = refreshTokenHash,
                RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(-1) // expired
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new RefreshRequest
            {
                Email = "test@example.com",
                RefreshToken = refreshToken
            };

            // Act & Assert
            await Assert.ThrowsAsync<BusinessException>(() => service.RefreshTokenAsync(request));
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenInvalid()
        {
            // Arrange
            var service = GetService(out var context);
            var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword("correct_token");
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "fake",
                FirstName = "John",
                LastName = "Doe",
                RefreshTokenHash = refreshTokenHash,
                RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(10)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new RefreshRequest
            {
                Email = "test@example.com",
                RefreshToken = "wrong_token"
            };

            // Act & Assert
            await Assert.ThrowsAsync<BusinessException>(() => service.RefreshTokenAsync(request));
        }
    }
}