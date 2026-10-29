namespace ECom.Auth.Api.Services;

using ECom.Auth.Api.DTOs;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
