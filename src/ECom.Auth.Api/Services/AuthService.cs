namespace ECom.Auth.Api.Services;

using ECom.Auth.Api.DTOs;
using ECom.Auth.Api.Entities;
using ECom.Shared.Exceptions;
using Microsoft.AspNetCore.Identity;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var name = request.Name.Trim();

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            throw new ConflictException($"An account with this email already exists.");

        var user = new ApplicationUser(email, name, UserRole.Customer);
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            throw new DomainException(errors);
        }

        return new RegisterResponse(user.Id, user.Email!, user.Name, user.Role.ToString());
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            throw new InvalidCredentialsException();

        var token = _tokenService.GenerateToken(user.Id, user.Email!, user.Name, user.Role.ToString());
        var userDto = new UserDto(user.Id, user.Email!, user.Name, user.Role.ToString());
        return new LoginResponse(token.AccessToken, token.TokenType, token.ExpiresAtUtc, userDto);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new NotFoundException($"User {userId} not found.");

        return new CurrentUserResponse(user.Id, user.Email!, user.Name, user.Role.ToString());
    }
}
