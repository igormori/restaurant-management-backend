using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    // The user comes only from the token, so this model never carries a user ID.
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(128, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [PasswordRules]
        public string NewPassword { get; set; } = string.Empty;
    }
}
