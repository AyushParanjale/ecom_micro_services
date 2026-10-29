namespace ECom.Auth.Api.DTOs;

public sealed record CurrentUserResponse(Guid Id, string Email, string Name, string Role);
