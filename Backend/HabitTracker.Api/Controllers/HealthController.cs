using Microsoft.AspNetCore.Mvc;
using HabitTracker.Api.Services;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HabitPredictionService _predictionService;

    public HealthController(HabitPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    [HttpGet("model")]
    public IActionResult GetModelHealth()
    {
        return Ok(_predictionService.GetModelHealth());
    }
}
