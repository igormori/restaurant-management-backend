using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    public class RegisterRequest
    {
        private string _email = string.Empty;
        private string? _phoneNumber;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [EmailAddress(ErrorMessage = "EmailAddressAttribute_ValidationError")]
        [MaxLength(320, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        [Phone(ErrorMessage = "PhoneAttribute_ValidationError")]
        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string? PhoneNumber
        {
            get => _phoneNumber;
            set => _phoneNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MinLength(8, ErrorMessage = "MinLengthAttribute_ValidationError")]
        [MaxLength(128, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).+$",
            ErrorMessage = "PasswordComplexity_ValidationError")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string FirstName
        {
            get => _firstName;
            set => _firstName = value?.Trim() ?? string.Empty;
        }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string LastName
        {
            get => _lastName;
            set => _lastName = value?.Trim() ?? string.Empty;
        }
    }
}
