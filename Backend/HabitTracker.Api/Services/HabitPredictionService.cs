using Microsoft.ML;
using Microsoft.ML.Data;
using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

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
    [LoadColumn(12)] public float SynergyScore { get; set; }
    [LoadColumn(13)] public float IsHoliday { get; set; }
    [LoadColumn(14)] public float SinDayOfYear { get; set; }
    [LoadColumn(15)] public float CosDayOfYear { get; set; }
    [LoadColumn(16)] public float RetroactiveLogRatio { get; set; }
    [LoadColumn(17)] public float CueStrengthScore { get; set; }
    [LoadColumn(18)] public bool Label { get; set; }
}

public class HabitModelOutput
{
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}

public class HabitEnsemblePrediction
{
    public double Probability { get; set; }
    public Dictionary<string, float> ModelProbabilities { get; set; } = new();
    public List<ShapContribution> ShapContributions { get; set; } = new();
}

public class ExternalHabitEnsembleOutput
{
    public Dictionary<string, float> ModelProbabilities { get; set; } = new();
    public float? StackedProbability { get; set; }
    public List<ShapContribution> ShapContributions { get; set; } = new();
}

public class ExternalHabitEnsembleBatchOutput
{
    public List<ExternalHabitEnsembleOutput> Predictions { get; set; } = new();
}

public class ShapContribution
{
    public string Feature { get; set; } = string.Empty;
    public float Value { get; set; }
    public string Direction { get; set; } = string.Empty;
}

public class HabitPredictionService
{
    private readonly IMongoCollection<Habit> _habitsCollection;
    private readonly IMongoCollection<HabitEntry> _entriesCollection;
    private readonly MLContext _mlContext;
    private readonly IMemoryCache _memoryCache;
    private readonly object _trainingLock = new();
    private List<WeightedHabitModel> _ensembleModels = new();
    private readonly ConcurrentDictionary<string, long> _userCacheVersions = new();
    private string? _externalEnsembleModelPath;
    private bool _externalEnsembleReady = false;
    private bool _isTrained = false;
    private DateTime? _lastTrainedUtc;

    private Dictionary<string, float> _userOverallRates = new();

    private sealed record WeightedHabitModel(string Name, double Weight, ITransformer Model);

    private static readonly TimeSpan ModelTrainingTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan FeatureCacheTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan PredictionCacheTtl = TimeSpan.FromHours(2);

