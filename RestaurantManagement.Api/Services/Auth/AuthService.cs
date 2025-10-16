using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Globalization;
using System.Text;
using RestaurantManagement.Api.Utils.Exceptions;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Auth;
using RestaurantManagement.Api.Options;
using RestaurantManagement.Api.Data;

namespace RestaurantManagement.Api.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly RestaurantDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly JwtOptions _jwtOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;

        private readonly ILogger<string> _logger;

        public AuthService(
            RestaurantDbContext db,
            IConfiguration config,
            IOptions<SecurityOptions> securityOptions,
            IOptions<JwtOptions> jwtOptions,
            IStringLocalizer<SharedResource> localizer,
            ILogger<string> logger)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _jwtOptions = jwtOptions.Value;
            _localizer = localizer;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            {
                throw new BusinessException(_localizer["EmailAlreadyRegistered"].Value, 400);
            }


            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = true,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new BusinessException(_localizer["UserNotFound"].Value, 400);

            if (user.IsVerified == false)
                throw new BusinessException(_localizer["UserNotVerified"].Value, 401);

            // 🔒 Check if locked
            if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            {
                var lockUntilText = user.LockedUntil.Value
                    .ToLocalTime()
                    .ToString("f", CultureInfo.CurrentUICulture);

                throw new BusinessException(
                    string.Format(_localizer["AccountLockedUntil"].Value, lockUntilText),423
                );

            }

            // 🔑 Verify password
            if (!VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedAttempts++;

                if (user.FailedAttempts >= _securityOptions.MaxFailedLoginAttempts)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(_securityOptions.LockoutDurationMinutes);
                    user.FailedAttempts = 0;

                    SentrySdk.CaptureMessage($"{_localizer["AccountLocked"].Value}: {user.Email}");
                }

                await _db.SaveChangesAsync();
                throw new BusinessException(_localizer["InvalidPassword"].Value, 401);
            }

            // ✅ Success → reset counters
            user.FailedAttempts = 0;
            user.LockedUntil = null;

            var jwt = GenerateJwt(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken); ;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            user.LastLoginAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Token = jwt,
                RefreshToken = refreshToken
            };
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || user.RefreshTokenHash == null || user.RefreshTokenExpiry == null)
                throw new BusinessException(_localizer["InvlideOrExpiredToken"].Value, 401);

            if (user.RefreshTokenExpiry < DateTime.UtcNow)
                throw new BusinessException(_localizer["InvlideOrExpiredToken"].Value, 401);

            if (!BCrypt.Net.BCrypt.Verify(request.RefreshToken, user.RefreshTokenHash))
                throw new BusinessException(_localizer["InvalidRefreshToken"].Value, 401);

            string newJwt = GenerateJwt(user);

            var newRefreshToken = GenerateRefreshToken();
            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(newRefreshToken);

            var expireMinutes = _jwtOptions.ExpireMinutes > 0
                ? _jwtOptions.ExpireMinutes
                : 60; // default fallback when configuration is missing or invalid

            user.RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(expireMinutes);
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Token = newJwt,
                RefreshToken = newRefreshToken
            };
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string GenerateJwt(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
            };

            foreach (var role in user.Roles.Select(r => r.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtKey = _jwtOptions.Key;
            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT key is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expireMinutes = _jwtOptions.ExpireMinutes > 0
                ? _jwtOptions.ExpireMinutes
                : 60;

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private bool VerifyPassword(string plainPassword, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(hashedPassword))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(plainPassword, hashedPassword);
            }
            catch
            {
                return false;
            }
        }
    }
}
