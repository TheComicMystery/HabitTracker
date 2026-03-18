using Microsoft.AspNetCore.Mvc;
using HabitTracker.Api.Models.DTOs;
using HabitTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] 
public class HabitsController : ControllerBase
{
    private readonly HabitService _habitService;
    private readonly HabitPredictionService _mlService;

    public HabitsController(HabitService habitService, HabitPredictionService mlService)
    {
        _habitService = habitService;
        _mlService = mlService;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost]
    public async Task<IActionResult> CreateHabit(CreateHabitDto createDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        var habit = await _habitService.CreateHabitAsync(createDto, userId);
        if (habit == null) return BadRequest("Could not create habit.");
        
        return CreatedAtAction(nameof(GetHabitById), new { habitId = habit.Id }, habit);
    }

    [HttpGet]
    public async Task<IActionResult> GetHabits([FromQuery] bool includeArchived = false)
    {
        var userId = GetUserId();
        var habits = await _habitService.GetHabitsAsync(userId, includeArchived);
        
        var enrichedHabits = new List<HabitDto>();
        
        await _mlService.TrainModelAsync();

        foreach (var habitDto in habits)
        {
            var habitModel = new Models.Habit 
            { 
                Id = habitDto.Id, 
                UserId = habitDto.UserId, 
                TargetCount = habitDto.TargetCount, 
                ActiveDays = habitDto.ActiveDays,
                StartDate = habitDto.StartDate
            };

            var (probability, shap) = await _mlService.PredictWithShapAsync(habitModel);
            
            enrichedHabits.Add(habitDto with { SuccessProbability = probability, ShapExplanation = shap });
        }

        return Ok(enrichedHabits);
    }

    [HttpGet("{habitId}")]
    public async Task<IActionResult> GetHabitById(string habitId)
    {
        var userId = GetUserId();
        var habit = await _habitService.GetHabitByIdAsync(habitId, userId);
        if (habit == null) return NotFound();
        return Ok(habit);
    }

    [HttpPut("{habitId}")]
    public async Task<IActionResult> UpdateHabit(string habitId, UpdateHabitDto updateDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        var updatedHabit = await _habitService.UpdateHabitAsync(habitId, updateDto, userId);
        if (updatedHabit == null) return NotFound("Habit not found or update failed.");
        return Ok(updatedHabit);
    }

    [HttpDelete("{habitId}")]
    public async Task<IActionResult> DeleteHabit(string habitId)
    {
        var userId = GetUserId();
        var success = await _habitService.DeleteHabitAsync(habitId, userId);
        if (!success) return NotFound("Habit not found or could not be deleted.");
        return NoContent();
    }

    [HttpPost("{habitId}/track")]
    public async Task<IActionResult> TrackHabit(string habitId, TrackHabitDto trackDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        var entry = await _habitService.TrackHabitAsync(habitId, trackDto, userId);
        if (entry == null) return BadRequest("Could not track habit. Habit not found or invalid data.");
        return Ok(entry);
    }

    [HttpPost("{habitId}/confidence")]
    public async Task<IActionResult> LogConfidence(string habitId, HabitConfidenceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        var success = await _habitService.LogConfidenceAsync(habitId, dto, userId);
        if (!success) return BadRequest("Could not log confidence.");
        return Ok();
    }

    [HttpGet("daily")] 
    public async Task<IActionResult> GetDailyHabitsStatus([FromQuery] DateTime? date)
    {
        var userId = GetUserId();
        var targetDate = date ?? DateTime.UtcNow; 
        var dailyStatus = await _habitService.GetDailyHabitsStatusAsync(userId, targetDate.Date);
        return Ok(dailyStatus);
    }

    [HttpGet("{habitId}/entries")] 
    public async Task<IActionResult> GetHabitEntries(string habitId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var userId = GetUserId();
        if (startDate == default || endDate == default || startDate > endDate)
        {
            return BadRequest("Invalid date range.");
        }
        var entries = await _habitService.GetHabitEntriesForPeriodAsync(habitId, userId, startDate, endDate);
        return Ok(entries);
    }
}