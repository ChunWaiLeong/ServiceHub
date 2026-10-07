using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Contracts.Businesses;

public sealed record BusinessRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(2000)] string Description,
    Guid BusinessCategoryId,
    [Required, StringLength(500)] string Address,
    [StringLength(30)] string? ContactPhone,
    [Required, EmailAddress, StringLength(254)] string ContactEmail,
    [Required, StringLength(100)] string TimeZoneId);

public sealed class BusinessListQuery
{
    [StringLength(100)] public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    [Range(1, 100000)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 12;
}

public sealed record CategoryResponse(Guid Id, string Name);
public sealed record TimeZoneResponse(string Id, string Label);
public sealed record BusinessResponse(Guid Id, string Name, string Description, CategoryResponse Category,
    string Address, string? ContactPhone, string ContactEmail, string TimeZoneId, bool IsActive,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record BusinessSummaryResponse(Guid Id, string Name, string Description,
    CategoryResponse Category, string Address, int ActiveServiceCount);
public sealed record BusinessPageResponse(IReadOnlyList<BusinessSummaryResponse> Items, int TotalCount, int Page, int PageSize);
