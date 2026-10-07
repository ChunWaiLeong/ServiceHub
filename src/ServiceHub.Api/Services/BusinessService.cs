using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceHub.Api.Configuration;
using ServiceHub.Api.Contracts.Businesses;
using ServiceHub.Api.Data;
using ServiceHub.Api.ErrorHandling;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Services;

public sealed class BusinessService(ApplicationDbContext context, TimeProvider clock)
{
    public async Task<BusinessResponse> CreateAsync(Guid ownerId, BusinessRequest request, CancellationToken ct)
    {
        await EnsureActiveOwnerAsync(ownerId, ct);
        if (await context.Businesses.AnyAsync(b => b.OwnerId == ownerId, ct))
            throw new RequestException(409, "You already have a business profile.");
        await ValidateReferencesAsync(request, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var business = new Business
        {
            OwnerId = ownerId, Name = request.Name.Trim(), Description = request.Description.Trim(),
            BusinessCategoryId = request.BusinessCategoryId, Address = request.Address.Trim(),
            ContactPhone = NormalizePhone(request.ContactPhone), ContactEmail = request.ContactEmail.Trim(),
            TimeZoneId = request.TimeZoneId, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        context.Businesses.Add(business);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Businesses_OwnerId" })
        {
            // The database remains authoritative if two creation requests race.
            throw new RequestException(409, "You already have a business profile.");
        }
        return await GetOwnerAsync(ownerId, ct);
    }

    public async Task<BusinessResponse> UpdateAsync(Guid ownerId, Guid id, BusinessRequest request, CancellationToken ct)
    {
        var business = await GetOwnedEntityAsync(ownerId, id, ct);
        await ValidateReferencesAsync(request, ct);
        business.Name = request.Name.Trim(); business.Description = request.Description.Trim();
        business.BusinessCategoryId = request.BusinessCategoryId; business.Address = request.Address.Trim();
        business.ContactPhone = NormalizePhone(request.ContactPhone); business.ContactEmail = request.ContactEmail.Trim();
        business.TimeZoneId = request.TimeZoneId; business.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(ct);
        return await GetOwnerAsync(ownerId, ct);
    }

    public async Task<BusinessResponse> GetOwnerAsync(Guid ownerId, CancellationToken ct)
    {
        await EnsureActiveOwnerAsync(ownerId, ct);
        var business = await context.Businesses.AsNoTracking().Include(b => b.BusinessCategory)
            .SingleOrDefaultAsync(b => b.OwnerId == ownerId, ct);
        return business is null ? throw new RequestException(404, "Business profile not found.") : Map(business);
    }

    public async Task<BusinessResponse> GetPublicAsync(Guid id, CancellationToken ct)
    {
        var business = await context.Businesses.AsNoTracking().Include(b => b.BusinessCategory)
            .SingleOrDefaultAsync(b => b.Id == id && b.IsActive && b.Owner.IsActive, ct);
        return business is null ? throw new RequestException(404, "Business not found.") : Map(business);
    }

    public async Task<BusinessPageResponse> BrowseAsync(BusinessListQuery query, CancellationToken ct)
    {
        var businesses = context.Businesses.AsNoTracking().Where(b => b.IsActive && b.Owner.IsActive);
        if (query.CategoryId.HasValue) businesses = businesses.Where(b => b.BusinessCategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = query.Search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            var pattern = "%" + text + "%";
            businesses = businesses.Where(b => EF.Functions.ILike(b.Name, pattern, @"\")
                || EF.Functions.ILike(b.Description, pattern, @"\") || EF.Functions.ILike(b.Address, pattern, @"\"));
        }
        var total = await businesses.CountAsync(ct);
        var items = await businesses.OrderBy(b => b.Name).ThenBy(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(b => new BusinessSummaryResponse(b.Id, b.Name,
                b.Description.Length > 180 ? b.Description.Substring(0, 180) + "…" : b.Description,
                new CategoryResponse(b.BusinessCategoryId, b.BusinessCategory.Name), b.Address,
                b.Services.Count(s => s.IsActive))).ToListAsync(ct);
        return new BusinessPageResponse(items, total, query.Page, query.PageSize);
    }

    public async Task<Business> GetOwnedEntityAsync(Guid ownerId, Guid businessId, CancellationToken ct)
    {
        await EnsureActiveOwnerAsync(ownerId, ct);
        return await context.Businesses.SingleOrDefaultAsync(b => b.Id == businessId && b.OwnerId == ownerId, ct)
            ?? throw new RequestException(404, "Business profile not found.");
    }

    private async Task EnsureActiveOwnerAsync(Guid ownerId, CancellationToken ct)
    {
        if (!await context.Users.AnyAsync(u => u.Id == ownerId && u.IsActive, ct))
            throw new RequestException(401, "Your account is unavailable. Please sign in again.");
    }

    private async Task ValidateReferencesAsync(BusinessRequest request, CancellationToken ct)
    {
        if (!await context.BusinessCategories.AnyAsync(c => c.Id == request.BusinessCategoryId, ct))
            throw new RequestException(400, "Choose a valid business category.");
        if (!BusinessTimeZones.Supported.Any(zone => zone.Id == request.TimeZoneId))
            throw new RequestException(400, "Choose a supported Australian time zone.");
    }

    private static string? NormalizePhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static BusinessResponse Map(Business b) => new(b.Id, b.Name, b.Description,
        new CategoryResponse(b.BusinessCategoryId, b.BusinessCategory.Name), b.Address, b.ContactPhone,
        b.ContactEmail, b.TimeZoneId, b.IsActive, b.CreatedAtUtc, b.UpdatedAtUtc);
}
