namespace ECom.Auth.Api.DTOs;

public sealed record TokenResult(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc);
