using HabitTracker.Api.Models;
using MongoDB.Driver;

namespace HabitTracker.Api.Services;

public class DatabaseInitializerService : IHostedService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<DatabaseInitializerService> _logger;

    public DatabaseInitializerService(IMongoDatabase database, ILogger<DatabaseInitializerService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateUserIndexes(cancellationToken);
            await CreateHabitIndexes(cancellationToken);
            await CreateEntryIndexes(cancellationToken);
            await CreateCategoryIndexes(cancellationToken);
            await CreatePomodoroIndexes(cancellationToken);
            await CreatePromptIndexes(cancellationToken);
            await CreateAuditIndexes(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB index initialization failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task CreateUserIndexes(CancellationToken cancellationToken)
    {
        var users = _database.GetCollection<User>("Users");
        var indexes = new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_users_email" })
        };
        return users.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private Task CreateHabitIndexes(CancellationToken cancellationToken)
    {
        var habits = _database.GetCollection<Habit>("Habits");
        var indexes = new[]
        {
            new CreateIndexModel<Habit>(
                Builders<Habit>.IndexKeys.Ascending(h => h.UserId).Ascending(h => h.IsArchived).Descending(h => h.CreatedAt),
                new CreateIndexOptions { Name = "ix_habits_user_archived_created" }),
            new CreateIndexModel<Habit>(
                Builders<Habit>.IndexKeys.Ascending(h => h.UserId).Ascending(h => h.Category),
                new CreateIndexOptions { Name = "ix_habits_user_category" })
        };
        return habits.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private Task CreateEntryIndexes(CancellationToken cancellationToken)
    {
        var entries = _database.GetCollection<HabitEntry>("HabitEntries");
        var indexes = new[]
        {
            new CreateIndexModel<HabitEntry>(
                Builders<HabitEntry>.IndexKeys.Ascending(e => e.UserId).Ascending(e => e.HabitId).Ascending(e => e.Date),
                new CreateIndexOptions { Unique = true, Name = "ux_entries_user_habit_date" }),
            new CreateIndexModel<HabitEntry>(
                Builders<HabitEntry>.IndexKeys.Ascending(e => e.UserId).Ascending(e => e.Date),
                new CreateIndexOptions { Name = "ix_entries_user_date" })
        };
        return entries.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private Task CreateCategoryIndexes(CancellationToken cancellationToken)
    {
        var categories = _database.GetCollection<HabitCategory>("HabitCategories");
        var indexes = new[]
        {
            new CreateIndexModel<HabitCategory>(
                Builders<HabitCategory>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.Name),
                new CreateIndexOptions { Unique = true, Name = "ux_categories_user_name" })
        };
        return categories.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private Task CreatePomodoroIndexes(CancellationToken cancellationToken)
    {
        var sessions = _database.GetCollection<PomodoroSession>("PomodoroSessions");
        var indexes = new[]
        {
            new CreateIndexModel<PomodoroSession>(
                Builders<PomodoroSession>.IndexKeys.Ascending(s => s.UserId).Descending(s => s.StartedAt),
                new CreateIndexOptions { Name = "ix_pomodoro_user_started" }),
            new CreateIndexModel<PomodoroSession>(
                Builders<PomodoroSession>.IndexKeys.Ascending(s => s.UserId).Ascending(s => s.HabitId).Descending(s => s.StartedAt),
                new CreateIndexOptions { Name = "ix_pomodoro_user_habit_started" })
        };
        return sessions.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private Task CreatePromptIndexes(CancellationToken cancellationToken)
    {
        var prompts = _database.GetCollection<ActiveLearningPrompt>("ActiveLearningPrompts");
        var indexes = new[]
        {
            new CreateIndexModel<ActiveLearningPrompt>(
                Builders<ActiveLearningPrompt>.IndexKeys.Ascending(p => p.UserId).Ascending(p => p.HabitId).Descending(p => p.ShownAt),
                new CreateIndexOptions { Name = "ix_prompts_user_habit_shown" })
        };
        return prompts.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private Task CreateAuditIndexes(CancellationToken cancellationToken)
    {
        var auditLogs = _database.GetCollection<AuditLog>("AuditLogs");
        var indexes = new[]
        {
            new CreateIndexModel<AuditLog>(
                Builders<AuditLog>.IndexKeys.Ascending(a => a.UserId).Descending(a => a.TimestampUtc),
                new CreateIndexOptions { Name = "ix_audit_user_timestamp" })
        };
        return auditLogs.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
