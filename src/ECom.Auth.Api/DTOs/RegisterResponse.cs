namespace ECom.Auth.Api.DTOs;

public sealed record RegisterResponse(Guid Id, string Email, string Name, string Role);
