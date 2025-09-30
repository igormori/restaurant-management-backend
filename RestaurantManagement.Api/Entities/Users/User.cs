namespace RestaurantManagement.Api.Entities.Users
{
    public class User
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = null!;   

        public string? PhoneNumber { get; set; }    

        public string PasswordHash { get; set; } = null!;

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public int FailedAttempts { get; set; } = 0;

        public string? RefreshTokenHash { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        public DateTime? LockedUntil { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}