using Microsoft.ML;
using Microsoft.ML.Data;
using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Config;
using Microsoft.Extensions.Options;

namespace HabitTracker.Api.Services;

public class HabitModelInput
{
    [LoadColumn(0)] public float IsWeekend { get; set; }
    [LoadColumn(1)] public float TargetCount { get; set; }
    [LoadColumn(2)] public float CurrentStreak { get; set; }
    [LoadColumn(3)] public float RecentSuccessRate { get; set; }
    [LoadColumn(4)] public float UserOverallRate { get; set; }
    [LoadColumn(5)] public bool Label { get; set; }
}

public class HabitModelOutput
{
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}

public class HabitPredictionService
{
    private readonly IMongoCollection<Habit> _habitsCollection;
    private readonly IMongoCollection<HabitEntry> _entriesCollection;
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private bool _isTrained = false;

    private Dictionary<string, float> _userOverallRates = new();

    public HabitPredictionService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _habitsCollection = mongoDatabase.GetCollection<Habit>("Habits");
        _entriesCollection = mongoDatabase.GetCollection<HabitEntry>("HabitEntries");
        _mlContext = new MLContext(seed: 1337); 
    }

    public async Task TrainModelAsync()
    {
        var habits = await _habitsCollection.Find(_ => true).ToListAsync();
        var entries = await _entriesCollection.Find(_ => true).ToListAsync();

        if (!entries.Any()) return;

        var trainingData = new List<HabitModelInput>();

        CalculateUserRates(habits, entries);

        foreach (var entry in entries)
        {
            var habit = habits.FirstOrDefault(h => h.Id == entry.HabitId);
            if (habit == null) continue;

            float streak = CalculateStreakForDate(entries, habit.Id!, entry.Date);
            float recentRate = CalculateRecentRate(entries, habit.Id!, entry.Date, 14);
            bool isWeekend = entry.Date.DayOfWeek == DayOfWeek.Saturday || entry.Date.DayOfWeek == DayOfWeek.Sunday;

            trainingData.Add(new HabitModelInput
            {
                IsWeekend = isWeekend ? 1f : 0f,
                TargetCount = habit.TargetCount,
                CurrentStreak = streak,
                RecentSuccessRate = recentRate,
                UserOverallRate = _userOverallRates.ContainsKey(entry.UserId) ? _userOverallRates[entry.UserId] : 0.5f,
                Label = entry.IsFullyCompleted
            });
        }

        if (trainingData.Count < 20) return;

        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        var pipeline = _mlContext.Transforms.Concatenate("Features", 
                "IsWeekend", "TargetCount", "CurrentStreak", "RecentSuccessRate", "UserOverallRate")
            .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label", 
                featureColumnName: "Features",
                numberOfLeaves: 10, 
                numberOfTrees: 20
            ));

        _model = pipeline.Fit(dataView);
        _isTrained = true;
    }

    public async Task<double> PredictSuccessProbabilityAsync(Habit habit)
    {
        if (!_isTrained) await TrainModelAsync();
        if (_model == null) return 0;

        var predictionEngine = _mlContext.Model.CreatePredictionEngine<HabitModelInput, HabitModelOutput>(_model);

        var userId = habit.UserId;
        var today = DateTime.UtcNow.Date;
        
        var history = await _entriesCollection.Find(e => e.HabitId == habit.Id).ToListAsync();

        float currentStreak = CalculateStreakForDate(history, habit.Id!, today);
        float recentRate = CalculateRecentRate(history, habit.Id!, today, 14);
        
        if (!history.Any())
        {
            recentRate = _userOverallRates.ContainsKey(userId) ? _userOverallRates[userId] : 0.5f;
        }

        bool isWeekend = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday;

        var input = new HabitModelInput
        {
            IsWeekend = isWeekend ? 1f : 0f,
            TargetCount = habit.TargetCount,
            CurrentStreak = currentStreak,
            RecentSuccessRate = recentRate,
            UserOverallRate = _userOverallRates.ContainsKey(userId) ? _userOverallRates[userId] : 0.5f
        };

        var result = predictionEngine.Predict(input);
        
        return Math.Round(result.Probability * 100, 1);
    }

    private void CalculateUserRates(List<Habit> habits, List<HabitEntry> entries)
    {
        var userEntries = entries.GroupBy(e => e.UserId);
        foreach (var userGroup in userEntries)
        {
            var totalTargets = 0;
            var totalCompleted = 0;
            foreach (var entry in userGroup)
            {
                var habit = habits.FirstOrDefault(h => h.Id == entry.HabitId);
                if (habit == null) continue;
                totalTargets += habit.TargetCount;
                totalCompleted += entry.CompletedCount;
            }
            float rate = totalTargets > 0 ? (float)totalCompleted / totalTargets : 0;
            _userOverallRates[userGroup.Key] = rate;
        }
    }

    private float CalculateStreakForDate(List<HabitEntry> habitHistory, string habitId, DateTime targetDate)
    {
        int streak = 0;
        var checkDate = targetDate.Date.AddDays(-1);
        
        var completedDates = habitHistory
            .Where(e => e.HabitId == habitId && e.IsFullyCompleted)
            .Select(e => e.Date.Date)
            .ToHashSet();

        while (true)
        {
            if (completedDates.Contains(checkDate))
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }
        return (float)streak;
    }

    private float CalculateRecentRate(List<HabitEntry> habitHistory, string habitId, DateTime targetDate, int daysLookback)
    {
        var startDate = targetDate.AddDays(-daysLookback);
        
        var recentEntries = habitHistory
            .Where(e => e.HabitId == habitId && e.Date >= startDate && e.Date < targetDate)
            .ToList();

        if (!recentEntries.Any()) return 0.5f; 

        int successes = recentEntries.Count(e => e.IsFullyCompleted);
        return (float)successes / recentEntries.Count;
    }
}