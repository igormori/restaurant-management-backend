using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    public class RefreshRequest
    {
        private string _email = string.Empty;
        private string _refreshToken = string.Empty;

        [Required, EmailAddress, MaxLength(320)]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        [Required, MaxLength(256)]
        public string RefreshToken
        {
            get => _refreshToken;
            set => _refreshToken = value?.Trim() ?? string.Empty;
        }
    }
}