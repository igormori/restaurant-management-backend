using RestaurantManagement.Api.Models.Users;
using System.Threading.Tasks;

namespace RestaurantManagement.Api.Services.Users
{
    public interface IUserService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);

        Task<AuthResponse> LoginAsync(LoginRequest request);
    }
}