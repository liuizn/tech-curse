using TechCurse.Application.DTOs;

namespace TechCurse.Application.Interfaces;

public interface IAuthService
{
    Task<bool> RegisterAsync(RegisterInputDto input);
    Task CreateUserAsync(CreateUserInputDto input);
    Task<AuthOutputDto?> LoginAsync(LoginInputDto input);
    Task<AuthOutputDto?> RefreshAsync(RefreshTokenInputDto input);
}
