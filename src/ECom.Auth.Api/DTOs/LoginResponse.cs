namespace ECom.Auth.Api.DTOs;

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc, UserDto User);
