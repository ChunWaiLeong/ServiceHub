using Microsoft.EntityFrameworkCore;
using ServiceHub.Api.Contracts.Services;
using ServiceHub.Api.Data;
using ServiceHub.Api.ErrorHandling;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Services;

public sealed class ServiceManagementService(ApplicationDbContext context, BusinessService businesses, TimeProvider clock)
{
    public async Task<ServiceResponse> CreateAsync(Guid ownerId, Guid businessId, ServiceRequest request, CancellationToken ct)
    {
        await businesses.GetOwnedEntityAsync(ownerId, businessId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var service = new Service { BusinessId = businessId, Name = request.Name.Trim(),
            Description = request.Description.Trim(), Price = request.Price, Currency = request.Currency,
            DurationMinutes = request.DurationMinutes, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        context.Services.Add(service);
        await context.SaveChangesAsync(ct);
        // Return the persisted timestamp precision, matching later PostgreSQL reads.
        await context.Entry(service).ReloadAsync(ct);
        return Map(service);
    }

    public async Task<ServiceResponse> UpdateAsync(Guid ownerId, Guid businessId, Guid serviceId, ServiceRequest request, CancellationToken ct)
    {
        var service = await GetOwnedAsync(ownerId, businessId, serviceId, ct);
        service.Name = request.Name.Trim(); service.Description = request.Description.Trim();
        service.Price = request.Price; service.Currency = request.Currency; service.DurationMinutes = request.DurationMinutes;
        service.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(ct);
        // Return the persisted timestamp precision, matching later PostgreSQL reads.
        await context.Entry(service).ReloadAsync(ct);
        return Map(service);
    }

    public async Task<ServiceResponse> SetStatusAsync(Guid ownerId, Guid businessId, Guid serviceId, bool active, CancellationToken ct)
    {
        var service = await GetOwnedAsync(ownerId, businessId, serviceId, ct);
        service.IsActive = active; service.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(ct);
        // Return the persisted timestamp precision, matching later PostgreSQL reads.
        await context.Entry(service).ReloadAsync(ct);
        return Map(service);
    }

    public async Task<IReadOnlyList<ServiceResponse>> GetOwnerServicesAsync(Guid ownerId, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        var services = await context.Services.AsNoTracking().Where(s => s.BusinessId == business.Id)
            .OrderBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(ct);
        return services.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ServiceResponse>> GetPublicServicesAsync(Guid businessId, CancellationToken ct)
    {
        await businesses.GetPublicAsync(businessId, ct);
        var services = await context.Services.AsNoTracking().Where(s => s.BusinessId == businessId && s.IsActive
            && s.Business.IsActive && s.Business.Owner.IsActive).OrderBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(ct);
        return services.Select(Map).ToList();
    }

    public async Task<ServiceResponse> GetPublicAsync(Guid businessId, Guid serviceId, CancellationToken ct)
    {
        var service = await context.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == serviceId
            && s.BusinessId == businessId && s.IsActive && s.Business.IsActive && s.Business.Owner.IsActive, ct);
        return service is null ? throw new RequestException(404, "Service not found.") : Map(service);
    }

    private async Task<Service> GetOwnedAsync(Guid ownerId, Guid businessId, Guid serviceId, CancellationToken ct)
    {
        await businesses.GetOwnedEntityAsync(ownerId, businessId, ct);
        return await context.Services.SingleOrDefaultAsync(s => s.Id == serviceId && s.BusinessId == businessId, ct)
            ?? throw new RequestException(404, "Service not found.");
    }
    private static ServiceResponse Map(Service s) => new(s.Id, s.BusinessId, s.Name, s.Description,
        s.Price, s.Currency, s.DurationMinutes, s.IsActive, s.CreatedAtUtc, s.UpdatedAtUtc);
}
