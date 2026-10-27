using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Services;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private const string DemoEmail = "demo@habitknot.com";
    private const string DemoPassword = "demo123";

    private readonly IMongoCollection<User> _usersCollection;
    private readonly IMongoCollection<Habit> _habitsCollection;
    private readonly IMongoCollection<HabitEntry> _habitEntriesCollection;
    private readonly HabitPredictionService _predictionService;
    private readonly IWebHostEnvironment _environment;

    public SeedController(IMongoDatabase mongoDatabase, HabitPredictionService predictionService, IWebHostEnvironment environment)
    {
        _usersCollection = mongoDatabase.GetCollection<User>("Users");
        _habitsCollection = mongoDatabase.GetCollection<Habit>("Habits");
        _habitEntriesCollection = mongoDatabase.GetCollection<HabitEntry>("HabitEntries");
        _predictionService = predictionService;
        _environment = environment;
    }

    [HttpPost("train-synthetic-corpus")]
    public async Task<IActionResult> TrainSyntheticCorpus(
        [FromQuery] int users = 500,
        [FromQuery] int days = 180,
        [FromQuery] int maxTrainingRows = 60000,
        [FromQuery] int validationRows = 3000)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var report = await _predictionService.TrainSyntheticCorpusAsync(users, days, maxTrainingRows, validationRows);
        return Ok(report);
    }

    [HttpPost("generate-demo")]
    public async Task<IActionResult> GenerateDemoData()
    {
        if (!_environment.IsDevelopment()) return NotFound();

        var user = await _usersCollection.Find(u => u.Email == DemoEmail).FirstOrDefaultAsync();

        if (user != null)
        {
            await _habitsCollection.DeleteManyAsync(h => h.UserId == user.Id);
            await _habitEntriesCollection.DeleteManyAsync(e => e.UserId == user.Id);
            await _usersCollection.UpdateOneAsync(u => u.Id == user.Id, BuildDemoUserUpdate());
        }
        else
        {
            user = CreateDemoUser();
            await _usersCollection.InsertOneAsync(user);
        }

        var userId = user.Id!;
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-119);
        var createdAt = startDate.AddDays(-3);

        var allDays = Enum.GetValues<DayOfWeek>().ToList();
        var weekend = new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
        var mondayWednesdayFriday = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var tuesdayThursdaySaturday = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday };

        var habits = new List<Habit>
        {
            CreateHabit(userId, createdAt, "Тест", "Тестування", "Робота", 2, "#6366f1", "📌", new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Sunday }),
            CreateHabit(userId, createdAt, "Ранкова зарядка 15 хв", "Легкі вправи", "Здоров'я", 1, "#10b981", "🧘", allDays),
            CreateHabit(userId, createdAt, "Читання книги 30 хв", "Перед сном", "Навчання", 1, "#f59e0b", "📚", allDays),
            CreateHabit(userId, createdAt, "Прийом вітамінів", "Комплекс", "Здоров'я", 2, "#14b8a6", "💊", allDays),
            CreateHabit(userId, createdAt, "Вечірня прогулянка", "Свіже повітря", "Здоров'я", 1, "#8b5cf6", "🚶", new() { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday }),
            CreateHabit(userId, createdAt, "Малювання ескізів", "Скетчинг", "Творчість", 3, "#ec4899", "✏️", tuesdayThursdaySaturday),
            CreateHabit(userId, createdAt, "Вправи для спини", "Здорова спина", "Здоров'я", 1, "#ef4444", "💪", new() { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday }),
            CreateHabit(userId, createdAt, "Англійська мова", "Коротке тренування", "Навчання", 1, "#3b82f6", "🇬🇧", mondayWednesdayFriday),
            CreateHabit(userId, createdAt, "Коротка розминка", "Перерва в роботі", "Здоров'я", 2, "#06b6d4", "🤸", allDays),
            CreateHabit(userId, createdAt, "Контроль бюджету", "Записати витрати", "Фінанси", 1, "#64748b", "💰", allDays),
            CreateHabit(userId, createdAt, "Не їсти солодкого", "Детокс", "Здоров'я", 1, "#f43f5e", "🚫", allDays),
            CreateHabit(userId, createdAt, "Пити воду протягом дня", "8 склянок у день", "Здоров'я", 8, "#0ea5e9", "💧", allDays),
            CreateHabit(userId, createdAt, "Старий проєкт", "Архівовано", "Робота", 5, "#78716c", "💻", weekend, true)
        };

        await _habitsCollection.InsertManyAsync(habits);

        var random = new Random(20260623);
        var entries = GenerateDemoEntries(habits, userId, startDate, endDate, random);

        if (entries.Count > 0)
        {
            await _habitEntriesCollection.InsertManyAsync(entries);
        }

        _predictionService.InvalidateUserPredictionCache(userId);

        return Ok(new
        {
            message = "Демодані успішно згенеровано.",
            user = DemoEmail,
            password = DemoPassword,
            startDate,
            endDate,
            totalHabits = habits.Count,
            activeHabits = habits.Count(h => !h.IsArchived),
            totalEntries = entries.Count,
            fullCompletions = entries.Count(e => e.IsFullyCompleted),
            recentEntries = entries.Count(e => e.Date >= endDate.AddDays(-14))
        });
    }

    private static User CreateDemoUser() =>
        new()
        {
            Username = "Demo User",
            Email = DemoEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
            OnboardingCompleted = true,
            SubjectiveDifficulty = 4,
            OverallDisciplineSelf = 7,
            WeekendVulnerability = 2,
            PreferredTimeOfDay = "morning",
            InitialFailureReason = "Втома / стрес",
            OnboardingCompletedAt = DateTime.UtcNow
        };

    private static UpdateDefinition<User> BuildDemoUserUpdate() =>
        Builders<User>.Update
            .Set(u => u.Username, "Demo User")
            .Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(DemoPassword))
            .Set(u => u.OnboardingCompleted, true)
            .Set(u => u.SubjectiveDifficulty, 4)
            .Set(u => u.OverallDisciplineSelf, 7)
            .Set(u => u.WeekendVulnerability, 2)
            .Set(u => u.PreferredTimeOfDay, "morning")
            .Set(u => u.InitialFailureReason, "Втома / стрес")
            .Set(u => u.OnboardingCompletedAt, DateTime.UtcNow);

    private static List<HabitEntry> GenerateDemoEntries(
        List<Habit> habits,
        string userId,
        DateTime startDate,
        DateTime endDate,
        Random random)
    {
        var entries = new List<HabitEntry>();

        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            foreach (var habit in habits)
            {
                if (!habit.ActiveDays.Contains(day.DayOfWeek)) continue;
                if (habit.IsArchived && day > endDate.AddDays(-28)) continue;

                var daysFromEnd = (endDate - day).Days;
                var probability = GetDemoSuccessChance(habit, day, daysFromEnd);
                var completed = ShouldForceRecentCompletion(habit, daysFromEnd)
                    ? habit.TargetCount
                    : CalculateCompletedCount(habit, random, probability, daysFromEnd);

                if (completed <= 0)
                {
                    entries.Add(CreateEntry(
                        habit,
                        userId,
                        day,
                        0,
                        PickFailureReason(habit, random, day),
                        GenerateConfidenceScore(random, probability, completed)));
                    continue;
                }

                entries.Add(CreateEntry(
                    habit,
                    userId,
                    day,
                    completed,
                    completed >= habit.TargetCount ? null : PickFailureReason(habit, random, day),
                    GenerateConfidenceScore(random, probability, completed)));
            }
        }

        return entries;
    }

    private static Habit CreateHabit(
        string userId,
        DateTime createdAt,
        string name,
        string description,
        string category,
        int targetCount,
        string color,
        string icon,
        List<DayOfWeek> activeDays,
        bool archived = false) =>
        new()
        {
            UserId = userId,
            StartDate = createdAt,
            CreatedAt = createdAt,
            Name = name,
            Description = description,
            Category = category,
            TargetCount = targetCount,
            Color = color,
            ActiveDays = activeDays,
            Icon = icon,
            IsArchived = archived
        };

    private static double GetDemoSuccessChance(Habit habit, DateTime day, int daysFromEnd)
    {
        var chance = habit.Name switch
        {
            "Прийом вітамінів" => 0.91,
            "Ранкова зарядка 15 хв" => 0.84,
            "Коротка розминка" => 0.82,
            "Контроль бюджету" => 0.80,
            "Читання книги 30 хв" => 0.78,
            "Тест" => 0.76,
            "Англійська мова" => 0.74,
            "Пити воду протягом дня" => 0.82,
            "Вечірня прогулянка" => 0.72,
            "Вправи для спини" => 0.69,
            "Не їсти солодкого" => 0.66,
            "Малювання ескізів" => 0.76,
            "Старий проєкт" => 0.42,
            _ => 0.75
        };

        if (daysFromEnd <= 21) chance += habit.Name switch
        {
            "Прийом вітамінів" or "Ранкова зарядка 15 хв" or "Читання книги 30 хв" => 0.10,
            "Пити воду протягом дня" or "Тест" or "Коротка розминка" => 0.10,
            "Малювання ескізів" or "Не їсти солодкого" => 0.09,
            _ => 0.06
        };

        if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            chance -= habit.Name switch
            {
                "Не їсти солодкого" => 0.14,
                "Англійська мова" => 0.08,
                "Контроль бюджету" => 0.06,
                _ => 0.03
            };
        }

        if (day >= DateTime.UtcNow.Date.AddDays(-55) && day <= DateTime.UtcNow.Date.AddDays(-43))
        {
            chance -= habit.Name switch
            {
                "Вечірня прогулянка" or "Малювання ескізів" or "Не їсти солодкого" => 0.18,
                _ => 0.08
            };
        }

        return Math.Clamp(chance, 0.38, 0.97);
    }

    private static bool ShouldForceRecentCompletion(Habit habit, int daysFromEnd) =>
        daysFromEnd <= (habit.Name == "Малювання ескізів" ? 14 : 6) &&
        habit.Name is "Ранкова зарядка 15 хв"
            or "Прийом вітамінів"
            or "Читання книги 30 хв"
            or "Пити воду протягом дня"
            or "Коротка розминка"
            or "Тест"
            or "Малювання ескізів";

    private static int CalculateCompletedCount(Habit habit, Random random, double probability, int daysFromEnd)
    {
        var success = random.NextDouble() < probability;
        if (habit.TargetCount == 1) return success ? 1 : 0;

        if (habit.Name == "Пити воду протягом дня")
        {
            if (success) return daysFromEnd <= 21 || random.NextDouble() > 0.15 ? 8 : random.Next(6, 8);
            return random.NextDouble() < 0.55 ? random.Next(4, 8) : 0;
        }

        if (habit.Name == "Малювання ескізів")
        {
            if (success) return random.NextDouble() > 0.20 ? 3 : 2;
            return random.NextDouble() < 0.50 ? random.Next(1, 3) : 0;
        }

        if (success) return habit.TargetCount;
        return random.NextDouble() < 0.32 ? Math.Max(1, habit.TargetCount - 1) : 0;
    }

    private static string PickFailureReason(Habit habit, Random random, DateTime day)
    {
        if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday && random.NextDouble() < 0.45)
        {
            return "Свято / гості";
        }

        return habit.Name switch
        {
            "Не їсти солодкого" => random.NextDouble() < 0.55 ? "Лінь / прокрастинація" : "Втома / стрес",
            "Англійська мова" or "Контроль бюджету" => random.NextDouble() < 0.60 ? "Брак часу" : "Забув(ла)",
            "Вечірня прогулянка" or "Вправи для спини" => random.NextDouble() < 0.50 ? "Втома / стрес" : "Хвороба",
            "Малювання ескізів" => random.NextDouble() < 0.55 ? "Брак часу" : "Лінь / прокрастинація",
            _ => random.NextDouble() < 0.50 ? "Втома / стрес" : "Забув(ла)"
        };
    }

    private static int? GenerateConfidenceScore(Random random, double probability, int completedCount)
    {
        if (completedCount <= 0 && random.NextDouble() < 0.55)
        {
            return random.Next(2, 6);
        }

        if (completedCount > 0 && random.NextDouble() < 0.18)
        {
            var expected = (int)Math.Round(probability * 10);
            return Math.Clamp(expected + random.Next(-1, 2), 6, 10);
        }

        return null;
    }

    private static HabitEntry CreateEntry(
        Habit habit,
        string userId,
        DateTime day,
        int completedCount,
        string? failureReason,
        int? confidenceScore)
    {
        var retroactive = day < DateTime.UtcNow.Date && habit.Name is "Пити воду протягом дня" or "Читання книги 30 хв";
        var lastModified = retroactive && day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday
            ? day.AddDays(1).AddHours(21)
            : day.AddHours(19);

        return new HabitEntry
        {
            HabitId = habit.Id!,
            UserId = userId,
            Date = day,
            CompletedCount = completedCount,
            IsFullyCompleted = completedCount >= habit.TargetCount,
            FailureReason = failureReason,
            ConfidenceScore = confidenceScore,
            LastModified = lastModified
        };
    }
}
