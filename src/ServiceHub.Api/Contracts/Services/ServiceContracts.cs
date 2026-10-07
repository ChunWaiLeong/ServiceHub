using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Contracts.Services;

public sealed record ServiceRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(2000)] string Description,
    [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Price,
    [Required, RegularExpression("^AUD$", ErrorMessage = "Only AUD is supported.")] string Currency,
    [Range(1, 480)] int DurationMinutes) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(Price, 2) != Price)
            yield return new ValidationResult("Price must have at most two decimal places.", [nameof(Price)]);
    }
}

public sealed record ServiceStatusRequest([Required] bool? IsActive);
public sealed record ServiceResponse(Guid Id, Guid BusinessId, string Name, string Description,
    decimal Price, string Currency, int DurationMinutes, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
