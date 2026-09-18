using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailService _emailService;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IStringLocalizer<SharedResource> localizer,
            IEmailService emailService,
            ILogger<RegistrationService> logger)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
            _emailService = emailService;
            _logger = logger;
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
            var verificationCode = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

            var verification = new UserVerificationCode
            {
                UserId = user.Id,
                Code = verificationCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_securityOptions.VerificationCodeExpiryMinutes)
            };
            _db.UserVerificationCodes.Add(verification);

            try
            {
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateEmailViolation(ex))
            {
                throw new BusinessException(_localizer["EmailAlreadyRegistered"].Value, 400);
            }

            // Send verification email; a failure here must not fail registration, since the
            // account already exists unverified and the user can request a resend.
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationCode, _securityOptions.VerificationCodeExpiryMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
            }

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }

        // Guards against the race the AnyAsync pre-check cannot close: only the unique
        // violation on the email uniqueness index is a duplicate email, everything else
        // is a genuine failure that must propagate.
        private static bool IsDuplicateEmailViolation(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException postgresException
                && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && postgresException.ConstraintName == IdentityDbContext.UniqueEmailIndexName;
        }
    }
}
