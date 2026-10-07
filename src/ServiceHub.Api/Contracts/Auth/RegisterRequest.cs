using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Contracts.Auth;

public sealed record RegisterRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required] string Role);
