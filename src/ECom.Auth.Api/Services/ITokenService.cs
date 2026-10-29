namespace ECom.Auth.Api.Services;

using ECom.Auth.Api.DTOs;

public interface ITokenService
{
    TokenResult GenerateToken(Guid userId, string email, string name, string role);
}
