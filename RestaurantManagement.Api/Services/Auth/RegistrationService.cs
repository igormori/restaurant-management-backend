using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Auth;
using RestaurantManagement.Api.Options;
using RestaurantManagement.Api.Utils.Exceptions;

namespace RestaurantManagement.Api.Services.Auth
{
    public class RegistrationService : IRegistrationService
    {
        private readonly RestaurantDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public RegistrationService(
            RestaurantDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            // 1. Check if user already exits
            if (await _db.Users.AnyAsync(u => u.Email == request.Email))
                throw new BusinessException(_localizer["EmailAlreadyRegistered"].Value, 400);

            // 2. Hash password
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            using var tx = await _db.Database.BeginTransactionAsync();

            // 3. Add new user
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

            // 4. Add the verification code
            var verificationCode = new Random().Next(100000, 999999).ToString();

            var verification = new UserVerificationCode
            {
                UserId = user.Id,
                Code = verificationCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_securityOptions.VerificationCodeExpiryMinutes)
            };
            _db.UserVerificationCodes.Add(verification);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // TODO: Implement the email verificaiton
            // await _emailService.SendVerificationEmailAsync(user.Email, code);

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }
    }
}
