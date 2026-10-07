using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceHub.Api.Configuration;
using ServiceHub.Api.Contracts.Businesses;
using ServiceHub.Api.Data;

namespace ServiceHub.Api.Controllers;

[ApiController]
public sealed class BusinessOptionsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("api/categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> Categories(CancellationToken ct)
        => Ok(await context.BusinessCategories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name)).ToListAsync(ct));

    [HttpGet("api/time-zones")]
    public ActionResult<IReadOnlyList<TimeZoneResponse>> TimeZones() => Ok(BusinessTimeZones.Supported);
}
