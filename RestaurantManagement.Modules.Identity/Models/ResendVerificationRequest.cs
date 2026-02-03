using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    public class ResendVerificationRequest
    {
        private string _email = string.Empty;
        public string _code { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [EmailAddress(ErrorMessage = "EmailAddressAttribute_ValidationError")]
        [MaxLength(320, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

    }
}