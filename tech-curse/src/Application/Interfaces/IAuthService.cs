using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Interfaces;

public interface IAuthService
{
    Task<bool> RegisterAsync(RegisterInputDto input);
    Task<AuthOutputDto?> LoginAsync(LoginInputDto input);
    Task<AuthOutputDto?> RefreshAsync(RefreshTokenInputDto input);
}
