using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Models.DTOs;

namespace HabitTracker.Api.Services;

public class HabitService
{
    private readonly IMongoCollection<Habit> _habitsCollection;
    private readonly IMongoCollection<HabitEntry> _habitEntriesCollection;
    private readonly IMongoCollection<HabitCategory> _categoriesCollection;
    private readonly IMongoCollection<PomodoroSession> _pomodoroSessionsCollection;
    private readonly IMongoCollection<ActiveLearningPrompt> _activeLearningPromptsCollection;
    private readonly HabitPredictionService _predictionService;

    public HabitService(IMongoDatabase mongoDatabase, HabitPredictionService predictionService)
    {
        _habitsCollection = mongoDatabase.GetCollection<Habit>("Habits");
        _habitEntriesCollection = mongoDatabase.GetCollection<HabitEntry>("HabitEntries");
        _categoriesCollection = mongoDatabase.GetCollection<HabitCategory>("HabitCategories");
        _pomodoroSessionsCollection = mongoDatabase.GetCollection<PomodoroSession>("PomodoroSessions");
        _activeLearningPromptsCollection = mongoDatabase.GetCollection<ActiveLearningPrompt>("ActiveLearningPrompts");
        _predictionService = predictionService;
    }

    public async Task<HabitDto?> CreateHabitAsync(CreateHabitDto createDto, string userId)
    {
        var habit = new Habit
        {
            UserId = userId,
            Name = createDto.Name.Trim(),
            Description = NormalizeOptionalText(createDto.Description),
            Category = NormalizeOptionalText(createDto.Category),
            Icon = createDto.Icon,
            Color = createDto.Color,
            TargetCount = createDto.TargetCount ?? 1,
            ActiveDays = NormalizeActiveDays(createDto.ActiveDays),
            StartDate = createDto.StartDate?.Date ?? DateTime.UtcNow.Date,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };
        await _habitsCollection.InsertOneAsync(habit);
        _predictionService.InvalidateUserPredictionCache(userId);
        return MapToHabitDto(habit);
    }

    public async Task<List<HabitDto>> GetHabitsAsync(string userId, bool includeArchived = false)
    {
        var filter = Builders<Habit>.Filter.Eq(h => h.UserId, userId);
        if (!includeArchived)
        {
            filter &= Builders<Habit>.Filter.Eq(h => h.IsArchived, false);
        }
        var habits = await _habitsCollection.Find(filter).SortByDescending(h => h.CreatedAt).ToListAsync();
        return habits.Select(MapToHabitDto).ToList();
    }
    
    public async Task<HabitDto?> GetHabitByIdAsync(string habitId, string userId)
    {
        var habit = await _habitsCollection.Find(h => h.Id == habitId && h.UserId == userId).FirstOrDefaultAsync();
        return habit == null ? null : MapToHabitDto(habit);
    }

    public async Task<HabitDto?> UpdateHabitAsync(string habitId, UpdateHabitDto updateDto, string userId)
    {
        var filter = Builders<Habit>.Filter.And(
            Builders<Habit>.Filter.Eq(h => h.Id, habitId),
            Builders<Habit>.Filter.Eq(h => h.UserId, userId)
        );

        var updateBuilder = Builders<Habit>.Update;
        var updates = new List<UpdateDefinition<Habit>>();

        if (updateDto.Name != null) updates.Add(updateBuilder.Set(h => h.Name, updateDto.Name.Trim()));
        if (updateDto.Description != null) updates.Add(updateBuilder.Set(h => h.Description, NormalizeOptionalText(updateDto.Description)));
        if (updateDto.Category != null) updates.Add(updateBuilder.Set(h => h.Category, NormalizeOptionalText(updateDto.Category)));
        if (updateDto.Icon != null) updates.Add(updateBuilder.Set(h => h.Icon, updateDto.Icon));
        if (updateDto.Color != null) updates.Add(updateBuilder.Set(h => h.Color, updateDto.Color));
        if (updateDto.TargetCount.HasValue) updates.Add(updateBuilder.Set(h => h.TargetCount, updateDto.TargetCount.Value));
        if (updateDto.ActiveDays != null) updates.Add(updateBuilder.Set(h => h.ActiveDays, NormalizeActiveDays(updateDto.ActiveDays)));
        if (updateDto.StartDate.HasValue) updates.Add(updateBuilder.Set(h => h.StartDate, updateDto.StartDate.Value.Date));
        if (updateDto.IsArchived.HasValue) updates.Add(updateBuilder.Set(h => h.IsArchived, updateDto.IsArchived.Value));
        
        if (!updates.Any()) return await GetHabitByIdAsync(habitId, userId);

        var combinedUpdate = updateBuilder.Combine(updates);
        var result = await _habitsCollection.UpdateOneAsync(filter, combinedUpdate);

        if (result.ModifiedCount > 0)
        {
            _predictionService.InvalidateUserPredictionCache(userId);
            return await GetHabitByIdAsync(habitId, userId);
        }

        return null;
    }

