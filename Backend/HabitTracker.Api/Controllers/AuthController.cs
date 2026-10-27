using Microsoft.AspNetCore.Mvc;
using HabitTracker.Api.Models.DTOs;
using HabitTracker.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (user, error) = await _authService.RegisterAsync(registerDto);
        if (error != null) return BadRequest(new { message = error });

        return Ok(new { message = "Користувача успішно зареєстровано. Будь ласка, увійдіть.", user });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (authResponse, error) = await _authService.LoginAsync(loginDto);
        if (error != null) return Unauthorized(new { message = error });

        return Ok(authResponse);
    }

    [HttpGet("onboarding")]
    [Authorize]
    public async Task<IActionResult> GetOnboarding()
    {
        var profile = await _authService.GetOnboardingProfileAsync(GetUserId());
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpPut("onboarding")]
    [Authorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateOnboarding(UpdateOnboardingProfileDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var profile = await _authService.UpdateOnboardingProfileAsync(GetUserId(), dto);
        return profile == null ? NotFound() : Ok(profile);
    }
}