    public HabitPredictionService(IOptions<MongoDbSettings> mongoDbSettings, IMemoryCache memoryCache)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _habitsCollection = mongoDatabase.GetCollection<Habit>("Habits");
        _entriesCollection = mongoDatabase.GetCollection<HabitEntry>("HabitEntries");
        _mlContext = new MLContext(seed: 1337);
        _memoryCache = memoryCache;
    }

    public async Task TrainModelAsync()
    {
        if (_isTrained && _lastTrainedUtc.HasValue && DateTime.UtcNow - _lastTrainedUtc.Value < ModelTrainingTtl) return;

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

            float contextDrift = CalculateWassersteinDrift(entries, habit.Id!, entry.Date);
            float recoverySpeed = CalculateRecoverySpeed(entries, habit, entry.Date);
            var ageFeatures = CalculateAgeFeatures(habit.StartDate, entry.Date);

            float synergy = CalculateSynergy(entries, habit.Id!, habit.UserId, entry.Date);
            float isHoliday = IsUkrainianHoliday(entry.Date) ? 1f : 0f;

            float daysInYear = DateTime.IsLeapYear(entry.Date.Year) ? 366f : 365f;
            float sinDay = (float)Math.Sin(2 * Math.PI * entry.Date.DayOfYear / daysInYear);
            float cosDay = (float)Math.Cos(2 * Math.PI * entry.Date.DayOfYear / daysInYear);

            float retroRatio = CalculateRetroactiveRatio(entries, habit.Id!, entry.Date);
            float cueStrength = CalculateCueStrength(entries, habit.Id!, entry.Date);

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
                SynergyScore = synergy,
                IsHoliday = isHoliday,
                SinDayOfYear = sinDay,
                CosDayOfYear = cosDay,
                RetroactiveLogRatio = retroRatio,
                CueStrengthScore = cueStrength,
                Label = entry.IsFullyCompleted
            });
        }

        if (trainingData.Count < 20 || trainingData.Select(d => d.Label).Distinct().Count() < 2) return;

        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        var externalReady = await TryTrainExternalEnsembleAsync(trainingData);
        var trainedModels = new List<WeightedHabitModel>();

        TryAddTrainedModel(trainedModels, "LightGBM", 0.55, () =>
            TrainBaseModel(dataView, _mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 31,
                learningRate: 0.05,
                numberOfIterations: 200
            )));

        if (!externalReady)
        {
            TryAddTrainedModel(trainedModels, "FastTreeFallback", 0.25, () =>
                TrainBaseModel(dataView, _mlContext.BinaryClassification.Trainers.FastTree(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfLeaves: 24,
                    numberOfTrees: 120,
                    minimumExampleCountPerLeaf: 4,
                    learningRate: 0.08
                )));

            TryAddTrainedModel(trainedModels, "LinearLogisticFallback", 0.20, () =>
                TrainBaseModel(dataView, _mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    l2Regularization: 0.01f,
                    maximumNumberOfIterations: 100
                )));
        }

        if (!trainedModels.Any() && !externalReady) return;

        lock (_trainingLock)
        {
            _ensembleModels = trainedModels;
            _externalEnsembleReady = externalReady;
            _isTrained = true;
            _lastTrainedUtc = DateTime.UtcNow;
        }
    }

    private ITransformer TrainBaseModel(IDataView dataView, IEstimator<ITransformer> trainer)
    {
        var pipeline = BuildFeaturePipeline().Append(trainer);
        return pipeline.Fit(dataView);
    }

    private static void TryAddTrainedModel(
        List<WeightedHabitModel> models,
        string name,
        double weight,
        Func<ITransformer> train)
    {
        try
        {
            models.Add(new WeightedHabitModel(name, weight, train()));
        }
        catch
        {
        }
    }

    private IEstimator<ITransformer> BuildFeaturePipeline()
    {
        return _mlContext.Transforms.Categorical.OneHotEncoding("MaturityBucketEncoded", "MaturityBucket")
            .Append(_mlContext.Transforms.Concatenate("Features",
                "IsWeekend", "TargetCount", "CurrentStreak", "Momentum", "FatigueScore", "StreakVolatility", "RecentSuccessRate", "UserOverallRate", "ContextDriftScore", "RecoverySpeed", "LogHabitAge", "SynergyScore", "IsHoliday", "SinDayOfYear", "CosDayOfYear", "RetroactiveLogRatio", "CueStrengthScore", "MaturityBucketEncoded"));
    }

    private HabitEnsemblePrediction PredictWithEnsemble(HabitModelInput input, ExternalHabitEnsembleOutput? externalPrediction = null)
    {
        List<WeightedHabitModel> models;
        bool externalReady;
        lock (_trainingLock)
        {
            models = _ensembleModels.ToList();
            externalReady = _externalEnsembleReady;
        }

        if (!models.Any() && !externalReady)
        {
            return new HabitEnsemblePrediction();
        }

        double weightedSum = 0;
        double totalWeight = 0;
        var modelProbabilities = new Dictionary<string, float>();

        foreach (var weightedModel in models)
        {
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<HabitModelInput, HabitModelOutput>(weightedModel.Model);
            var result = predictionEngine.Predict(input);
            var probability = Math.Clamp(result.Probability, 0f, 1f);

            modelProbabilities[weightedModel.Name] = probability;
            weightedSum += probability * weightedModel.Weight;
            totalWeight += weightedModel.Weight;
        }

        if (externalReady)
        {
            externalPrediction ??= TryPredictExternalEnsemble(input);
            if (externalPrediction.StackedProbability.HasValue)
            {
                var probability = Math.Clamp(externalPrediction.StackedProbability.Value, 0f, 1f);
                modelProbabilities["StackedPython"] = probability;
                weightedSum += probability * 0.45;
                totalWeight += 0.45;
            }
            else
            {
                foreach (var (modelName, probability) in externalPrediction.ModelProbabilities)
                {
                    var clampedProbability = Math.Clamp(probability, 0f, 1f);
                    modelProbabilities[modelName] = clampedProbability;
                    weightedSum += clampedProbability;
                    totalWeight += 1;
                }
            }
        }

        return new HabitEnsemblePrediction
        {
            Probability = totalWeight > 0 ? weightedSum / totalWeight : 0,
            ModelProbabilities = modelProbabilities,
            ShapContributions = externalPrediction?.ShapContributions ?? new List<ShapContribution>()
        };
    }

    private string GenerateEnsembleSummary(HabitEnsemblePrediction prediction)
    {
        if (!prediction.ModelProbabilities.Any()) return string.Empty;

        var parts = prediction.ModelProbabilities
            .Select(p => $"{p.Key}: {Math.Round(p.Value * 100, 1)}%")
            .ToList();

        return $" Ensemble [{string.Join(", ", parts)}].";
    }

    private async Task<bool> TryTrainExternalEnsembleAsync(List<HabitModelInput> trainingData)
    {
        var scriptPath = GetExternalEnsembleScriptPath();
        if (!File.Exists(scriptPath)) return false;

        var modelDirectory = Path.Combine(Path.GetTempPath(), "HabitTracker");
        Directory.CreateDirectory(modelDirectory);

        var trainingDataPath = Path.Combine(modelDirectory, "habit_ensemble_training.json");
        var modelPath = Path.Combine(modelDirectory, "habit_ensemble_python.pkl");

        await File.WriteAllTextAsync(trainingDataPath, JsonSerializer.Serialize(trainingData));

        var result = await RunPythonEnsembleAsync(scriptPath, "train", trainingDataPath, modelPath, timeoutMilliseconds: 45000);
        if (!result.Success) return false;

        _externalEnsembleModelPath = modelPath;
        return true;
    }

    private ExternalHabitEnsembleOutput TryPredictExternalEnsemble(HabitModelInput input)
    {
        var scriptPath = GetExternalEnsembleScriptPath();
        var modelPath = _externalEnsembleModelPath;
        if (modelPath == null || !File.Exists(scriptPath) || !File.Exists(modelPath))
        {
            return new ExternalHabitEnsembleOutput();
        }

        var inputPath = Path.Combine(Path.GetTempPath(), "HabitTracker", $"habit_ensemble_input_{Guid.NewGuid():N}.json");
        File.WriteAllText(inputPath, JsonSerializer.Serialize(input));

        try
        {
            var result = RunPythonEnsembleAsync(scriptPath, "predict", modelPath, inputPath, timeoutMilliseconds: 7000)
                .GetAwaiter()
                .GetResult();

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                return new ExternalHabitEnsembleOutput();
            }

            return JsonSerializer.Deserialize<ExternalHabitEnsembleOutput>(
                result.Output,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new ExternalHabitEnsembleOutput();
        }
        catch
        {
            return new ExternalHabitEnsembleOutput();
        }
        finally
        {
            try
            {
                if (File.Exists(inputPath)) File.Delete(inputPath);
            }
            catch
            {
            }
        }
    }

    private List<ExternalHabitEnsembleOutput> TryPredictExternalEnsembleBatch(List<HabitModelInput> inputs)
    {
        var scriptPath = GetExternalEnsembleScriptPath();
        var modelPath = _externalEnsembleModelPath;
        if (modelPath == null || inputs.Count == 0 || !File.Exists(scriptPath) || !File.Exists(modelPath))
        {
            return new List<ExternalHabitEnsembleOutput>();
        }

        var inputPath = Path.Combine(Path.GetTempPath(), "HabitTracker", $"habit_ensemble_batch_{Guid.NewGuid():N}.json");
        File.WriteAllText(inputPath, JsonSerializer.Serialize(inputs));

        try
        {
            var result = RunPythonEnsembleAsync(scriptPath, "predict", modelPath, inputPath, timeoutMilliseconds: 12000)
                .GetAwaiter()
                .GetResult();

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                return new List<ExternalHabitEnsembleOutput>();
            }

            var batch = JsonSerializer.Deserialize<ExternalHabitEnsembleBatchOutput>(
                result.Output,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return batch?.Predictions ?? new List<ExternalHabitEnsembleOutput>();
        }
        catch
        {
            return new List<ExternalHabitEnsembleOutput>();
        }
        finally
        {
            try
            {
                if (File.Exists(inputPath)) File.Delete(inputPath);
            }
            catch
            {
            }
        }
    }

    private string GetExternalEnsembleScriptPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "ML", "habit_ensemble.py");
    }

    private static async Task<(bool Success, string Output)> RunPythonEnsembleAsync(
        string scriptPath,
        string command,
        string firstArgument,
        string secondArgument,
        int timeoutMilliseconds)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.StartInfo.ArgumentList.Add(scriptPath);
            process.StartInfo.ArgumentList.Add(command);
            process.StartInfo.ArgumentList.Add(firstArgument);
            process.StartInfo.ArgumentList.Add(secondArgument);

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var exited = await Task.Run(() => process.WaitForExit(timeoutMilliseconds));
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return (false, string.Empty);
            }

            var stdout = await stdoutTask;
            _ = await stderrTask;

            return (process.ExitCode == 0, stdout);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    public async Task<double> PredictSuccessProbabilityAsync(Habit habit)
    {
        var (prob, _) = await PredictWithShapAsync(habit);
        return prob;
    }

    public async Task<(double Probability, string Explanation)> PredictWithShapAsync(Habit habit)
    {
        if (!_isTrained) await TrainModelAsync();
        if (!_ensembleModels.Any()) return (0, string.Empty);

        var input = await BuildPredictionInputAsync(habit);
        var ensemblePrediction = PredictWithEnsemble(input);
        double prob = Math.Round(ensemblePrediction.Probability * 100, 1);

        string explanation = GenerateNaturalExplanation(input, prob, ensemblePrediction.ShapContributions);
        explanation += GenerateEnsembleSummary(ensemblePrediction);

        return (prob, explanation);
    }

    public async Task<Dictionary<string, (double Probability, string Explanation)>> PredictManyWithShapAsync(List<Habit> habits)
    {
        if (!_isTrained) await TrainModelAsync();

        var result = new Dictionary<string, (double Probability, string Explanation)>();
        if (!_ensembleModels.Any())
        {
            return result;
        }

        var userIds = habits.Select(h => h.UserId).Distinct().ToList();
        var allEntries = await _entriesCollection.Find(e => userIds.Contains(e.UserId)).ToListAsync();
        var entriesByUser = allEntries
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var entriesByHabit = allEntries
            .Where(e => e.HabitId != null)
            .GroupBy(e => e.HabitId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inputs = new List<(string HabitId, HabitModelInput Input)>();
        foreach (var habit in habits)
        {
            if (habit.Id == null) continue;
            entriesByUser.TryGetValue(habit.UserId, out var userEntries);
            entriesByHabit.TryGetValue(habit.Id, out var habitEntries);

            inputs.Add((
                habit.Id,
                BuildPredictionInputCached(habit, habitEntries ?? new List<HabitEntry>(), userEntries ?? new List<HabitEntry>())
            ));
        }

        var uncachedInputs = inputs
            .Where(i => !_memoryCache.TryGetValue(BuildPredictionCacheKey(i.Input), out HabitEnsemblePrediction? _))
            .ToList();
        var externalPredictions = TryPredictExternalEnsembleBatch(uncachedInputs.Select(i => i.Input).ToList());
        var externalPredictionsByCacheKey = new Dictionary<string, ExternalHabitEnsembleOutput>();
        for (var i = 0; i < uncachedInputs.Count && i < externalPredictions.Count; i++)
        {
            externalPredictionsByCacheKey[BuildPredictionCacheKey(uncachedInputs[i].Input)] = externalPredictions[i];
        }

        for (var i = 0; i < inputs.Count; i++)
        {
            var cacheKey = BuildPredictionCacheKey(inputs[i].Input);
            if (_memoryCache.TryGetValue(cacheKey, out HabitEnsemblePrediction? cachedPrediction) && cachedPrediction != null)
            {
                var cachedProbability = Math.Round(cachedPrediction.Probability * 100, 1);
                result[inputs[i].HabitId] = (
                    cachedProbability,
                    GenerateNaturalExplanation(inputs[i].Input, cachedProbability, cachedPrediction.ShapContributions) + GenerateEnsembleSummary(cachedPrediction)
                );
                continue;
            }

            externalPredictionsByCacheKey.TryGetValue(cacheKey, out var externalPrediction);

            var ensemblePrediction = PredictWithEnsemble(inputs[i].Input, externalPrediction);
            _memoryCache.Set(cacheKey, ensemblePrediction, PredictionCacheTtl);
            var probability = Math.Round(ensemblePrediction.Probability * 100, 1);
            var explanation = GenerateNaturalExplanation(inputs[i].Input, probability, ensemblePrediction.ShapContributions) + GenerateEnsembleSummary(ensemblePrediction);
            result[inputs[i].HabitId] = (probability, explanation);
        }

        return result;
    }

    private static string BuildPredictionCacheKey(HabitModelInput input)
    {
        return JsonSerializer.Serialize(input);
    }

    public async Task<(bool ShouldAsk, string? Reason)> ShouldRequestConfidenceAsync(Habit habit, double probability)
    {
        if (habit.Id == null) return (false, null);
        if (probability < 35 || probability > 65) return (false, null);

        var today = DateTime.UtcNow.Date;
        var recentEntries = await _entriesCollection.Find(e =>
            e.HabitId == habit.Id &&
            e.UserId == habit.UserId &&
            e.Date >= today.AddDays(-30) &&
            e.Date <= today
        ).ToListAsync();

        if (recentEntries.Count < 10) return (false, null);
        if (recentEntries.Any(e => e.Date.Date == today && e.ConfidenceScore.HasValue)) return (false, null);

        var recentActiveEntries = recentEntries
            .Where(e => habit.ActiveDays.Contains(e.Date.DayOfWeek) && e.Date.Date < today)
            .OrderByDescending(e => e.Date)
            .Take(3)
            .ToList();

        var unstableRecentPattern = recentActiveEntries.Count >= 3 &&
            recentActiveEntries.Any(e => e.IsFullyCompleted) &&
            recentActiveEntries.Any(e => !e.IsFullyCompleted);

        if (!unstableRecentPattern) return (false, null);

        return (true, "uncertainty_sampling");
    }

    public async Task<Dictionary<string, (bool ShouldAsk, string? Reason)>> GetConfidencePromptsAsync(
        List<Habit> habits,
        Dictionary<string, (double Probability, string Explanation)> predictions)
    {
        var result = new Dictionary<string, (bool ShouldAsk, string? Reason)>();
        var today = DateTime.UtcNow.Date;
        var userIds = habits.Select(h => h.UserId).Distinct().ToList();
        var recentEntries = await _entriesCollection.Find(e =>
            userIds.Contains(e.UserId) &&
            e.Date >= today.AddDays(-30) &&
            e.Date <= today
        ).ToListAsync();
        var recentEntriesByHabit = recentEntries
            .Where(e => e.HabitId != null)
            .GroupBy(e => e.HabitId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var habit in habits)
        {
            if (habit.Id == null || !predictions.TryGetValue(habit.Id, out var prediction))
            {
                continue;
            }

            recentEntriesByHabit.TryGetValue(habit.Id, out var habitEntries);
            result[habit.Id] = ShouldRequestConfidence(habit, prediction.Probability, habitEntries ?? new List<HabitEntry>(), today);
        }

        return result;
    }

    private (bool ShouldAsk, string? Reason) ShouldRequestConfidence(
        Habit habit,
        double probability,
        List<HabitEntry> recentEntries,
        DateTime today)
    {
        if (habit.Id == null) return (false, null);
        if (probability < 35 || probability > 65) return (false, null);
        if (recentEntries.Count < 10) return (false, null);
        if (recentEntries.Any(e => e.Date.Date == today && e.ConfidenceScore.HasValue)) return (false, null);

        var recentActiveEntries = recentEntries
            .Where(e => habit.ActiveDays.Contains(e.Date.DayOfWeek) && e.Date.Date < today)
            .OrderByDescending(e => e.Date)
            .Take(3)
            .ToList();

        var unstableRecentPattern = recentActiveEntries.Count >= 3 &&
            recentActiveEntries.Any(e => e.IsFullyCompleted) &&
            recentActiveEntries.Any(e => !e.IsFullyCompleted);

        if (!unstableRecentPattern) return (false, null);

        return (true, "uncertainty_sampling");
    }

    private async Task<HabitModelInput> BuildPredictionInputAsync(Habit habit)
    {
        var featureCacheKey = BuildFeatureCacheKey(habit);
        if (_memoryCache.TryGetValue(featureCacheKey, out HabitModelInput? cachedInput) && cachedInput != null)
        {
            return cachedInput;
        }

        var habitHistory = await _entriesCollection.Find(e => e.HabitId == habit.Id).ToListAsync();
        var allUserHistory = await _entriesCollection.Find(e => e.UserId == habit.UserId).ToListAsync();
        return BuildPredictionInputCached(habit, habitHistory, allUserHistory);
    }

    private HabitModelInput BuildPredictionInputCached(Habit habit, List<HabitEntry> habitHistory, List<HabitEntry> allUserHistory)
    {
        var featureCacheKey = BuildFeatureCacheKey(habit);
        if (_memoryCache.TryGetValue(featureCacheKey, out HabitModelInput? cachedInput) && cachedInput != null)
        {
            return cachedInput;
        }

        var userId = habit.UserId;
        var today = DateTime.UtcNow.Date;
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

        float contextDrift = CalculateWassersteinDrift(habitHistory, habit.Id!, today);
        float recoverySpeed = CalculateRecoverySpeed(habitHistory, habit, today);
        var ageFeatures = CalculateAgeFeatures(habit.StartDate, today);
        
        float synergy = CalculateSynergy(allUserHistory, habit.Id!, userId, today);
        float isHoliday = IsUkrainianHoliday(today) ? 1f : 0f;

        float daysInYear = DateTime.IsLeapYear(today.Year) ? 366f : 365f;
        float sinDay = (float)Math.Sin(2 * Math.PI * today.DayOfYear / daysInYear);
        float cosDay = (float)Math.Cos(2 * Math.PI * today.DayOfYear / daysInYear);

        bool isWeekend = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday;

        float retroRatio = CalculateRetroactiveRatio(habitHistory, habit.Id!, today);
        float cueStrength = CalculateCueStrength(habitHistory, habit.Id!, today);

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
            MaturityBucket = ageFeatures.Bucket,
            SynergyScore = synergy,
            IsHoliday = isHoliday,
            SinDayOfYear = sinDay,
            CosDayOfYear = cosDay,
            RetroactiveLogRatio = retroRatio,
            CueStrengthScore = cueStrength
        };

        _memoryCache.Set(featureCacheKey, input, FeatureCacheTtl);
        return input;
    }

    private string BuildFeatureCacheKey(Habit habit)
    {
        var version = _userCacheVersions.GetValueOrDefault(habit.UserId, 0);
        return $"habit_features:{habit.UserId}:{habit.Id}:{DateTime.UtcNow.Date:yyyyMMdd}:{version}";
    }

    public void InvalidateUserPredictionCache(string userId)
    {
        _userCacheVersions.AddOrUpdate(userId, 1, (_, current) => current + 1);
    }

    private string GenerateNaturalExplanation(HabitModelInput f, double prob, List<ShapContribution>? shapContributions = null)
    {
        if (shapContributions != null && shapContributions.Any())
        {
            return GenerateShapExplanation(shapContributions, prob);
        }

        var pos = new List<string>();
        var neg = new List<string>();

        if (f.IsWeekend == 1 && f.RecentSuccessRate < 0.5) neg.Add("вихідний день 🔴");
        if (f.IsHoliday == 1 && f.RecentSuccessRate < 0.6) neg.Add("святковий день 🔴");
        if (f.FatigueScore > 10) neg.Add("висока загальна втома 🔴");
        if (f.ContextDriftScore > 2.5f) neg.Add("зсув часу виконання 🔴");
        if (f.MaturityBucket == "Week1" || f.MaturityBucket == "Week2") neg.Add("звичка ще крихка 🟡");
        if (f.RecoverySpeed > 3f) neg.Add("важке відновлення після зривів 🟡");
        if (f.RetroactiveLogRatio > 0.4f) neg.Add("часте логування заднім числом 🟡");

        if (f.CurrentStreak > 5) pos.Add("гарний стрік 🟢");
        if (f.Momentum > 1) pos.Add("зростаючий моментум 🟢");
        if (f.LogHabitAge > 4) pos.Add("звичка вже закріплена 🟢");
        if (f.SynergyScore > 0.6f) pos.Add("синергія з іншими звичками 🟢");
        if (f.CueStrengthScore < 1.5f && f.LogHabitAge > 1) pos.Add("стабільний час виконання 🟢");

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

    private string GenerateShapExplanation(List<ShapContribution> contributions, double prob)
    {
        var condition = prob < 45 ? "низька" : prob > 70 ? "висока" : "середня";
        var top = contributions
            .OrderByDescending(c => Math.Abs(c.Value))
            .Take(5)
            .Select(c => $"{DescribeFeature(c.Feature)} ({(c.Value >= 0 ? "+" : "-")}{Math.Abs(c.Value):0.##})")
            .ToList();

        return $"Ймовірність {condition} ({prob}%), ключові внески моделі: {string.Join(", ", top)}.";
    }

    private static string DescribeFeature(string feature)
    {
        return feature switch
        {
            "IsWeekend" => "вихідний день",
            "TargetCount" => "цільова кількість",
            "CurrentStreak" => "поточний стрік",
            "Momentum" => "динаміка стріка",
            "FatigueScore" => "загальна втома",
            "StreakVolatility" => "нестабільність стріка",
            "RecentSuccessRate" => "останні успіхи",
            "UserOverallRate" => "загальний темп користувача",
            "ContextDriftScore" => "зміна контексту",
            "RecoverySpeed" => "швидкість відновлення",
            "LogHabitAge" => "вік звички",
            "SynergyScore" => "синергія з іншими звичками",
            "IsHoliday" => "святковий день",
            "SinDayOfYear" => "сезонність",
            "CosDayOfYear" => "сезонність",
            "RetroactiveLogRatio" => "записи заднім числом",
            "CueStrengthScore" => "стабільність часу виконання",
            _ when feature.StartsWith("MaturityBucket=") => $"стадія звички {feature.Split('=').Last()}",
            _ => feature
        };
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

    private float CalculateWassersteinDrift(List<HabitEntry> habitHistory, string habitId, DateTime targetDate)
    {
        var recentStart = targetDate.Date.AddDays(-14);
        var prevStart = targetDate.Date.AddDays(-44);

        var recentHours = habitHistory
            .Where(e => e.HabitId == habitId && e.Date >= recentStart && e.Date < targetDate && e.IsFullyCompleted)
            .Select(e => (float)e.LastModified.Hour)
            .OrderBy(h => h)
            .ToList();

        var prevHours = habitHistory
            .Where(e => e.HabitId == habitId && e.Date >= prevStart && e.Date < recentStart && e.IsFullyCompleted)
            .Select(e => (float)e.LastModified.Hour)
            .OrderBy(h => h)
            .ToList();

        if (recentHours.Count == 0 || prevHours.Count == 0) return 0f;

        float distance = 0f;
        var allHours = recentHours.Concat(prevHours).Distinct().OrderBy(h => h).ToList();
        
        for (int i = 0; i < allHours.Count - 1; i++)
        {
            float currentHour = allHours[i];
            float nextHour = allHours[i + 1];
            float cdfRecent = (float)recentHours.Count(h => h <= currentHour) / recentHours.Count;
            float cdfPrev = (float)prevHours.Count(h => h <= currentHour) / prevHours.Count;
            distance += Math.Abs(cdfRecent - cdfPrev) * (nextHour - currentHour);
        }

        return distance;
    }

    private float CalculateSynergy(List<HabitEntry> allUserEntries, string habitId, string userId, DateTime targetDate)
    {
        var recentStart = targetDate.AddDays(-30);
        var recentEntries = allUserEntries
            .Where(e => e.UserId == userId && e.Date >= recentStart && e.Date < targetDate && e.IsFullyCompleted)
            .ToList();

        var targetHabitDays = recentEntries
            .Where(e => e.HabitId == habitId)
            .Select(e => e.Date.Date)
            .ToHashSet();

        if (targetHabitDays.Count == 0) return 0f;

        var topOtherHabits = recentEntries
            .Where(e => e.HabitId != habitId)
            .GroupBy(e => e.HabitId)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .ToList();

        if (!topOtherHabits.Any()) return 0f;

        float totalSynergy = 0f;
        foreach (var otherHabitGroup in topOtherHabits)
        {
            var otherHabitDays = otherHabitGroup.Select(e => e.Date.Date).ToHashSet();
            int intersection = targetHabitDays.Intersect(otherHabitDays).Count();
            int union = targetHabitDays.Union(otherHabitDays).Count();
            if (union > 0)
            {
                totalSynergy += (float)intersection / union;
            }
        }

        return totalSynergy / topOtherHabits.Count;
    }

    private float CalculateRetroactiveRatio(List<HabitEntry> habitHistory, string habitId, DateTime targetDate)
    {
        var recentStart = targetDate.AddDays(-30);
        var recentEntries = habitHistory
            .Where(e => e.HabitId == habitId && e.Date >= recentStart && e.Date < targetDate && e.IsFullyCompleted)
            .ToList();

        if (recentEntries.Count == 0) return 0f;

        int retroactiveCount = recentEntries.Count(e => (e.LastModified.Date - e.Date.Date).TotalDays >= 1);
        return (float)retroactiveCount / recentEntries.Count;
    }

    private float CalculateCueStrength(List<HabitEntry> habitHistory, string habitId, DateTime targetDate)
    {
        var recentStart = targetDate.AddDays(-30);
        var recentHours = habitHistory
            .Where(e => e.HabitId == habitId && e.Date >= recentStart && e.Date < targetDate && e.IsFullyCompleted)
            .Select(e => (float)e.LastModified.Hour)
            .ToList();

        if (recentHours.Count < 2) return 12f; 

        float avg = recentHours.Average();
        float sumSq = recentHours.Sum(h => (h - avg) * (h - avg));
        return (float)Math.Sqrt(sumSq / recentHours.Count);
    }

    private bool IsUkrainianHoliday(DateTime date)
    {
        var holidays = new HashSet<(int Month, int Day)>
        {
            (1, 1), (3, 8), (5, 1), (5, 8), (6, 28), (7, 15), (7, 28), (8, 24), (10, 1), (12, 25)
        };
        return holidays.Contains((date.Month, date.Day));
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
