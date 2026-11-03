namespace ECom.Auth.Api.Services;

using ECom.Auth.Api.DTOs;
using ECom.Auth.Api.Entities;
using ECom.Auth.Api.Repositories;
using ECom.Shared.Exceptions;
using Microsoft.AspNetCore.Identity;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokens,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokens = refreshTokens;
        _timeProvider = timeProvider;
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

        // A login starts a new token family.
        var (response, _) = await IssueTokensAsync(user, Guid.NewGuid(), cancellationToken);
        return response;
    }

    public async Task<LoginResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var stored = await _refreshTokens.GetByHashAsync(
            _tokenService.HashRefreshToken(request.RefreshToken.Trim()), cancellationToken);

        if (stored is null)
            throw new InvalidRefreshTokenException();

        // A revoked token being presented again means it was copied: kill the whole family.
        if (stored.IsRevoked)
        {
            await _refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            throw new InvalidRefreshTokenException();
        }

        if (stored.IsExpired(now))
            throw new InvalidRefreshTokenException();

        // Re-read the user so role/name changes take effect and deleted users cannot refresh.
        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            throw new InvalidRefreshTokenException();

        // Persist the replacement first, then consume the old token. If another request consumed it
        // in between, TryRevokeAsync returns false and the whole family (including the token just
        // created) is revoked.
        var (response, replacement) = await IssueTokensAsync(user, stored.FamilyId, cancellationToken);
        if (!await _refreshTokens.TryRevokeAsync(stored.Id, now, replacement.Id, cancellationToken))
        {
            await _refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            throw new InvalidRefreshTokenException();
        }

        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var stored = await _refreshTokens.GetByHashAsync(
            _tokenService.HashRefreshToken(request.RefreshToken.Trim()), cancellationToken);

        // Idempotent and silent for unknown tokens so callers cannot probe which tokens exist.
        if (stored is not null)
            await _refreshTokens.RevokeFamilyAsync(stored.FamilyId, _timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new NotFoundException($"User {userId} not found.");

        return new CurrentUserResponse(user.Id, user.Email!, user.Name, user.Role.ToString());
    }

    private async Task<(LoginResponse Response, RefreshToken Stored)> IssueTokensAsync(
        ApplicationUser user,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var access = _tokenService.GenerateToken(user.Id, user.Email!, user.Name, user.Role.ToString());
        var refresh = _tokenService.GenerateRefreshToken();

        var stored = new RefreshToken(
            user.Id, refresh.TokenHash, familyId, _timeProvider.GetUtcNow(), refresh.ExpiresAtUtc);
        await _refreshTokens.AddAsync(stored, cancellationToken);

        var userDto = new UserDto(user.Id, user.Email!, user.Name, user.Role.ToString());
        var response = new LoginResponse(
            access.AccessToken, access.TokenType, access.ExpiresAtUtc, userDto,
            refresh.Token, refresh.ExpiresAtUtc);
        return (response, stored);
    }
}
