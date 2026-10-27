using System.ComponentModel.DataAnnotations;

namespace HabitTracker.Api.Models.DTOs;

public record RegisterDto([Required] string Username, [Required][EmailAddress] string Email, [Required] string Password);
public record LoginDto([Required][EmailAddress] string Email, [Required] string Password);
public record UserDto(
    string Id,
    string Username,
    string Email,
    bool OnboardingCompleted = false,
    int? SubjectiveDifficulty = null,
    int? OverallDisciplineSelf = null,
    int? WeekendVulnerability = null,
    string? PreferredTimeOfDay = null,
    string? InitialFailureReason = null
);
public record AuthResponseDto(string Token, UserDto User);
public record OnboardingProfileDto(
    bool OnboardingCompleted,
    int? SubjectiveDifficulty,
    int? OverallDisciplineSelf,
    int? WeekendVulnerability,
    string? PreferredTimeOfDay,
    string? InitialFailureReason
);
public record UpdateOnboardingProfileDto(
    [Range(1, 10)] int? SubjectiveDifficulty,
    [Range(1, 10)] int? OverallDisciplineSelf,
    [Range(1, 4)] int? WeekendVulnerability,
    string? PreferredTimeOfDay,
    string? InitialFailureReason,
    bool Complete = true
);

public record CreateHabitDto(
    [Required] string Name,
    [Range(1, 100)] int? TargetCount,
    List<DayOfWeek>? ActiveDays = null,
    DateTime? StartDate = null,
    string? Description = null,
    string? Category = null,
    string? Icon = null,
    string? Color = null
);

public record UpdateHabitDto(
    string? Name = null,
    string? Description = null,
    string? Category = null,
    string? Icon = null,
    string? Color = null,
    [Range(1, 100)] int? TargetCount = null,
    List<DayOfWeek>? ActiveDays = null,
    DateTime? StartDate = null,
    bool? IsArchived = null
);

public record HabitDto(
    string Id,
    string UserId,
    string Name,
    string? Description,
    string? Category,
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

public record HabitConfidenceDto([Required] DateTime Date, [Required][Range(1, 10)] int Score, string? Reason = null);

public record HabitEntryDto(
    string Id,
    string HabitId,
    string UserId,
    DateTime Date,
    int CompletedCount,
    bool IsFullyCompleted,
    string? FailureReason,
    int? ConfidenceScore,
    string? ConfidenceReason
);

public record HabitStatsDto(
    string HabitId,
    int PeriodDays,
    int TotalCompleted,
    int CurrentStreak,
    double SuccessRate,
    int EntriesCount,
    int MissedDays
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

public record HabitCategoryDto(
    string Id,
    string UserId,
    string Name,
    string? Color,
    string? Icon,
    DateTime CreatedAt
);

public record CreateHabitCategoryDto(
    [Required] string Name,
    string? Color = null,
    string? Icon = null
);

public record UpdateHabitCategoryDto(
    string? Name = null,
    string? Color = null,
    string? Icon = null
);

public record PomodoroSessionDto(
    string Id,
    string UserId,
    string? HabitId,
    DateTime StartedAt,
    DateTime EndedAt,
    int DurationMinutes,
    string Mode,
    bool Completed,
    DateTime CreatedAt
);

public record CreatePomodoroSessionDto(
    string? HabitId,
    [Required] DateTime StartedAt,
    [Required] DateTime EndedAt,
    [Range(1, 240)] int DurationMinutes,
    string Mode = "pomodoro",
    bool Completed = true
);

public record CompletePomodoroDto(
    string? HabitId = null,
    DateTime? StartedAt = null,
    DateTime? EndedAt = null,
    int? DurationSeconds = null,
    int? DurationMinutes = null
);

public record PomodoroHabitStatsDto(
    string? HabitId,
    string HabitName,
    int Sessions,
    int FocusMinutes
);

public record PomodoroStatsDto(
    int TotalSessions,
    int CompletedSessions,
    int TotalFocusMinutes,
    double AverageSessionMinutes,
    int CurrentWeekFocusMinutes,
    List<PomodoroHabitStatsDto> ByHabit
);

public record ModelArtifactStatusDto(
    string Name,
    double Weight,
    string FileName,
    bool Exists
);

public record ModelHealthDto(
    bool IsTrained,
    DateTime? LastTrainedUtc,
    string RuntimeMode,
    bool ExternalEnsembleReady,
    double StackedPythonBlendWeight,
    double CalibrationIntercept,
    double CalibrationSlope,
    List<ModelArtifactStatusDto> EmbeddedModels
);
