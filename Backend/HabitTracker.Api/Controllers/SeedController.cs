using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Config;
using Microsoft.Extensions.Options;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IMongoCollection<Habit> _habitsCollection;
    private readonly IMongoCollection<HabitEntry> _habitEntriesCollection;

    public SeedController(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _usersCollection = mongoDatabase.GetCollection<User>("Users");
        _habitsCollection = mongoDatabase.GetCollection<Habit>("Habits");
        _habitEntriesCollection = mongoDatabase.GetCollection<HabitEntry>("HabitEntries");
    }

    [HttpPost("generate-demo")]
    public async Task<IActionResult> GenerateDemoData()
    {
        var demoEmail = "demo@habitknot.com";
        var user = await _usersCollection.Find(u => u.Email == demoEmail).FirstOrDefaultAsync();

        if (user != null)
        {
            await _habitsCollection.DeleteManyAsync(h => h.UserId == user.Id);
            await _habitEntriesCollection.DeleteManyAsync(e => e.UserId == user.Id);
            await _usersCollection.DeleteOneAsync(u => u.Id == user.Id);
        }

        user = new User
        {
            Username = "Олексій",
            Email = demoEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo123")
        };
        await _usersCollection.InsertOneAsync(user);
        var userId = user.Id;

        // 🟢 ДИНАМІЧНІ ДАТИ: Від сьогоднішнього дня і на 3 місяці назад
        var endGenDate = DateTime.UtcNow.Date;
        var startGenDate = endGenDate.AddDays(-90); 
        var creationDate = startGenDate.AddDays(-1);

        var allDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var weekDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var weekEnd = new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
        var mwf = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var tts = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday };
        var testDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Sunday };
        var walkDays = new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Monday };
        var backDays = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Saturday, DayOfWeek.Thursday };

        var habits = new List<Habit>
        {
            new Habit { UserId = userId, StartDate = creationDate, Name = "Тест", Description = "Тестування", TargetCount = 2, Color = "#6366f1", ActiveDays = testDays, Icon = "📌" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Ранкова зарядка 15 хв", Description = "Легкі вправи", TargetCount = 1, Color = "#10b981", ActiveDays = allDays, Icon = "🧘" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Читання книги 30 хв", Description = "Перед сном", TargetCount = 1, Color = "#f59e0b", ActiveDays = allDays, Icon = "📚" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Прийом вітамінів", Description = "Комплекс", TargetCount = 2, Color = "#14b8a6", ActiveDays = allDays, Icon = "💊" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Вечірня прогулянка", Description = "Свіже повітря", TargetCount = 1, Color = "#8b5cf6", ActiveDays = walkDays, Icon = "🚶" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Малювання ескізів", Description = "Скетчинг", TargetCount = 3, Color = "#ec4899", ActiveDays = tts, Icon = "✏️" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Вправи для спини", Description = "Здорова спина", TargetCount = 1, Color = "#ef4444", ActiveDays = backDays, Icon = "💪" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Англійська мова", Description = "Duolingo", TargetCount = 1, Color = "#3b82f6", ActiveDays = mwf, Icon = "🇬🇧" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Коротка розминка", Description = "Перерва в роботі", TargetCount = 2, Color = "#06b6d4", ActiveDays = allDays, Icon = "🤸" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Контроль бюджету", Description = "Записати витрати", TargetCount = 1, Color = "#64748b", ActiveDays = allDays, Icon = "💰" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Не їсти солодкого", Description = "Детокс", TargetCount = 1, Color = "#f43f5e", ActiveDays = allDays, Icon = "🚫" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Пити воду протягом дня", Description = "8 склянок у день", TargetCount = 8, Color = "#0ea5e9", ActiveDays = allDays, Icon = "💧" },
            new Habit { UserId = userId, StartDate = creationDate, Name = "Старий проект", Description = "Архівовано", TargetCount = 5, Color = "#78716c", ActiveDays = weekEnd, IsArchived = true, Icon = "💻" }
        };

        await _habitsCollection.InsertManyAsync(habits);

        var random = new Random();
        var entries = new List<HabitEntry>();
        var failureReasons = new[] { "Втома / Стрес", "Брак часу", "Лінь / Прокрастинація", "Забув(ла)", "Хвороба", "Свято / Гості" };

        for (var day = startGenDate; day <= endGenDate; day = day.AddDays(1))
        {
            foreach (var habit in habits)
            {
                if (!habit.ActiveDays.Contains(day.DayOfWeek)) continue;
                if (habit.IsArchived && day > endGenDate.AddDays(-20)) continue; // Старий проєкт закинуто 20 днів тому

                double successChance = 0.96; 
                bool allowPartial = false;

                switch (habit.Name)
                {
                    case "Вправи для спини":
                        successChance = 0.65;
                        break;
                    case "Вечірня прогулянка":
                        successChance = 0.78;
                        break;
                    case "Пити воду протягом дня": 
                        successChance = 0.40; 
                        allowPartial = true; 
                        break;
                    case "Малювання ескізів":
                        successChance = 0.50; 
                        allowPartial = true;
                        break;
                    case "Читання книги 30 хв":
                        successChance = 0.70;
                        break;
                    case "Не їсти солодкого":
                        successChance = 0.85;
                        break;
                    default:
                        successChance = 0.96;
                        break;
                }

                // 🟢 ДОДАНО ІМІТАЦІЮ ЗРИВІВ ДЛЯ АКТИВНОГО НАВЧАННЯ (Active Learning)
                if (random.NextDouble() > successChance && !allowPartial) 
                {
                    // У 25% випадків пропуску користувач свідомо залишає причину зриву (через нове модальне вікно)
                    if (random.NextDouble() < 0.25)
                    {
                        entries.Add(new HabitEntry
                        {
                            HabitId = habit.Id,
                            UserId = userId,
                            Date = day,
                            CompletedCount = 0,
                            IsFullyCompleted = false,
                            FailureReason = failureReasons[random.Next(failureReasons.Length)],
                            // Імітуємо ситуацію, коли ШІ запитав користувача вранці, і той поставив низьку впевненість
                            ConfidenceScore = random.NextDouble() < 0.3 ? random.Next(1, 5) : null, 
                            LastModified = DateTime.UtcNow
                        });
                    }
                    continue;
                }

                int completed = 0;

                if (habit.TargetCount == 1)
                {
                    completed = 1;
                }
                else
                {
                    double effort = random.NextDouble();
                    
                    if (habit.Name == "Пити воду протягом дня")
                    {
                        if (effort > 0.7) completed = 8;        
                        else if (effort > 0.2) completed = random.Next(4, 8); 
                        else completed = random.Next(1, 4);     
                    }
                    else if (allowPartial)
                    {
                        completed = (effort > 0.5) ? habit.TargetCount : (int)Math.Ceiling(habit.TargetCount / 2.0);
                    }
                    else
                    {
                        completed = habit.TargetCount;
                    }
                }

                if (completed > 0)
                {
                    entries.Add(new HabitEntry
                    {
                        HabitId = habit.Id,
                        UserId = userId,
                        Date = day,
                        CompletedCount = completed,
                        IsFullyCompleted = completed >= habit.TargetCount,
                        // Імітуємо ситуацію, коли ШІ запитав користувача, і той поставив високу впевненість перед виконанням
                        ConfidenceScore = random.NextDouble() < 0.15 ? random.Next(7, 11) : null,
                        LastModified = DateTime.UtcNow
                    });
                }
            }
        }

        if (entries.Any())
        {
            await _habitEntriesCollection.InsertManyAsync(entries);
        }

        return Ok(new { message = "Demo data successfully generated for LightGBM!", user = "Олексій", totalEntries = entries.Count });
    }
}