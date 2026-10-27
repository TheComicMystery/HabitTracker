using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using HabitTracker.Api.Models.DTOs;
using HabitTracker.Api.Services;
using System.Security.Claims;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PomodoroController : ControllerBase
{
    private readonly HabitService _habitService;

    public PomodoroController(HabitService habitService)
    {
        _habitService = habitService;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost("complete")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Complete(CompletePomodoroDto dto)
    {
        var session = await _habitService.CompletePomodoroAsync(dto, GetUserId());
        return session == null ? BadRequest("Habit not found.") : Ok(session);
    }
}
