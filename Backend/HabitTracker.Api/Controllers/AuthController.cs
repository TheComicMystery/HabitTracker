using Microsoft.AspNetCore.Mvc;
using HabitTracker.Api.Models.DTOs;
using HabitTracker.Api.Services;

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

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        var (user, error) = await _authService.RegisterAsync(registerDto);
        if (error != null) return BadRequest(new { message = error });
        
        return Ok(new { message = "Користувача успішно зареєстровано. Будь ласка, увійдіть..", user });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (authResponse, error) = await _authService.LoginAsync(loginDto);
        if (error != null) return Unauthorized(new { message = error });
        
        return Ok(authResponse);
    }
}