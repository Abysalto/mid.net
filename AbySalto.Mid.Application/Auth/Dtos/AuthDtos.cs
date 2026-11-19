namespace AbySalto.Mid.Application.Auth.Dtos;

public record RegisterRequest(string Email, string Username, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
