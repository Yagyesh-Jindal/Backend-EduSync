using EduSyncAPI.DTOs;
using EduSyncAPI.Models;

namespace EduSyncAPI.Interfaces
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(UserDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
    }
}
