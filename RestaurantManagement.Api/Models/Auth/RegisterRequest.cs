using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.Models.Auth
{
    public class RegisterRequest
    {
        private string _email = string.Empty;
        private string? _phoneNumber;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;

        [Required, EmailAddress, MaxLength(320)]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        [Phone, MaxLength(20)]
        public string? PhoneNumber
        {
            get => _phoneNumber;
            set => _phoneNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [Required, MinLength(8), MaxLength(128), DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).+$", ErrorMessage = "Password must contain at least one uppercase letter, one number, and one special character.")]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FirstName
        {
            get => _firstName;
            set => _firstName = value?.Trim() ?? string.Empty;
        }

        [Required, MaxLength(100)]
        public string LastName
        {
            get => _lastName;
            set => _lastName = value?.Trim() ?? string.Empty;
        }
    }
}
