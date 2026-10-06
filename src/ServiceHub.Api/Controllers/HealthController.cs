using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Contracts;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("ServiceHub API", "Healthy"));
}
