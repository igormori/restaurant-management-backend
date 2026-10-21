using RestaurantManagement.Modules.Identity.Models.Passwords;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models.Sessions
{
    public class LoginRequest
    {
        private string _email = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [EmailAddress(ErrorMessage = "EmailAddressAttribute_ValidationError")]
        [MaxLength(320, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MinLength(8, ErrorMessage = "MinLengthAttribute_ValidationError")]
        [MaxLength(64, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        [MaxPasswordBytes]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}