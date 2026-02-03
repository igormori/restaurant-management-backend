using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    public class LoginRequest
    {
        private string _email = string.Empty;

        [Required, EmailAddress, MaxLength(320)]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        [Required, MinLength(8), MaxLength(128), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}