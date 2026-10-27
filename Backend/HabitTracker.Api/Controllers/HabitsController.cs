using Microsoft.AspNetCore.Mvc;
using HabitTracker.Api.Models.DTOs;
using HabitTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateHabit(CreateHabitDto createDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        var habit = await _habitService.CreateHabitAsync(createDto, userId);
        if (habit == null) return BadRequest("Could not create habit.");
        
        return CreatedAtAction(nameof(GetHabitById), new { habitId = habit.Id }, habit);
    }

    [HttpGet]
    [EnableRateLimiting("prediction")]
    public async Task<IActionResult> GetHabits([FromQuery] bool includeArchived = false, [FromQuery] bool includePredictions = true)
    {
        var userId = GetUserId();
        var habits = await _habitService.GetHabitsAsync(userId, includeArchived);

        if (!includePredictions)
        {
            return Ok(habits);
        }
        
        var enrichedHabits = new List<HabitDto>();
        
        await _mlService.TrainModelAsync();

        var habitModels = habits.Select(habitDto => new Models.Habit
        {
            Id = habitDto.Id,
            UserId = habitDto.UserId,
            Name = habitDto.Name,
            Description = habitDto.Description,
            Category = habitDto.Category,
            Icon = habitDto.Icon,
            Color = habitDto.Color,
            TargetCount = habitDto.TargetCount,
            ActiveDays = habitDto.ActiveDays,
            StartDate = habitDto.StartDate,
            IsArchived = habitDto.IsArchived,
            CreatedAt = habitDto.CreatedAt
        }).ToList();

        var predictions = await _mlService.PredictManyWithShapAsync(habitModels);
        var confidencePrompts = await _mlService.GetConfidencePromptsAsync(habitModels, predictions);

        foreach (var habitDto in habits)
        {
            predictions.TryGetValue(habitDto.Id, out var prediction);
            confidencePrompts.TryGetValue(habitDto.Id, out var confidencePrompt);
            var shouldAsk = confidencePrompt.ShouldAsk && await _habitService.CanShowConfidencePromptAsync(habitDto.Id, userId);

            if (shouldAsk)
            {
                await _habitService.RecordConfidencePromptAsync(habitDto.Id, userId, confidencePrompt.Reason);
            }
            
            enrichedHabits.Add(habitDto with
            {
                SuccessProbability = prediction.Probability,
                ShapExplanation = prediction.Explanation,
                ShouldAskConfidence = shouldAsk,
                ActiveLearningReason = shouldAsk ? confidencePrompt.Reason : null
            });
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
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateHabit(string habitId, UpdateHabitDto updateDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        var updatedHabit = await _habitService.UpdateHabitAsync(habitId, updateDto, userId);
        if (updatedHabit == null) return NotFound("Habit not found or update failed.");
        return Ok(updatedHabit);
    }

    [HttpDelete("{habitId}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> DeleteHabit(string habitId)
    {
        var userId = GetUserId();
        var success = await _habitService.DeleteHabitAsync(habitId, userId);
        if (!success) return NotFound("Habit not found or could not be deleted.");
        return NoContent();
    }

    [HttpPost("{habitId}/track")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> TrackHabit(string habitId, TrackHabitDto trackDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        var entry = await _habitService.TrackHabitAsync(habitId, trackDto, userId);
        if (entry == null) return BadRequest("Could not track habit. Habit not found or invalid data.");
        return Ok(entry);
    }

    [HttpPost("{habitId}/confidence")]
    [EnableRateLimiting("mutations")]
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

    [HttpGet("{habitId}/stats")]
    public async Task<IActionResult> GetHabitStats(string habitId, [FromQuery] int period = 30)
    {
        var stats = await _habitService.GetHabitStatsAsync(habitId, GetUserId(), period);
        return stats == null ? NotFound() : Ok(stats);
    }

    [HttpGet("entries")]
    public async Task<IActionResult> GetAllHabitEntries([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var userId = GetUserId();
        if (startDate == default || endDate == default || startDate > endDate)
        {
            return BadRequest("Invalid date range.");
        }
        var entries = await _habitService.GetAllHabitEntriesForPeriodAsync(userId, startDate, endDate);
        return Ok(entries);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _habitService.GetCategoriesAsync(GetUserId());
        return Ok(categories);
    }

    [HttpPost("categories")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateCategory(CreateHabitCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var category = await _habitService.CreateCategoryAsync(dto, GetUserId());
        return Ok(category);
    }

    [HttpPut("categories/{categoryId}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateCategory(string categoryId, UpdateHabitCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var category = await _habitService.UpdateCategoryAsync(categoryId, dto, GetUserId());
        if (category == null) return NotFound();
        return Ok(category);
    }

    [HttpDelete("categories/{categoryId}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> DeleteCategory(string categoryId)
    {
        var success = await _habitService.DeleteCategoryAsync(categoryId, GetUserId());
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("pomodoro-sessions")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreatePomodoroSession(CreatePomodoroSessionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var session = await _habitService.CreatePomodoroSessionAsync(dto, GetUserId());
        if (session == null) return BadRequest("Habit not found.");
        return Ok(session);
    }

    [HttpGet("pomodoro-sessions")]
    public async Task<IActionResult> GetPomodoroSessions([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        if (startDate == default || endDate == default || startDate > endDate)
        {
            return BadRequest("Invalid date range.");
        }
        var sessions = await _habitService.GetPomodoroSessionsAsync(GetUserId(), startDate, endDate);
        return Ok(sessions);
    }

    [HttpGet("pomodoro-stats")]
    public async Task<IActionResult> GetPomodoroStats([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);
        if (start > end)
        {
            return BadRequest("Invalid date range.");
        }

        var stats = await _habitService.GetPomodoroStatsAsync(GetUserId(), start, end);
        return Ok(stats);
    }
}