    public async Task<bool> DeleteHabitAsync(string habitId, string userId)
    {
        await _habitEntriesCollection.DeleteManyAsync(e => e.HabitId == habitId && e.UserId == userId);
        var result = await _habitsCollection.DeleteOneAsync(h => h.Id == habitId && h.UserId == userId);
        if (result.DeletedCount > 0) _predictionService.InvalidateUserPredictionCache(userId);
        return result.DeletedCount > 0;
    }

    public async Task<HabitEntryDto?> TrackHabitAsync(string habitId, TrackHabitDto trackDto, string userId)
    {
        var habit = await _habitsCollection.Find(h => h.Id == habitId && h.UserId == userId).FirstOrDefaultAsync();
        if (habit == null) return null;

        var entryDate = trackDto.Date.Date;
        var filter = Builders<HabitEntry>.Filter.And(
            Builders<HabitEntry>.Filter.Eq(e => e.HabitId, habitId),
            Builders<HabitEntry>.Filter.Eq(e => e.UserId, userId),
            Builders<HabitEntry>.Filter.Eq(e => e.Date, entryDate)
        );

        var completedCount = Math.Clamp(trackDto.CompletedCount, 0, habit.TargetCount);
        var update = Builders<HabitEntry>.Update
            .Set(e => e.CompletedCount, completedCount)
            .Set(e => e.IsFullyCompleted, completedCount >= habit.TargetCount)
            .Set(e => e.FailureReason, NormalizeOptionalText(trackDto.FailureReason))
            .Set(e => e.LastModified, DateTime.UtcNow)
            .SetOnInsert(e => e.HabitId, habitId)
            .SetOnInsert(e => e.UserId, userId)
            .SetOnInsert(e => e.Date, entryDate);

        var entry = await _habitEntriesCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<HabitEntry> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

        _predictionService.InvalidateUserPredictionCache(userId);
        return MapToHabitEntryDto(entry);
    }

    public async Task<bool> LogConfidenceAsync(string habitId, HabitConfidenceDto dto, string userId)
    {
        var filter = Builders<HabitEntry>.Filter.And(
            Builders<HabitEntry>.Filter.Eq(e => e.HabitId, habitId),
            Builders<HabitEntry>.Filter.Eq(e => e.UserId, userId),
            Builders<HabitEntry>.Filter.Eq(e => e.Date, dto.Date.Date)
        );

        var update = Builders<HabitEntry>.Update
            .Set(e => e.ConfidenceScore, dto.Score)
            .Set(e => e.ConfidenceReason, NormalizeOptionalText(dto.Reason))
            .Set(e => e.LastModified, DateTime.UtcNow)
            .SetOnInsert(e => e.HabitId, habitId)
            .SetOnInsert(e => e.UserId, userId)
            .SetOnInsert(e => e.Date, dto.Date.Date)
            .SetOnInsert(e => e.CompletedCount, 0)
            .SetOnInsert(e => e.IsFullyCompleted, false);

        var result = await _habitEntriesCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        if (result.ModifiedCount > 0 || result.UpsertedId != null) _predictionService.InvalidateUserPredictionCache(userId);
        return result.ModifiedCount > 0 || result.UpsertedId != null;
    }

