using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    public class VerifyEmailRequest
    {
        private string _email = string.Empty;
        private string _code = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [EmailAddress(ErrorMessage = "EmailAddressAttribute_ValidationError")]
        [MaxLength(320, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(6, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Code
        {
            get => _code;
            set => _code = value?.Trim() ?? string.Empty;
        }
    }
}