using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Contracts.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128)] string Password);