    public async Task<List<DailyHabitStatusDto>> GetDailyHabitsStatusAsync(string userId, DateTime date)
    {
        var targetDate = date.Date;
        var dayOfWeek = targetDate.DayOfWeek;

        var activeHabits = await _habitsCollection.Find(h =>
            h.UserId == userId &&
            !h.IsArchived &&
            h.StartDate <= targetDate &&
            h.ActiveDays.Contains(dayOfWeek)
        ).ToListAsync();

        if (!activeHabits.Any()) return new List<DailyHabitStatusDto>();

        var habitIds = activeHabits.Select(h => h.Id).ToList();
        var entries = await _habitEntriesCollection.Find(e =>
            e.UserId == userId &&
            habitIds.Contains(e.HabitId) &&
            e.Date == targetDate
        ).ToListAsync();

        var entriesMap = entries.ToDictionary(e => e.HabitId!);

        var result = activeHabits.Select(h => {
            entriesMap.TryGetValue(h.Id!, out var entry);
            return new DailyHabitStatusDto(
                h.Id!,
                h.Name,
                h.Icon,
                h.Color,
                h.TargetCount,
                entry?.CompletedCount ?? 0,
                entry?.IsFullyCompleted ?? false
            );
        }).ToList();

        return result;
    }
    
    public async Task<List<HabitEntryDto>> GetHabitEntriesForPeriodAsync(string habitId, string userId, DateTime startDate, DateTime endDate)
    {
        var entries = await _habitEntriesCollection.Find(e =>
            e.HabitId == habitId &&
            e.UserId == userId &&
            e.Date >= startDate.Date &&
            e.Date <= endDate.Date
        ).SortBy(e => e.Date).ToListAsync();
        return entries.Select(MapToHabitEntryDto).ToList();
    }

    public async Task<List<HabitEntryDto>> GetAllHabitEntriesForPeriodAsync(string userId, DateTime startDate, DateTime endDate)
    {
        var entries = await _habitEntriesCollection.Find(e =>
            e.UserId == userId &&
            e.Date >= startDate.Date &&
            e.Date <= endDate.Date
        ).SortBy(e => e.Date).ToListAsync();
        return entries.Select(MapToHabitEntryDto).ToList();
    }

    public async Task<HabitStatsDto?> GetHabitStatsAsync(string habitId, string userId, int periodDays)
    {
        periodDays = Math.Clamp(periodDays, 1, 365);
        var habit = await _habitsCollection.Find(h => h.Id == habitId && h.UserId == userId).FirstOrDefaultAsync();
        if (habit == null) return null;

        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-periodDays + 1);
        if (habit.StartDate.Date > startDate) startDate = habit.StartDate.Date;

        var entries = await _habitEntriesCollection.Find(e =>
            e.HabitId == habitId &&
            e.UserId == userId &&
            e.Date >= startDate &&
            e.Date <= endDate
        ).ToListAsync();

