using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RestaurantManagement.Api;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Auth;
using RestaurantManagement.Api.Options;
using RestaurantManagement.Api.Services.Auth;
using RestaurantManagement.Api.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Tests.Controller.Auth
{
    public class AuthServiceTest
    {
        private AuthService GetService(out RestaurantDbContext context)
        {
            // 🧱 In-memory database
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            context = new RestaurantDbContext(options);

            // ⚙️ In-memory config
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Jwt:Key", "test_secret_key_12345678901234567890"},
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"},
                {"Jwt:ExpireMinutes", "60"},
                {"Security:MaxFailedLoginAttempts", "5"},
                {"Security:LockoutDurationMinutes", "15"}
            };
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            var securityOptions = Options.Create(new SecurityOptions
            {
                MaxFailedLoginAttempts = 5,
                LockoutDurationMinutes = 15
            });

            var jwtOptions = Options.Create(new JwtOptions
            {
                Key = "test_secret_key_12345678901234567890",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpireMinutes = 60
            });

            // 🌐 Localizer mock (returns key as value)
            var localizerMock = new Mock<IStringLocalizer<SharedResource>>();
            localizerMock.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));

            // 🧾 Logger mock
            var loggerMock = new Mock<ILogger<string>>();

            return new AuthService(
                context,
                config,
                securityOptions,
                jwtOptions,
                localizerMock.Object,
                loggerMock.Object
            );
        }

        // ------------------------
        // ✅ Registration
        // ------------------------

        [Fact]
        public async Task RegisterAsync_ShouldCreateUser()
        {
            var service = GetService(out var context);
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                PhoneNumber = "1234567890",
                Password = "Password123!",
                FirstName = "John",
                LastName = "Doe"
            };

            var result = await service.RegisterAsync(request);

            Assert.NotNull(result);
            Assert.Equal("test@example.com", result.Email);
            Assert.Single(context.Users);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenEmailExists()
        {
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

            await Assert.ThrowsAsync<BusinessException>(() => service.RegisterAsync(request));
        }

        // ------------------------
        // ✅ Login
        // ------------------------

        [Fact]
        public async Task LoginAsync_ShouldReturnToken_WhenCredentialsValid()
        {
            var service = GetService(out var context);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = passwordHash,
                FirstName = "John",
                LastName = "Doe",
                IsVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new LoginRequest
            {
                Email = "TEST@example.com",
                Password = "Password123!"
            };

            var result = await service.LoginAsync(request);

            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrow_WhenInvalidCredentials()
        {
            var service = GetService(out var context);
            var request = new LoginRequest
            {
                Email = "wrong@example.com",
                Password = "badpassword"
            };

            await Assert.ThrowsAsync<BusinessException>(() => service.LoginAsync(request));
        }

        [Fact]
        public async Task LoginAsync_ShouldLockAccount_WhenThresholdExceeded()
        {
            var service = GetService(out var context);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = passwordHash,
                FirstName = "John",
                LastName = "Doe",
                IsVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var invalidRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "WrongPass1!"
            };

            for (var i = 0; i < 5; i++)
            {
                await Assert.ThrowsAsync<BusinessException>(() => service.LoginAsync(invalidRequest));
            }

            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.LoginAsync(invalidRequest));
            Assert.Equal(423, ex.StatusCode);
        }

        // ------------------------
        // ✅ Refresh Token
        // ------------------------

        [Fact]
        public async Task RefreshTokenAsync_ShouldReturnNewToken_WhenValidRefreshToken()
        {
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

            var result = await service.RefreshTokenAsync(request);

            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenExpired()
        {
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
                RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(-1)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new RefreshRequest
            {
                Email = "test@example.com",
                RefreshToken = refreshToken
            };

            await Assert.ThrowsAsync<BusinessException>(() => service.RefreshTokenAsync(request));
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenInvalid()
        {
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

            await Assert.ThrowsAsync<BusinessException>(() => service.RefreshTokenAsync(request));
        }

        // ------------------------
        // ✅ Email Verification
        // ------------------------

        [Fact]
        public async Task VerifyEmailAsync_ShouldVerifyUser_WhenCodeValid()
        {
            var service = GetService(out var context);
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "fake",
                FirstName = "John",
                LastName = "Doe",
                IsVerified = false
            };
            context.Users.Add(user);

            var code = new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            };
            context.UserVerificationCodes.Add(code);
            await context.SaveChangesAsync();

            var request = new VerifyEmailRequest
            {
                Email = "test@example.com",
                Code = "123456"
            };

            var result = await service.VerifyEmailAsync(request);

            Assert.Equal("UserVerified", result);
            Assert.True(user.IsVerified);
            Assert.True(code.IsUsed);
        }

        // ------------------------
        // ✅ Resend Verification
        // ------------------------

        [Fact]
        public async Task ResendVerificationCodeAsync_ShouldGenerateNewCode_WhenAllowed()
        {
            var service = GetService(out var context);
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "fake",
                FirstName = "John",
                LastName = "Doe",
                IsVerified = false
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new ResendVerificationRequest
            {
                Email = "test@example.com"
            };

            var result = await service.ResendVerificationCodeAsync(request);

            Assert.Equal("VerificationCodeResent", result);
            Assert.Single(context.UserVerificationCodes);
        }
    }
}
