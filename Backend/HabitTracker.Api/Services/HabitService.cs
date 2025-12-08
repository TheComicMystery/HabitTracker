using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Models.DTOs;
using HabitTracker.Api.Config;
using Microsoft.Extensions.Options;

namespace HabitTracker.Api.Services;

public class HabitService
{
    private readonly IMongoCollection<Habit> _habitsCollection;
    private readonly IMongoCollection<HabitEntry> _habitEntriesCollection;

    public HabitService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _habitsCollection = mongoDatabase.GetCollection<Habit>("Habits");
        _habitEntriesCollection = mongoDatabase.GetCollection<HabitEntry>("HabitEntries");
    }

    public async Task<HabitDto?> CreateHabitAsync(CreateHabitDto createDto, string userId)
    {
        var habit = new Habit
        {
            UserId = userId,
            Name = createDto.Name,
            Description = createDto.Description,
            Icon = createDto.Icon,
            Color = createDto.Color,
            TargetCount = createDto.TargetCount ?? 1,
            ActiveDays = createDto.ActiveDays ?? GetDefaultActiveDays(),
            StartDate = createDto.StartDate?.Date ?? DateTime.UtcNow.Date,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };
        await _habitsCollection.InsertOneAsync(habit);
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

        if (updateDto.Name != null) updates.Add(updateBuilder.Set(h => h.Name, updateDto.Name));
        if (updateDto.Description != null) updates.Add(updateBuilder.Set(h => h.Description, updateDto.Description));
        if (updateDto.Icon != null) updates.Add(updateBuilder.Set(h => h.Icon, updateDto.Icon));
        if (updateDto.Color != null) updates.Add(updateBuilder.Set(h => h.Color, updateDto.Color));
        if (updateDto.TargetCount.HasValue) updates.Add(updateBuilder.Set(h => h.TargetCount, updateDto.TargetCount.Value));
        if (updateDto.ActiveDays != null) updates.Add(updateBuilder.Set(h => h.ActiveDays, updateDto.ActiveDays));
        if (updateDto.IsArchived.HasValue) updates.Add(updateBuilder.Set(h => h.IsArchived, updateDto.IsArchived.Value));
        
        if (!updates.Any()) return await GetHabitByIdAsync(habitId, userId);

        var combinedUpdate = updateBuilder.Combine(updates);
        var result = await _habitsCollection.UpdateOneAsync(filter, combinedUpdate);

        return result.ModifiedCount > 0 ? await GetHabitByIdAsync(habitId, userId) : null;
    }

    public async Task<bool> DeleteHabitAsync(string habitId, string userId)
    {
        await _habitEntriesCollection.DeleteManyAsync(e => e.HabitId == habitId && e.UserId == userId);
        var result = await _habitsCollection.DeleteOneAsync(h => h.Id == habitId && h.UserId == userId);
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

        var existingEntry = await _habitEntriesCollection.Find(filter).FirstOrDefaultAsync();

        if (existingEntry != null)
        {
            existingEntry.CompletedCount = Math.Min(trackDto.CompletedCount, habit.TargetCount);
            existingEntry.IsFullyCompleted = existingEntry.CompletedCount >= habit.TargetCount;
            existingEntry.LastModified = DateTime.UtcNow;
            await _habitEntriesCollection.ReplaceOneAsync(filter, existingEntry);
            return MapToHabitEntryDto(existingEntry);
        }
        else
        {
            var newEntry = new HabitEntry
            {
                HabitId = habitId,
                UserId = userId,
                Date = entryDate,
                CompletedCount = Math.Min(trackDto.CompletedCount, habit.TargetCount),
                IsFullyCompleted = trackDto.CompletedCount >= habit.TargetCount,
                LastModified = DateTime.UtcNow
            };
            await _habitEntriesCollection.InsertOneAsync(newEntry);
            return MapToHabitEntryDto(newEntry);
        }
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

    private HabitDto MapToHabitDto(Habit habit) =>
        new HabitDto(
            habit.Id!, 
            habit.UserId, 
            habit.Name, 
            habit.Description, 
            habit.Icon, 
            habit.Color, 
            habit.TargetCount, 
            habit.ActiveDays, 
            habit.StartDate, 
            habit.IsArchived, 
            habit.CreatedAt,
            0
        );

    private HabitEntryDto MapToHabitEntryDto(HabitEntry entry) =>
        new HabitEntryDto(entry.Id!, entry.HabitId, entry.UserId, entry.Date, entry.CompletedCount, entry.IsFullyCompleted);
    
    private List<DayOfWeek> GetDefaultActiveDays() =>
        Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
}