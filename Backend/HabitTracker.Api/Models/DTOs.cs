using System.ComponentModel.DataAnnotations;

namespace HabitTracker.Api.Models.DTOs;

public record RegisterDto([Required] string Username, [Required][EmailAddress] string Email, [Required] string Password);
public record LoginDto([Required][EmailAddress] string Email, [Required] string Password);
public record UserDto(string Id, string Username, string Email);
public record AuthResponseDto(string Token, UserDto User);

public record CreateHabitDto(
    [Required] string Name,
    [Range(1, 100)] int? TargetCount,
    List<DayOfWeek>? ActiveDays = null,
    DateTime? StartDate = null,
    string? Description = null,
    string? Icon = null,
    string? Color = null
);

public record UpdateHabitDto(
    string? Name = null,
    string? Description = null,
    string? Icon = null,
    string? Color = null,
    [Range(1, 100)] int? TargetCount = null,
    List<DayOfWeek>? ActiveDays = null,
    bool? IsArchived = null
);

public record HabitDto(
    string Id,
    string UserId,
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    int TargetCount,
    List<DayOfWeek> ActiveDays,
    DateTime StartDate,
    bool IsArchived,
    DateTime CreatedAt,
    double SuccessProbability = 0,
    string? ShapExplanation = null,
    bool ShouldAskConfidence = false,
    string? ActiveLearningReason = null
);

public record TrackHabitDto([Required] DateTime Date, [Required][Range(0,100)] int CompletedCount, string? FailureReason = null);

public record HabitConfidenceDto([Required] DateTime Date, [Required][Range(1, 10)] int Score);

public record HabitEntryDto(
    string Id,
    string HabitId,
    string UserId,
    DateTime Date,
    int CompletedCount,
    bool IsFullyCompleted,
    string? FailureReason,
    int? ConfidenceScore
);

public record DailyHabitStatusDto(
    string HabitId,
    string Name,
    string? Icon,
    string? Color,
    int TargetCount,
    int CompletedToday,
    bool IsFullyCompletedToday
);
