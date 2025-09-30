using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Users;
using RestaurantManagement.Api.Services.Users;
using System.Security.Cryptography;
using System.Text;


namespace RestaurantManagement.Api.Services.Users
{
    public class UserService : IUserService
    {
        private readonly RestaurantDbContext _context;

        public UserService(RestaurantDbContext context) {
            _context = context;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request) {

            // Validate email
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                throw new Exception("Email already registered");

            // Hash password
            string passwordHash = HashPassword(request.Password);

            // Construct the user
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
                UserId =  user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
