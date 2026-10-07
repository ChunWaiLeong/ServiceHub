namespace ServiceHub.Api.Contracts.Auth;

public sealed record AuthUserResponse(Guid Id, string FirstName, string LastName, string Email, string Role);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, AuthUserResponse User);
public sealed record RegistrationResponse(string Message);
