using Microsoft.AspNetCore.Mvc;

namespace NightMarket.WebApi.Host.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        });
    }
}
