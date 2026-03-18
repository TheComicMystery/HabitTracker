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
    [LoadColumn(3)] public float Momentum { get; set; }
    [LoadColumn(4)] public float FatigueScore { get; set; }
    [LoadColumn(5)] public float StreakVolatility { get; set; }
    [LoadColumn(6)] public float RecentSuccessRate { get; set; }
    [LoadColumn(7)] public float UserOverallRate { get; set; }
    [LoadColumn(8)] public float ContextDriftScore { get; set; }
    [LoadColumn(9)] public float RecoverySpeed { get; set; }
    [LoadColumn(10)] public float LogHabitAge { get; set; }
    [LoadColumn(11)] public string MaturityBucket { get; set; } = string.Empty;
    [LoadColumn(12)] public bool Label { get; set; }
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
            float prevStreak = CalculateStreakForDate(entries, habit.Id!, entry.Date.AddDays(-7));
            float momentum = streak - prevStreak;

            float fatigue = CalculateFatigue(entries, habit.UserId, entry.Date);
            float volatility = CalculateStreakVolatility(entries, habit.Id!, entry.Date);
            
            float recentRate = CalculateRecentRate(entries, habit.Id!, entry.Date, 14);
            bool isWeekend = entry.Date.DayOfWeek == DayOfWeek.Saturday || entry.Date.DayOfWeek == DayOfWeek.Sunday;

            float contextDrift = CalculateContextDrift(entries, habit, entry.Date);
            float recoverySpeed = CalculateRecoverySpeed(entries, habit, entry.Date);
            var ageFeatures = CalculateAgeFeatures(habit.StartDate, entry.Date);

            trainingData.Add(new HabitModelInput
            {
                IsWeekend = isWeekend ? 1f : 0f,
                TargetCount = habit.TargetCount,
                CurrentStreak = streak,
                Momentum = momentum,
                FatigueScore = fatigue,
                StreakVolatility = volatility,
                RecentSuccessRate = recentRate,
                UserOverallRate = _userOverallRates.ContainsKey(entry.UserId) ? _userOverallRates[entry.UserId] : 0.5f,
                ContextDriftScore = contextDrift,
                RecoverySpeed = recoverySpeed,
                LogHabitAge = ageFeatures.LogAge,
                MaturityBucket = ageFeatures.Bucket,
                Label = entry.IsFullyCompleted
            });
        }

        if (trainingData.Count < 20) return;

        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        var pipeline = _mlContext.Transforms.Categorical.OneHotEncoding("MaturityBucketEncoded", "MaturityBucket")
            .Append(_mlContext.Transforms.Concatenate("Features", 
                "IsWeekend", "TargetCount", "CurrentStreak", "Momentum", "FatigueScore", "StreakVolatility", "RecentSuccessRate", "UserOverallRate", "ContextDriftScore", "RecoverySpeed", "LogHabitAge", "MaturityBucketEncoded"))
            .Append(_mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: "Label", 
                featureColumnName: "Features",
                numberOfLeaves: 31,
                learningRate: 0.05,
                numberOfIterations: 200
            ));

        _model = pipeline.Fit(dataView);
        _isTrained = true;
    }

    public async Task<double> PredictSuccessProbabilityAsync(Habit habit)
    {
        var (prob, _) = await PredictWithShapAsync(habit);
        return prob;
    }

    public async Task<(double Probability, string Explanation)> PredictWithShapAsync(Habit habit)
    {
        if (!_isTrained) await TrainModelAsync();
        if (_model == null) return (0, string.Empty);

        var predictionEngine = _mlContext.Model.CreatePredictionEngine<HabitModelInput, HabitModelOutput>(_model);

        var userId = habit.UserId;
        var today = DateTime.UtcNow.Date;
        
        var habitHistory = await _entriesCollection.Find(e => e.HabitId == habit.Id).ToListAsync();
        var allUserHistory = await _entriesCollection.Find(e => e.UserId == habit.UserId).ToListAsync();

        float currentStreak = CalculateStreakForDate(habitHistory, habit.Id!, today);
        float prevStreak = CalculateStreakForDate(habitHistory, habit.Id!, today.AddDays(-7));
        float momentum = currentStreak - prevStreak;

        float fatigue = CalculateFatigue(allUserHistory, userId, today);
        float volatility = CalculateStreakVolatility(habitHistory, habit.Id!, today);
        
        float recentRate = CalculateRecentRate(habitHistory, habit.Id!, today, 14);
        
        if (!habitHistory.Any())
        {
            recentRate = _userOverallRates.ContainsKey(userId) ? _userOverallRates[userId] : 0.5f;
        }

        float contextDrift = CalculateContextDrift(habitHistory, habit, today);
        float recoverySpeed = CalculateRecoverySpeed(habitHistory, habit, today);
        var ageFeatures = CalculateAgeFeatures(habit.StartDate, today);

        bool isWeekend = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday;

        var input = new HabitModelInput
        {
            IsWeekend = isWeekend ? 1f : 0f,
            TargetCount = habit.TargetCount,
            CurrentStreak = currentStreak,
            Momentum = momentum,
            FatigueScore = fatigue,
            StreakVolatility = volatility,
            RecentSuccessRate = recentRate,
            UserOverallRate = _userOverallRates.ContainsKey(userId) ? _userOverallRates[userId] : 0.5f,
            ContextDriftScore = contextDrift,
            RecoverySpeed = recoverySpeed,
            LogHabitAge = ageFeatures.LogAge,
            MaturityBucket = ageFeatures.Bucket
        };

        var result = predictionEngine.Predict(input);
        double prob = Math.Round(result.Probability * 100, 1);

        string explanation = GenerateNaturalExplanation(input, prob);

        return (prob, explanation);
    }

    private string GenerateNaturalExplanation(HabitModelInput f, double prob)
    {
        var pos = new List<string>();
        var neg = new List<string>();

        if (f.IsWeekend == 1 && f.RecentSuccessRate < 0.5) neg.Add("вихідний день 🔴");
        if (f.FatigueScore > 10) neg.Add("висока загальна втома 🔴");
        if (f.ContextDriftScore < -0.2f) neg.Add("недавній збій режиму 🔴");
        if (f.MaturityBucket == "Week1" || f.MaturityBucket == "Week2") neg.Add("звичка ще крихка 🟡");
        if (f.RecoverySpeed > 3f) neg.Add("важке відновлення після зривів 🟡");

        if (f.CurrentStreak > 5) pos.Add("гарний стрік 🟢");
        if (f.Momentum > 1) pos.Add("зростаючий моментум 🟢");
        if (f.LogHabitAge > 4) pos.Add("звичка вже закріплена 🟢");

        string condition = prob < 45 ? "низька" : prob > 70 ? "висока" : "середня";
        
        string text = $"Ймовірність {condition} ({prob}%), бо: ";
        
        if (neg.Any()) text += string.Join(", ", neg.Take(2));
        
        if (pos.Any()) 
        {
            if (neg.Any()) text += ", але ";
            text += string.Join(" та ", pos.Take(2)) + " " + (neg.Any() ? "трохи компенсують." : "дають впевненість.");
        }
        
        if (!pos.Any() && !neg.Any()) return $"Ймовірність {prob}%, стабільний ритм ⚪";

        return text;
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
            .Where(e => e.HabitId == habitId && e.IsFullyCompleted && e.Date < targetDate)
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

    private float CalculateFatigue(List<HabitEntry> allEntries, string userId, DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-3);
        return allEntries
            .Where(e => e.UserId == userId && e.Date >= startDate && e.Date < targetDate && e.IsFullyCompleted)
            .Count();
    }

    private float CalculateStreakVolatility(List<HabitEntry> habitHistory, string habitId, DateTime targetDate)
    {
        var completedEntries = habitHistory
            .Where(e => e.HabitId == habitId && e.IsFullyCompleted && e.Date < targetDate)
            .OrderBy(e => e.Date)
            .ToList();

        if (completedEntries.Count < 2) return 0;

        var streaks = new List<float>();
        int currentStreak = 0;
        DateTime? lastDate = null;

        foreach (var entry in completedEntries)
        {
            if (lastDate == null || (entry.Date.Date - lastDate.Value.Date).TotalDays == 1)
            {
                currentStreak++;
            }
            else
            {
                streaks.Add(currentStreak);
                currentStreak = 1;
            }
            lastDate = entry.Date;
        }
        streaks.Add(currentStreak); 

        if (streaks.Count < 2) return 0;

        float avg = streaks.Average();
        float sumSq = streaks.Sum(s => (s - avg) * (s - avg));
        return (float)Math.Sqrt(sumSq / streaks.Count);
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

    private float CalculateContextDrift(List<HabitEntry> habitHistory, Habit habit, DateTime targetDate)
    {
        var recentStart = targetDate.Date.AddDays(-14);
        var prevStart = targetDate.Date.AddDays(-44);
        
        var entriesDict = habitHistory.Where(e => e.HabitId == habit.Id).ToDictionary(e => e.Date.Date, e => e.IsFullyCompleted);
        
        int recentExpected = 0, recentCompleted = 0;
        int prevExpected = 0, prevCompleted = 0;

        for (var d = prevStart; d < targetDate.Date; d = d.AddDays(1))
        {
            if (d < habit.StartDate.Date) continue;
            
            if (habit.ActiveDays.Contains(d.DayOfWeek))
            {
                bool completed = entriesDict.ContainsKey(d) && entriesDict[d];
                if (d >= recentStart)
                {
                    recentExpected++;
                    if (completed) recentCompleted++;
                }
                else
                {
                    prevExpected++;
                    if (completed) prevCompleted++;
                }
            }
        }

        float recentRate = recentExpected > 0 ? (float)recentCompleted / recentExpected : 0f;
        float prevRate = prevExpected > 0 ? (float)prevCompleted / prevExpected : recentRate;

        return recentRate - prevRate;
    }

    private float CalculateRecoverySpeed(List<HabitEntry> habitHistory, Habit habit, DateTime targetDate)
    {
        var activeDays = habit.ActiveDays;
        var start = habit.StartDate.Date;
        var end = targetDate.Date;
        
        if (start >= end) return 0f;

        var entriesDict = habitHistory.Where(e => e.HabitId == habit.Id).ToDictionary(e => e.Date.Date, e => e.IsFullyCompleted);
        
        List<int> recoveryTimes = new List<int>();
        int currentStreak = 0;
        int daysSinceFailure = 0;
        bool inFailureState = false;

        for (var d = start; d < end; d = d.AddDays(1))
        {
            bool isTargetDay = activeDays.Contains(d.DayOfWeek);
            bool isCompleted = entriesDict.ContainsKey(d) && entriesDict[d];

            if (isTargetDay)
            {
                if (isCompleted)
                {
                    currentStreak++;
                    if (inFailureState)
                    {
                        daysSinceFailure++;
                        if (currentStreak >= 3)
                        {
                            recoveryTimes.Add(daysSinceFailure);
                            inFailureState = false;
                            daysSinceFailure = 0;
                        }
                    }
                }
                else
                {
                    currentStreak = 0;
                    if (!inFailureState)
                    {
                        inFailureState = true;
                        daysSinceFailure = 1;
                    }
                    else
                    {
                        daysSinceFailure++;
                    }
                }
            }
            else
            {
                if (inFailureState) daysSinceFailure++;
            }
        }

        return recoveryTimes.Any() ? (float)recoveryTimes.Average() : 0f;
    }

    private (float LogAge, string Bucket) CalculateAgeFeatures(DateTime startDate, DateTime targetDate)
    {
        var ageDays = (targetDate - startDate).TotalDays;
        if (ageDays < 0) ageDays = 0;

        float logAge = (float)Math.Log(ageDays + 1);
        
        string bucket;
        if (ageDays <= 7) bucket = "Week1";
        else if (ageDays <= 14) bucket = "Week2";
        else if (ageDays <= 30) bucket = "Month1";
        else if (ageDays <= 90) bucket = "Month3";
        else bucket = "Established";

        return (logAge, bucket);
    }
}