        var entriesByDate = entries
            .GroupBy(e => e.Date.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.LastModified).First());

        var expectedDates = new List<DateTime>();
        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            if (habit.ActiveDays.Contains(day.DayOfWeek)) expectedDates.Add(day);
        }

        var fullSuccesses = expectedDates.Count(day =>
            entriesByDate.TryGetValue(day, out var entry) && entry.IsFullyCompleted);
        var totalCompleted = expectedDates.Sum(day =>
            entriesByDate.TryGetValue(day, out var entry) ? entry.CompletedCount : 0);

        var currentStreak = 0;
        for (var day = endDate; day >= startDate; day = day.AddDays(-1))
        {
            if (!habit.ActiveDays.Contains(day.DayOfWeek)) continue;
            if (entriesByDate.TryGetValue(day, out var entry) && entry.IsFullyCompleted)
            {
                currentStreak++;
                continue;
            }
            break;
        }

        var expectedCount = expectedDates.Count;
        var missedDays = expectedCount - fullSuccesses;
        var successRate = expectedCount == 0 ? 0 : Math.Round(fullSuccesses / (double)expectedCount, 4);

        return new HabitStatsDto(
            habit.Id!,
            periodDays,
            totalCompleted,
            currentStreak,
            successRate,
            entries.Count,
            missedDays);
    }

    public async Task<List<HabitCategoryDto>> GetCategoriesAsync(string userId)
    {
        var categories = await _categoriesCollection.Find(c => c.UserId == userId)
            .SortBy(c => c.Name)
            .ToListAsync();
        return categories.Select(MapToCategoryDto).ToList();
    }

    public async Task<HabitCategoryDto> CreateCategoryAsync(CreateHabitCategoryDto dto, string userId)
    {
        var category = new HabitCategory
        {
            UserId = userId,
            Name = dto.Name.Trim(),
            Color = dto.Color,
            Icon = dto.Icon,
            CreatedAt = DateTime.UtcNow
        };
        await _categoriesCollection.InsertOneAsync(category);
        return MapToCategoryDto(category);
    }

    public async Task<HabitCategoryDto?> UpdateCategoryAsync(string categoryId, UpdateHabitCategoryDto dto, string userId)
    {
        var updates = new List<UpdateDefinition<HabitCategory>>();
        var updateBuilder = Builders<HabitCategory>.Update;
        if (dto.Name != null) updates.Add(updateBuilder.Set(c => c.Name, dto.Name.Trim()));
        if (dto.Color != null) updates.Add(updateBuilder.Set(c => c.Color, dto.Color));
        if (dto.Icon != null) updates.Add(updateBuilder.Set(c => c.Icon, dto.Icon));
        if (!updates.Any()) return null;

        var category = await _categoriesCollection.FindOneAndUpdateAsync(
            c => c.Id == categoryId && c.UserId == userId,
            updateBuilder.Combine(updates),
            new FindOneAndUpdateOptions<HabitCategory> { ReturnDocument = ReturnDocument.After });

        return category == null ? null : MapToCategoryDto(category);
    }

    public async Task<bool> DeleteCategoryAsync(string categoryId, string userId)
    {
        var category = await _categoriesCollection.Find(c => c.Id == categoryId && c.UserId == userId).FirstOrDefaultAsync();
        if (category == null) return false;

        await _habitsCollection.UpdateManyAsync(
            h => h.UserId == userId && h.Category == category.Name,
            Builders<Habit>.Update.Set(h => h.Category, null));

        var result = await _categoriesCollection.DeleteOneAsync(c => c.Id == categoryId && c.UserId == userId);
        if (result.DeletedCount > 0) _predictionService.InvalidateUserPredictionCache(userId);
        return result.DeletedCount > 0;
    }

    public async Task<PomodoroSessionDto?> CreatePomodoroSessionAsync(CreatePomodoroSessionDto dto, string userId)
    {
        if (dto.HabitId != null)
        {
            var habitExists = await _habitsCollection.Find(h => h.Id == dto.HabitId && h.UserId == userId).AnyAsync();
            if (!habitExists) return null;
        }

        var session = new PomodoroSession
        {
            UserId = userId,
            HabitId = dto.HabitId,
            StartedAt = dto.StartedAt,
            EndedAt = dto.EndedAt,
            DurationMinutes = dto.DurationMinutes,
            Mode = string.IsNullOrWhiteSpace(dto.Mode) ? "pomodoro" : dto.Mode.Trim(),
            Completed = dto.Completed,
            CreatedAt = DateTime.UtcNow
        };

        await _pomodoroSessionsCollection.InsertOneAsync(session);
        return MapToPomodoroSessionDto(session);
    }

    public async Task<List<PomodoroSessionDto>> GetPomodoroSessionsAsync(string userId, DateTime startDate, DateTime endDate)
    {
        var sessions = await _pomodoroSessionsCollection.Find(s =>
            s.UserId == userId &&
            s.StartedAt >= startDate &&
            s.StartedAt <= endDate)
            .SortByDescending(s => s.StartedAt)
            .ToListAsync();

        return sessions.Select(MapToPomodoroSessionDto).ToList();
    }

    public Task<PomodoroSessionDto?> CompletePomodoroAsync(CompletePomodoroDto dto, string userId)
    {
        var endedAt = dto.EndedAt ?? DateTime.UtcNow;
        var durationMinutes = dto.DurationMinutes
            ?? (dto.DurationSeconds.HasValue ? Math.Max(1, (int)Math.Round(dto.DurationSeconds.Value / 60.0)) : 25);
        durationMinutes = Math.Clamp(durationMinutes, 1, 240);
        var startedAt = dto.StartedAt ?? endedAt.AddMinutes(-durationMinutes);

        return CreatePomodoroSessionAsync(new CreatePomodoroSessionDto(
            dto.HabitId,
            startedAt,
            endedAt,
            durationMinutes,
            "pomodoro",
            true), userId);
    }

    public async Task<PomodoroStatsDto> GetPomodoroStatsAsync(string userId, DateTime startDate, DateTime endDate)
    {
        var sessions = await _pomodoroSessionsCollection.Find(s =>
            s.UserId == userId &&
            s.StartedAt >= startDate &&
            s.StartedAt <= endDate)
            .ToListAsync();

        var habitIds = sessions
            .Select(s => s.HabitId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var habits = habitIds.Count == 0
            ? new List<Habit>()
            : await _habitsCollection.Find(h => h.UserId == userId && habitIds.Contains(h.Id)).ToListAsync();

        var habitNames = habits.ToDictionary(h => h.Id!, h => h.Name);
        var weekStart = DateTime.UtcNow.Date.AddDays(-((int)DateTime.UtcNow.DayOfWeek == 0 ? 6 : (int)DateTime.UtcNow.DayOfWeek - 1));

        var byHabit = sessions
            .Where(s => s.Mode == "pomodoro" && s.Completed)
            .GroupBy(s => s.HabitId)
            .Select(group =>
            {
                var habitId = group.Key;
                var habitName = habitId != null && habitNames.TryGetValue(habitId, out var name)
                    ? name
                    : "Без прив'язки";
                return new PomodoroHabitStatsDto(
                    habitId,
                    habitName,
                    group.Count(),
                    group.Sum(s => s.DurationMinutes));
            })
            .OrderByDescending(item => item.FocusMinutes)
            .ToList();

        var completedSessions = sessions.Where(s => s.Completed && s.Mode == "pomodoro").ToList();
        var totalMinutes = completedSessions.Sum(s => s.DurationMinutes);

        return new PomodoroStatsDto(
            sessions.Count,
            completedSessions.Count,
            totalMinutes,
            completedSessions.Count == 0 ? 0 : Math.Round(totalMinutes / (double)completedSessions.Count, 1),
            completedSessions.Where(s => s.StartedAt >= weekStart).Sum(s => s.DurationMinutes),
            byHabit);
    }

    public async Task<bool> CanShowConfidencePromptAsync(string habitId, string userId)
    {
        var since = DateTime.UtcNow.AddDays(-3);
        var recentCount = await _activeLearningPromptsCollection.CountDocumentsAsync(p =>
            p.UserId == userId &&
            p.HabitId == habitId &&
            p.ShownAt >= since);

        if (recentCount > 0) return false;

        var windowStart = DateTime.UtcNow.AddDays(-14);
        var windowCount = await _activeLearningPromptsCollection.CountDocumentsAsync(p =>
            p.UserId == userId &&
            p.HabitId == habitId &&
            p.ShownAt >= windowStart);

        return windowCount < 2;
    }

    public Task RecordConfidencePromptAsync(string habitId, string userId, string? reason)
    {
        return _activeLearningPromptsCollection.InsertOneAsync(new ActiveLearningPrompt
        {
            UserId = userId,
            HabitId = habitId,
            Reason = reason,
            ShownAt = DateTime.UtcNow
        });
    }

    private HabitDto MapToHabitDto(Habit habit) =>
        new HabitDto(
            habit.Id!, 
            habit.UserId, 
            habit.Name, 
            habit.Description, 
            habit.Category,
            habit.Icon, 
            habit.Color, 
            habit.TargetCount, 
            habit.ActiveDays, 
            habit.StartDate, 
            habit.IsArchived, 
            habit.CreatedAt,
            0,
            null,
            false,
            null
        );

    private HabitEntryDto MapToHabitEntryDto(HabitEntry entry) =>
        new HabitEntryDto(entry.Id!, entry.HabitId, entry.UserId, entry.Date, entry.CompletedCount, entry.IsFullyCompleted, entry.FailureReason, entry.ConfidenceScore, entry.ConfidenceReason);

    private HabitCategoryDto MapToCategoryDto(HabitCategory category) =>
        new HabitCategoryDto(category.Id!, category.UserId, category.Name, category.Color, category.Icon, category.CreatedAt);

    private PomodoroSessionDto MapToPomodoroSessionDto(PomodoroSession session) =>
        new PomodoroSessionDto(session.Id!, session.UserId, session.HabitId, session.StartedAt, session.EndedAt, session.DurationMinutes, session.Mode, session.Completed, session.CreatedAt);
    
    private List<DayOfWeek> GetDefaultActiveDays() =>
        Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();

    private List<DayOfWeek> NormalizeActiveDays(List<DayOfWeek>? activeDays)
    {
        var normalized = activeDays?.Distinct().OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d).ToList();
        return normalized == null || normalized.Count == 0 ? GetDefaultActiveDays() : normalized;
    }

    private string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }
}
