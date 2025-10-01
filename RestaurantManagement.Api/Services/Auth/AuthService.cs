using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Auth;
using RestaurantManagement.Api.Options;
using RestaurantManagement.Api.Utils.Exceptions;
using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace RestaurantManagement.Api.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly RestaurantDbContext _context;
        private readonly SecurityOptions _securityOptions;
        private readonly JwtOptions _jwtOptions;

        public AuthService(
            RestaurantDbContext context,
            IConfiguration config,
            IOptions<SecurityOptions> securityOptions,
            IOptions<JwtOptions> jwtOptions)
        {
            _context = context;
            _securityOptions = securityOptions.Value;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                throw new BusinessException("Email is already registered.", 400);

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new BusinessException("Invalid email or password.", 401);

            // 🔒 Check if locked
            if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
                throw new BusinessException($"Account locked until {user.LockedUntil.Value:u}", 423);

            // 🔑 Verify password
            if (!VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedAttempts++;

                if (user.FailedAttempts >= _securityOptions.MaxFailedLoginAttempts)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(_securityOptions.LockoutDurationMinutes);
                    user.FailedAttempts = 0;

                    SentrySdk.CaptureMessage($"Account locked: {user.Email}");
                }

                await _context.SaveChangesAsync();
                throw new BusinessException("Invalid email or password.", 401);
            }

            // ✅ Success → reset counters
            user.FailedAttempts = 0;
            user.LockedUntil = null;

            var jwt = GenerateJwt(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken); ;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            user.LastLoginAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || user.RefreshTokenHash == null || user.RefreshTokenExpiry == null)
                throw new BusinessException("Invalid or expired refresh token.", 401);

            if (user.RefreshTokenExpiry < DateTime.UtcNow)
                throw new BusinessException("Invalid or expired refresh token.", 401);

            if (!BCrypt.Net.BCrypt.Verify(request.RefreshToken, user.RefreshTokenHash))
                throw new BusinessException("Invalid refresh token.", 401);

            string newJwt = GenerateJwt(user);

            var newRefreshToken = GenerateRefreshToken();
            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(newRefreshToken);

            var expireMinutes = _jwtOptions.ExpireMinutes > 0
                ? _jwtOptions.ExpireMinutes
                : 60; // default fallback when configuration is missing or invalid

            user.RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(expireMinutes);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

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
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
            };

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
