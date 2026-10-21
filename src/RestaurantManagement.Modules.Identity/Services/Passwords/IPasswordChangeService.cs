using RestaurantManagement.Modules.Identity.Models.Common;
using RestaurantManagement.Modules.Identity.Models.Passwords;

namespace RestaurantManagement.Modules.Identity.Services.Passwords
{
    public interface IPasswordChangeService
    {
        /// <summary>
        /// Replaces the password of the signed-in user (ID from the token only) after checking the
        /// current one, and ends every session of that user (task T8).
        /// </summary>
        Task<MessageResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    }
}
