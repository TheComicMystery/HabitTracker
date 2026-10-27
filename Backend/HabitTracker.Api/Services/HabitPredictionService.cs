using Microsoft.ML;
using Microsoft.ML.Data;
using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Models.DTOs;
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
    [LoadColumn(18)] public float EndogenousFailureRate14d { get; set; }
    [LoadColumn(19)] public float ExogenousFailureRate14d { get; set; }
    [LoadColumn(20)] public float MixedFailureRate14d { get; set; }
    [LoadColumn(21)] public float EndogenousFailureRecency { get; set; }
    [LoadColumn(22)] public float ExogenousFailureRecency { get; set; }
    [LoadColumn(23)] public float FailureDecayImpact { get; set; }
    [LoadColumn(24)] public float SuccessRate3d { get; set; }
    [LoadColumn(25)] public float SuccessRate7d { get; set; }
    [LoadColumn(26)] public float SuccessRate30d { get; set; }
    [LoadColumn(27)] public float DaysSinceLastSuccess { get; set; }
    [LoadColumn(28)] public float DaysSinceLastFailure { get; set; }
    [LoadColumn(29)] public float ConsecutiveMisses { get; set; }
    [LoadColumn(30)] public float WeekdaySuccessRate { get; set; }
    [LoadColumn(31)] public float WeekendSuccessGap { get; set; }
    [LoadColumn(32)] public float ActiveDaysPerWeek { get; set; }
    [LoadColumn(33)] public float ScheduleDensity { get; set; }
    [LoadColumn(34)] public float IsDailyHabit { get; set; }
    [LoadColumn(35)] public float IsMultiCountHabit { get; set; }
    [LoadColumn(36)] public float IsWorkdayHabit { get; set; }
    [LoadColumn(37)] public float WeekendRecentRisk { get; set; }
    [LoadColumn(38)] public float FatigueRecentRisk { get; set; }
    [LoadColumn(39)] public float DriftVolatilityRisk { get; set; }
    [LoadColumn(40)] public float RecoveryPressure { get; set; }
    [LoadColumn(41)] public float StreakMomentumSignal { get; set; }
    [LoadColumn(42)] public float HabitIntensity { get; set; }
    [LoadColumn(43)] public float SubjectiveDifficulty { get; set; }
    [LoadColumn(44)] public float DisciplineSelf { get; set; }
    [LoadColumn(45)] public float WeekendVulnerability { get; set; }
    [LoadColumn(46)] public float Lag1Completed { get; set; } = 0.5f;
    [LoadColumn(47)] public float Lag2Completed { get; set; } = 0.5f;
    [LoadColumn(48)] public float Lag3Completed { get; set; } = 0.5f;
    [LoadColumn(49)] public float Lag4Completed { get; set; } = 0.5f;
    [LoadColumn(50)] public float Lag5Completed { get; set; } = 0.5f;
    [LoadColumn(51)] public float Lag6Completed { get; set; } = 0.5f;
    [LoadColumn(52)] public float Lag7Completed { get; set; } = 0.5f;
    [LoadColumn(53)] public float Lag8Completed { get; set; } = 0.5f;
    [LoadColumn(54)] public float Lag9Completed { get; set; } = 0.5f;
    [LoadColumn(55)] public float Lag10Completed { get; set; } = 0.5f;
    [LoadColumn(56)] public float Lag11Completed { get; set; } = 0.5f;
    [LoadColumn(57)] public float Lag12Completed { get; set; } = 0.5f;
    [LoadColumn(58)] public float Lag13Completed { get; set; } = 0.5f;
    [LoadColumn(59)] public float Lag14Completed { get; set; } = 0.5f;
    [LoadColumn(60)] public float MissPattern7d { get; set; }
    [LoadColumn(61)] public float LagTrend14d { get; set; }
    [LoadColumn(62)] public string PreferredTimeOfDay { get; set; } = "flexible";
    [LoadColumn(63)] public string InitialFailureReason { get; set; } = "none";
    [LoadColumn(64)] public string HabitCategory { get; set; } = "Uncategorized";
    [LoadColumn(65)] public bool Label { get; set; }
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

public class SyntheticTrainingReport
{
    public int Users { get; set; }
    public int Days { get; set; }
    public int GeneratedRows { get; set; }
    public int TrainingRows { get; set; }
    public int ValidationRows { get; set; }
    public double PositiveRate { get; set; }
    public double Accuracy { get; set; }
    public double DefaultThresholdAccuracy { get; set; }
    public double DecisionThreshold { get; set; }
    public double PersonaThresholdAccuracy { get; set; }
    public double BrierScore { get; set; }
    public double LogLoss { get; set; }
    public double RocAuc { get; set; }
    public double ExpectedCalibrationError { get; set; }
    public string RuntimeMode { get; set; } = string.Empty;
    public bool PythonEnsembleTrained { get; set; }
    public double StackedPythonWeight { get; set; }
    public double CalibrationIntercept { get; set; }
    public double CalibrationSlope { get; set; }
    public Dictionary<string, int> PersonaDistribution { get; set; } = new();
    public Dictionary<string, SyntheticPersonaMetric> PersonaMetrics { get; set; } = new();
    public Dictionary<string, float> ModelProbabilitiesSample { get; set; } = new();
}

public class SyntheticPersonaMetric
{
    public int Rows { get; set; }
    public double Accuracy { get; set; }
    public double DefaultThresholdAccuracy { get; set; }
    public double DecisionThreshold { get; set; }
    public double RocAuc { get; set; }
    public double BrierScore { get; set; }
    public double LogLoss { get; set; }
    public double ExpectedCalibrationError { get; set; }
}

public class HabitPredictionService
{
    private readonly IMongoCollection<Habit> _habitsCollection;
    private readonly IMongoCollection<HabitEntry> _entriesCollection;
    private readonly IMongoCollection<User> _usersCollection;
    private readonly MLContext _mlContext;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<HabitPredictionService> _logger;
    private readonly object _trainingLock = new();
    private List<WeightedHabitModel> _ensembleModels = new();
    private List<CachedPredictionEngine> _predictionEngines = new();
    private readonly ConcurrentDictionary<string, long> _userCacheVersions = new();
    private string? _externalEnsembleModelPath;
    private bool _externalEnsembleReady = false;
    private bool _isTrained = false;
    private DateTime? _lastTrainedUtc;
    private double _stackedPythonBlendWeight = 0.45;
    private double _calibrationIntercept = 0.0;
    private double _calibrationSlope = 1.0;

    private Dictionary<string, float> _userOverallRates = new();

    private sealed record WeightedHabitModel(string Name, double Weight, ITransformer Model);
    private sealed record CachedPredictionEngine(string Name, double Weight, PredictionEngine<HabitModelInput, HabitModelOutput> Engine, object SyncRoot);
    private sealed record SyntheticTrainingExample(string UserKey, string Persona, HabitModelInput Input);
    private sealed record SyntheticHabitTemplate(string Name, string Category, int TargetCount, List<DayOfWeek> ActiveDays, double BaseProbability, double WeekendPenalty, double FatigueSensitivity, double DriftSensitivity, double PartialChance, double DropoutRisk);
    private sealed record SyntheticPersonaProfile(string Name, double BaseReliability, double Volatility, double WeekendSensitivity, double FatigueSensitivity, double RecoverySpeed, double RetroactiveProbability, double ConfidenceBias, double Overconfidence, double RoutineStability, int MinHabits, int MaxHabits);
    private sealed record SyntheticLifeEvent(DateTime Start, DateTime End, string Kind, double Impact, float DriftBoost, FailureReasonKind FailureKind);
    private sealed record PersistedModelMetadata(
        DateTime LastTrainedUtc,
        bool ExternalEnsembleReady,
        string? ExternalEnsembleModelPath,
        double StackedPythonBlendWeight,
        double CalibrationIntercept,
        double CalibrationSlope,
        List<PersistedModelEntry> Models);
    private sealed record PersistedModelEntry(string Name, double Weight, string FileName);

    private static readonly TimeSpan ModelTrainingTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan FeatureCacheTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan PredictionCacheTtl = TimeSpan.FromHours(2);
    private const bool UsePythonRuntimeInferenceForUsers = false;
    private readonly string _modelDirectory = Path.Combine(AppContext.BaseDirectory, "MLModels");
    private readonly string _modelMetadataPath;

    public HabitPredictionService(IMongoDatabase mongoDatabase, IMemoryCache memoryCache, ILogger<HabitPredictionService> logger)
    {
        _habitsCollection = mongoDatabase.GetCollection<Habit>("Habits");
        _entriesCollection = mongoDatabase.GetCollection<HabitEntry>("HabitEntries");
        _usersCollection = mongoDatabase.GetCollection<User>("Users");
        _mlContext = new MLContext(seed: 1337);
        _memoryCache = memoryCache;
        _logger = logger;
        _modelMetadataPath = Path.Combine(_modelDirectory, "habit_mlnet_ensemble.json");
        TryLoadPersistedModels();
    }

    public async Task TrainModelAsync()
    {
        if (_isTrained && _lastTrainedUtc.HasValue && DateTime.UtcNow - _lastTrainedUtc.Value < ModelTrainingTtl) return;

        var habits = await _habitsCollection.Find(_ => true).ToListAsync();
        var entries = await _entriesCollection.Find(_ => true).ToListAsync();
        var users = await _usersCollection.Find(_ => true).ToListAsync();
        var usersById = users.Where(u => u.Id != null).ToDictionary(u => u.Id!, u => u);

        if (!entries.Any()) return;

        var trainingData = new List<HabitModelInput>();

        CalculateUserRates(habits, entries);

        foreach (var entry in entries)
        {
            var habit = habits.FirstOrDefault(h => h.Id == entry.HabitId);
            if (habit == null) continue;
            usersById.TryGetValue(habit.UserId, out var user);

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
            var failureSignals = CalculateFailureSignals(entries, habit, entry.Date);
            var temporalSignals = CalculateTemporalSignals(entries, habit, entry.Date);
            var scheduleFeatures = CalculateScheduleFeatures(habit.ActiveDays);

            var input = new HabitModelInput
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
                EndogenousFailureRate14d = failureSignals.EndogenousRate14d,
                ExogenousFailureRate14d = failureSignals.ExogenousRate14d,
                MixedFailureRate14d = failureSignals.MixedRate14d,
                EndogenousFailureRecency = failureSignals.EndogenousRecency,
                ExogenousFailureRecency = failureSignals.ExogenousRecency,
                FailureDecayImpact = failureSignals.DecayImpact,
                SuccessRate3d = temporalSignals.SuccessRate3d,
                SuccessRate7d = temporalSignals.SuccessRate7d,
                SuccessRate30d = temporalSignals.SuccessRate30d,
                DaysSinceLastSuccess = temporalSignals.DaysSinceLastSuccess,
                DaysSinceLastFailure = temporalSignals.DaysSinceLastFailure,
                ConsecutiveMisses = temporalSignals.ConsecutiveMisses,
                WeekdaySuccessRate = temporalSignals.WeekdaySuccessRate,
                WeekendSuccessGap = temporalSignals.WeekendSuccessGap,
                ActiveDaysPerWeek = scheduleFeatures.ActiveDaysPerWeek,
                ScheduleDensity = scheduleFeatures.ScheduleDensity,
                IsDailyHabit = scheduleFeatures.IsDailyHabit,
                IsMultiCountHabit = habit.TargetCount > 1 ? 1f : 0f,
                IsWorkdayHabit = scheduleFeatures.IsWorkdayHabit,
                HabitCategory = NormalizeHabitCategory(habit.Category),
                Label = entry.IsFullyCompleted
            };
            ApplyLagFeatures(input, entries, habit.Id, entry.Date);
            ApplyUserProfileFeatures(input, user);
            trainingData.Add(PopulateDerivedFeatures(input));
        }

        if (trainingData.Count < 20 || trainingData.Select(d => d.Label).Distinct().Count() < 2) return;

        await TrainFromInputsAsync(trainingData, force: false);
    }

    private async Task<bool> TrainFromInputsAsync(List<HabitModelInput> trainingData, bool force)
    {
        if (!force && _isTrained && _lastTrainedUtc.HasValue && DateTime.UtcNow - _lastTrainedUtc.Value < ModelTrainingTtl) return true;
        if (trainingData.Count < 20 || trainingData.Select(d => d.Label).Distinct().Count() < 2) return false;

        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        var externalReady = await TryTrainExternalEnsembleAsync(trainingData);
        var trainedModels = new List<WeightedHabitModel>();

        TryAddTrainedModel(trainedModels, "LightGBM", 0.55, () =>
            TrainBaseModel(dataView, _mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 63,
                learningRate: 0.035,
                numberOfIterations: 240
            )));

        TryAddTrainedModel(trainedModels, "FastTree", 0.25, () =>
            TrainBaseModel(dataView, _mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 48,
                numberOfTrees: 220,
                minimumExampleCountPerLeaf: 8,
                learningRate: 0.05
            )));

        TryAddTrainedModel(trainedModels, "LinearLogistic", 0.20, () =>
            TrainBaseModel(dataView, _mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label",
                featureColumnName: "Features",
                l2Regularization: 0.01f,
                maximumNumberOfIterations: 120
            )));

        if (!trainedModels.Any() && !externalReady) return false;

        var predictionEngines = trainedModels
            .Select(model => new CachedPredictionEngine(
                model.Name,
                model.Weight,
                _mlContext.Model.CreatePredictionEngine<HabitModelInput, HabitModelOutput>(model.Model),
                new object()))
            .ToList();

        lock (_trainingLock)
        {
            _ensembleModels = trainedModels;
            _predictionEngines = predictionEngines;
            _externalEnsembleReady = externalReady;
            _isTrained = true;
            _lastTrainedUtc = DateTime.UtcNow;
        }

        SavePersistedModels();
        return true;
    }

    private ITransformer TrainBaseModel(IDataView dataView, IEstimator<ITransformer> trainer)
    {
        var pipeline = BuildFeaturePipeline().Append(trainer);
        return pipeline.Fit(dataView);
    }

    private void SavePersistedModels()
    {
        try
        {
            List<WeightedHabitModel> models;
            DateTime? lastTrainedUtc;
            bool externalReady;
            string? externalPath;
            double stackedWeight;
            double calibrationIntercept;
            double calibrationSlope;

            lock (_trainingLock)
            {
                models = _ensembleModels.ToList();
                lastTrainedUtc = _lastTrainedUtc;
                externalReady = _externalEnsembleReady;
                externalPath = _externalEnsembleModelPath;
                stackedWeight = _stackedPythonBlendWeight;
                calibrationIntercept = _calibrationIntercept;
                calibrationSlope = _calibrationSlope;
            }

            if (!models.Any() || lastTrainedUtc == null) return;

            Directory.CreateDirectory(_modelDirectory);
            var schema = _mlContext.Data.LoadFromEnumerable(Array.Empty<HabitModelInput>()).Schema;
            var entries = new List<PersistedModelEntry>();

            foreach (var model in models)
            {
                var fileName = $"{SanitizeModelName(model.Name)}.zip";
                var path = Path.Combine(_modelDirectory, fileName);
                _mlContext.Model.Save(model.Model, schema, path);
                entries.Add(new PersistedModelEntry(model.Name, model.Weight, fileName));
            }

            var metadata = new PersistedModelMetadata(
                lastTrainedUtc.Value,
                externalReady,
                externalPath,
                stackedWeight,
                calibrationIntercept,
                calibrationSlope,
                entries);

            File.WriteAllText(_modelMetadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist ML.NET habit models.");
        }
    }

    private void TryLoadPersistedModels()
    {
        try
        {
            if (!File.Exists(_modelMetadataPath)) return;

            var metadata = JsonSerializer.Deserialize<PersistedModelMetadata>(File.ReadAllText(_modelMetadataPath));
            if (metadata == null || metadata.Models.Count == 0) return;

            var models = new List<WeightedHabitModel>();
            foreach (var entry in metadata.Models)
            {
                var path = Path.Combine(_modelDirectory, entry.FileName);
                if (!File.Exists(path)) continue;
                var model = _mlContext.Model.Load(path, out _);
                models.Add(new WeightedHabitModel(entry.Name, entry.Weight, model));
            }

            if (!models.Any()) return;

            var engines = models
                .Select(model => new CachedPredictionEngine(
                    model.Name,
                    model.Weight,
                    _mlContext.Model.CreatePredictionEngine<HabitModelInput, HabitModelOutput>(model.Model),
                    new object()))
                .ToList();

            lock (_trainingLock)
            {
                _ensembleModels = models;
                _predictionEngines = engines;
                _externalEnsembleReady = metadata.ExternalEnsembleReady && File.Exists(metadata.ExternalEnsembleModelPath);
                _externalEnsembleModelPath = metadata.ExternalEnsembleModelPath;
                _stackedPythonBlendWeight = metadata.StackedPythonBlendWeight;
                _calibrationIntercept = metadata.CalibrationIntercept;
                _calibrationSlope = metadata.CalibrationSlope;
                _lastTrainedUtc = metadata.LastTrainedUtc;
                _isTrained = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load persisted ML.NET habit models.");
        }
    }

    private static string SanitizeModelName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    public ModelHealthDto GetModelHealth()
    {
        List<WeightedHabitModel> models;
        DateTime? lastTrainedUtc;
        bool externalReady;
        double stackedWeight;
        double calibrationIntercept;
        double calibrationSlope;
        bool isTrained;

        lock (_trainingLock)
        {
            models = _ensembleModels.ToList();
            lastTrainedUtc = _lastTrainedUtc;
            externalReady = _externalEnsembleReady;
            stackedWeight = _stackedPythonBlendWeight;
            calibrationIntercept = _calibrationIntercept;
            calibrationSlope = _calibrationSlope;
            isTrained = _isTrained;
        }

        var artifacts = models
            .Select(model =>
            {
                var fileName = $"{SanitizeModelName(model.Name)}.zip";
                return new ModelArtifactStatusDto(
                    model.Name,
                    model.Weight,
                    fileName,
                    File.Exists(Path.Combine(_modelDirectory, fileName)));
            })
            .ToList();

        return new ModelHealthDto(
            isTrained,
            lastTrainedUtc,
            UsePythonRuntimeInferenceForUsers
                ? "ML.NET embedded with Python runtime blend"
                : "ML.NET embedded for user inference; Python ensemble offline/training only",
            externalReady,
            stackedWeight,
            calibrationIntercept,
            calibrationSlope,
            artifacts);
    }

    public async Task<SyntheticTrainingReport> TrainSyntheticCorpusAsync(int users = 500, int days = 180, int maxTrainingRows = 60000, int validationRows = 3000)
    {
        users = Math.Clamp(users, 50, 10000);
        days = Math.Clamp(days, 60, 365);
        maxTrainingRows = Math.Clamp(maxTrainingRows, 5000, 500000);
        validationRows = Math.Clamp(validationRows, 1000, 100000);

        var examples = GenerateSyntheticTrainingExamplesFast(users, days);
        var validationUsers = examples
            .Select(e => e.UserKey)
            .Distinct()
            .Where((_, index) => index % 5 == 0)
            .ToHashSet();

        var trainPool = examples.Where(e => !validationUsers.Contains(e.UserKey)).ToList();
        var validationPool = examples.Where(e => validationUsers.Contains(e.UserKey)).ToList();
        var trainInputs = SampleTrainingRows(trainPool, maxTrainingRows).Select(e => e.Input).ToList();

        var trained = await TrainFromInputsAsync(trainInputs, force: true);
        if (!trained)
        {
            throw new InvalidOperationException("Synthetic corpus did not contain enough diverse rows for training.");
        }

        var validationExamples = SampleTrainingRows(validationPool, Math.Min(validationRows, validationPool.Count)).ToList();
        var validationInputs = validationExamples.Select(e => e.Input).ToList();
        var externalPredictions = TryPredictExternalEnsembleBatch(validationInputs);
        TuneBlendAndCalibration(validationInputs, externalPredictions);
        SavePersistedModels();

        var labels = new List<bool>();
        var probabilities = new List<double>();
        HabitEnsemblePrediction? firstPrediction = null;

        for (var i = 0; i < validationInputs.Count; i++)
        {
            var externalPrediction = externalPredictions.Count == validationInputs.Count
                ? externalPredictions[i]
                : new ExternalHabitEnsembleOutput();
            var prediction = PredictWithEnsemble(validationInputs[i], externalPrediction, allowExternalInference: true);
            firstPrediction ??= prediction;
            labels.Add(validationInputs[i].Label);
            probabilities.Add(Math.Clamp(prediction.Probability, 0.000001, 0.999999));
        }

        var optimizedThreshold = FindBestAccuracyThreshold(labels, probabilities);
        var personaMetrics = CalculatePersonaMetrics(validationExamples, probabilities);
        var personaThresholdCorrect = validationExamples
            .Zip(probabilities, (example, probability) => (example, probability))
            .Count(p =>
            {
                var threshold = personaMetrics.TryGetValue(p.example.Persona, out var metric)
                    ? metric.DecisionThreshold
                    : optimizedThreshold.Threshold;
                return p.example.Input.Label == (p.probability >= threshold);
            });

        return new SyntheticTrainingReport
        {
            Users = users,
            Days = days,
            GeneratedRows = examples.Count,
            TrainingRows = trainInputs.Count,
            ValidationRows = validationInputs.Count,
            PositiveRate = Math.Round(examples.Count(e => e.Input.Label) / (double)examples.Count, 4),
            Accuracy = Math.Round(optimizedThreshold.Accuracy, 4),
            DefaultThresholdAccuracy = Math.Round(CalculateAccuracy(labels, probabilities), 4),
            DecisionThreshold = Math.Round(optimizedThreshold.Threshold, 4),
            PersonaThresholdAccuracy = Math.Round(personaThresholdCorrect / (double)validationExamples.Count, 4),
            BrierScore = Math.Round(CalculateBrierScore(labels, probabilities), 4),
            LogLoss = Math.Round(CalculateLogLoss(labels, probabilities), 4),
            RocAuc = Math.Round(CalculateRocAuc(labels, probabilities), 4),
            ExpectedCalibrationError = Math.Round(CalculateExpectedCalibrationError(labels, probabilities), 4),
            RuntimeMode = UsePythonRuntimeInferenceForUsers
                ? "ML.NET embedded + Python runtime ensemble"
                : "ML.NET embedded for user inference; Python ensemble offline/training only",
            PythonEnsembleTrained = _externalEnsembleReady,
            StackedPythonWeight = Math.Round(_stackedPythonBlendWeight, 4),
            CalibrationIntercept = Math.Round(_calibrationIntercept, 4),
            CalibrationSlope = Math.Round(_calibrationSlope, 4),
            PersonaDistribution = examples
                .GroupBy(e => e.Persona)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Select(e => e.UserKey).Distinct().Count()),
            PersonaMetrics = personaMetrics,
            ModelProbabilitiesSample = firstPrediction?.ModelProbabilities ?? new Dictionary<string, float>()
        };
    }

    private List<SyntheticTrainingExample> GenerateSyntheticTrainingExamples(int users, int days)
    {
        var random = new Random(20260511);
        var templates = BuildSyntheticHabitTemplates();
        var personas = new[] { "stable", "swingy", "lazy", "weekend_vulnerable", "evening_logger", "overloaded", "retroactive_logger", "early_dropout", "fast_recovery" };
        var startDate = DateTime.UtcNow.Date.AddDays(-days);
        var examples = new List<SyntheticTrainingExample>(users * 6 * days);

        for (var userIndex = 0; userIndex < users; userIndex++)
        {
            var userKey = $"synthetic_user_{userIndex:D5}";
            var persona = personas[userIndex % personas.Length];
            var habitCount = random.Next(5, 10);
            var userTemplates = templates.OrderBy(_ => random.Next()).Take(habitCount).ToList();
            var userEntries = new List<HabitEntry>();
            var dayCompletionLoad = new Dictionary<DateTime, int>();
            var userRate = GetPersonaBaseReliability(persona);

            for (var habitIndex = 0; habitIndex < userTemplates.Count; habitIndex++)
            {
                var template = userTemplates[habitIndex];
                var habit = new Habit
                {
                    Id = $"synthetic_habit_{userIndex:D5}_{habitIndex:D2}",
                    UserId = userKey,
                    Name = template.Name,
                    Category = template.Category,
                    TargetCount = template.TargetCount,
                    ActiveDays = template.ActiveDays,
                    StartDate = startDate.AddDays(random.Next(0, 21))
                };

                var habitEntries = new List<HabitEntry>();
                var preferredHour = persona == "evening_logger" ? random.Next(19, 23) : random.Next(6, 22);
                var habitSpecificReliability = Math.Clamp(template.BaseProbability + random.NextDouble() * 0.18 - 0.09, 0.22, 0.96);

                for (var day = startDate; day <= DateTime.UtcNow.Date; day = day.AddDays(1))
                {
                    if (day < habit.StartDate.Date || !habit.ActiveDays.Contains(day.DayOfWeek)) continue;

                    var currentStreak = CalculateStreakForDate(habitEntries, habit.Id!, day);
                    var prevStreak = CalculateStreakForDate(habitEntries, habit.Id!, day.AddDays(-7));
                    var momentum = currentStreak - prevStreak;
                    var fatigue = (float)dayCompletionLoad
                        .Where(p => p.Key >= day.AddDays(-3) && p.Key < day)
                        .Sum(p => p.Value);
                    var volatility = CalculateStreakVolatility(habitEntries, habit.Id!, day);
                    var recentRate = CalculateRecentRate(habitEntries, habit.Id!, day, 14);
                    var contextDrift = CalculateWassersteinDrift(habitEntries, habit.Id!, day);
                    var recoverySpeed = CalculateRecoverySpeed(habitEntries, habit, day);
                    var ageFeatures = CalculateAgeFeatures(habit.StartDate, day);
                    var synergy = CalculateSynergy(userEntries, habit.Id!, userKey, day);
                    var isHoliday = IsUkrainianHoliday(day) ? 1f : 0f;
                    var daysInYear = DateTime.IsLeapYear(day.Year) ? 366f : 365f;
                    var sinDay = (float)Math.Sin(2 * Math.PI * day.DayOfYear / daysInYear);
                    var cosDay = (float)Math.Cos(2 * Math.PI * day.DayOfYear / daysInYear);
                    var retroRatio = CalculateRetroactiveRatio(habitEntries, habit.Id!, day);
                    var cueStrength = CalculateCueStrength(habitEntries, habit.Id!, day);
                    var failureSignals = CalculateFailureSignals(habitEntries, habit, day);
                    var temporalSignals = CalculateTemporalSignals(habitEntries, habit, day);
                    var isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;

                    var probability = CalculateSyntheticSuccessProbability(
                        persona,
                        habitSpecificReliability,
                        userRate,
                        isWeekend,
                        currentStreak,
                        recentRate,
                        fatigue,
                        contextDrift,
                        failureSignals.DecayImpact,
                        day,
                        habit.StartDate);

                    var completed = random.NextDouble() < probability;
                    var completedCount = completed
                        ? habit.TargetCount
                        : random.Next(0, Math.Max(1, habit.TargetCount));

                    var reason = completed ? null : PickSyntheticFailureReason(random, persona, isWeekend, fatigue, isHoliday == 1f);
                    var logHourJitter = persona == "retroactive_logger" ? random.Next(-5, 6) : random.Next(-2, 3);
                    var logDate = day.AddHours(Math.Clamp(preferredHour + logHourJitter, 0, 23)).AddMinutes(random.Next(0, 60));
                    var scheduleFeatures = CalculateScheduleFeatures(habit.ActiveDays);
                    if (persona == "retroactive_logger" && random.NextDouble() < 0.28)
                    {
                        logDate = logDate.AddDays(random.Next(1, 4));
                    }

                    var input = new HabitModelInput
                    {
                        IsWeekend = isWeekend ? 1f : 0f,
                        TargetCount = habit.TargetCount,
                        CurrentStreak = currentStreak,
                        Momentum = momentum,
                        FatigueScore = fatigue,
                        StreakVolatility = volatility,
                        RecentSuccessRate = recentRate,
                        UserOverallRate = (float)userRate,
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
                        EndogenousFailureRate14d = failureSignals.EndogenousRate14d,
                        ExogenousFailureRate14d = failureSignals.ExogenousRate14d,
                        MixedFailureRate14d = failureSignals.MixedRate14d,
                        EndogenousFailureRecency = failureSignals.EndogenousRecency,
                        ExogenousFailureRecency = failureSignals.ExogenousRecency,
                        FailureDecayImpact = failureSignals.DecayImpact,
                        SuccessRate3d = temporalSignals.SuccessRate3d,
                        SuccessRate7d = temporalSignals.SuccessRate7d,
                        SuccessRate30d = temporalSignals.SuccessRate30d,
                        DaysSinceLastSuccess = temporalSignals.DaysSinceLastSuccess,
                        DaysSinceLastFailure = temporalSignals.DaysSinceLastFailure,
                        ConsecutiveMisses = temporalSignals.ConsecutiveMisses,
                        WeekdaySuccessRate = temporalSignals.WeekdaySuccessRate,
                        WeekendSuccessGap = temporalSignals.WeekendSuccessGap,
                        ActiveDaysPerWeek = scheduleFeatures.ActiveDaysPerWeek,
                        ScheduleDensity = scheduleFeatures.ScheduleDensity,
                        IsDailyHabit = scheduleFeatures.IsDailyHabit,
                        IsMultiCountHabit = habit.TargetCount > 1 ? 1f : 0f,
                        IsWorkdayHabit = scheduleFeatures.IsWorkdayHabit,
                        HabitCategory = NormalizeHabitCategory(habit.Category),
                        Label = completed
                    };

                    ApplyLagFeatures(input, habitEntries
                        .Where(e => e.HabitId == habit.Id && e.Date < day)
                        .OrderByDescending(e => e.Date)
                        .Select(e => e.IsFullyCompleted));
                    ApplySyntheticProfileFeatures(input, persona);
                    examples.Add(new SyntheticTrainingExample(userKey, persona, PopulateDerivedFeatures(input)));

                    var entry = new HabitEntry
                    {
                        HabitId = habit.Id!,
                        UserId = userKey,
                        Date = day,
                        CompletedCount = completedCount,
                        IsFullyCompleted = completed,
                        FailureReason = reason,
                        ConfidenceScore = completed && random.NextDouble() < 0.08 ? random.Next(7, 11) : !completed && random.NextDouble() < 0.18 ? random.Next(1, 6) : null,
                        LastModified = logDate
                    };

                    habitEntries.Add(entry);
                    userEntries.Add(entry);
                    if (completed)
                    {
                        dayCompletionLoad[day] = dayCompletionLoad.GetValueOrDefault(day) + 1;
                    }
                }
            }
        }

        return examples;
    }

    private List<SyntheticTrainingExample> GenerateSyntheticTrainingExamplesFast(int users, int days)
    {
        var random = new Random(20260511);
        var templates = BuildSyntheticHabitTemplates();
        var personas = BuildSyntheticPersonaProfiles();
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-days + 1);
        var examples = new List<SyntheticTrainingExample>(users * 10 * days / 2);

        for (var userIndex = 0; userIndex < users; userIndex++)
        {
            var userKey = $"synthetic_user_{userIndex:D5}";
            var persona = personas[userIndex % personas.Count];
            var userRate = Math.Clamp(persona.BaseReliability + random.NextDouble() * 0.12 - 0.06, 0.18, 0.96);
            var habitCount = random.Next(persona.MinHabits, persona.MaxHabits + 1);
            var userTemplates = templates
                .OrderBy(t => random.NextDouble() + (persona.Name == "overloaded" && t.Category == "Productivity" ? -0.18 : 0))
                .Take(habitCount)
                .ToList();
            var lifeEvents = BuildSyntheticLifeEvents(random, startDate, endDate, persona.Name);
            var dayCompletionLoad = new Dictionary<DateTime, int>();
            var activeHabitLoad = userTemplates.Count;

            for (var habitIndex = 0; habitIndex < userTemplates.Count; habitIndex++)
            {
                var template = userTemplates[habitIndex];
                var habitStart = startDate.AddDays(random.Next(0, Math.Min(45, Math.Max(1, days / 4))));
                var plannedEnd = persona.Name == "early_dropout" && random.NextDouble() < 0.62
                    ? habitStart.AddDays(random.Next(18, 55))
                    : persona.Name == "overloaded" && random.NextDouble() < 0.22
                        ? habitStart.AddDays(random.Next(55, Math.Max(56, days - 10)))
                        : endDate.AddDays(1);
                var habitReliability = Math.Clamp(template.BaseProbability + random.NextDouble() * 0.20 - 0.10 + (activeHabitLoad > 12 ? -0.06 : 0), 0.16, 0.97);
                var preferredHour = PickSyntheticPreferredHour(random, persona.Name, template.Category);
                var habitRoutineShiftDay = random.NextDouble() < 0.18 ? habitStart.AddDays(random.Next(35, Math.Max(36, days - 7))) : DateTime.MaxValue;
                var currentStreak = 0;
                var streakByDate = new Dictionary<DateTime, int>();
                var recentOutcomes = new Queue<bool>();
                var recentFailures = new Queue<(DateTime Date, FailureReasonKind Kind)>();
                var retroactiveFlags = new Queue<bool>();
                var successHours = new Queue<int>();
                var weekdayOutcomes = new Queue<(DateTime Date, bool Completed)>();
                var lastCompleted = false;

                for (var day = startDate; day <= endDate; day = day.AddDays(1))
                {
                    if (day < habitStart || day > plannedEnd || !template.ActiveDays.Contains(day.DayOfWeek)) continue;

                    streakByDate.TryGetValue(day.AddDays(-7), out var previousWeekStreak);
                    var momentum = currentStreak - previousWeekStreak;
                    var fatigue = (float)dayCompletionLoad
                        .Where(p => p.Key >= day.AddDays(-3) && p.Key < day)
                        .Sum(p => p.Value);
                    fatigue += Math.Max(0, activeHabitLoad - 9) * 0.35f;
                    var recentRate = recentOutcomes.Count == 0 ? 0.5f : recentOutcomes.Count(o => o) / (float)recentOutcomes.Count;
                    var successRate3d = CalculateQueueRate(recentOutcomes, 3);
                    var successRate7d = CalculateQueueRate(recentOutcomes, 7);
                    var successRate30d = CalculateQueueRate(recentOutcomes, 30);
                    var failureSignals = CalculateSyntheticFailureSignals(recentFailures, day);
                    var isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
                    var activeEvent = lifeEvents.FirstOrDefault(e => day >= e.Start && day <= e.End);
                    var contextDrift = CalculateSyntheticContextDrift(persona.Name, day, habitStart, random)
                        + (activeEvent?.DriftBoost ?? 0f)
                        + (day >= habitRoutineShiftDay && day <= habitRoutineShiftDay.AddDays(18) ? 1.2f : 0f);
                    var recoverySpeed = (float)Math.Clamp(persona.RecoverySpeed + random.NextDouble() * 0.6 - 0.3, 0.7, 7.5);
                    var ageFeatures = CalculateAgeFeatures(habitStart, day);
                    var daysInYear = DateTime.IsLeapYear(day.Year) ? 366f : 365f;
                    var sinDay = (float)Math.Sin(2 * Math.PI * day.DayOfYear / daysInYear);
                    var cosDay = (float)Math.Cos(2 * Math.PI * day.DayOfYear / daysInYear);
                    var retroRatio = retroactiveFlags.Count == 0 ? 0f : retroactiveFlags.Count(v => v) / (float)retroactiveFlags.Count;
                    var cueStrength = CalculateSyntheticCueStrength(persona.Name, successHours, random);
                    var isHoliday = IsUkrainianHoliday(day) ? 1f : 0f;
                    var synergy = dayCompletionLoad.GetValueOrDefault(day) > 0 ? Math.Clamp(dayCompletionLoad.GetValueOrDefault(day) / 4f, 0f, 1f) : 0f;
                    var daysSinceLastSuccess = CalculateSyntheticDaysSinceLast(recentOutcomes, true);
                    var daysSinceLastFailure = CalculateSyntheticDaysSinceLast(recentOutcomes, false);
                    var consecutiveMisses = CalculateSyntheticConsecutiveMisses(recentOutcomes);
                    var weekdaySuccessRate = CalculateSyntheticWeekdayRate(weekdayOutcomes, day);
                    var weekendSuccessGap = CalculateSyntheticWeekendGap(weekdayOutcomes, persona, random);
                    var volatility = (float)Math.Clamp(persona.Volatility + (1 - recentRate) * 1.1 + consecutiveMisses * 0.16 + random.NextDouble() * 0.25, 0.1, 6.5);

                    var probability = CalculateSyntheticSuccessProbability(
                        persona.Name,
                        habitReliability,
                        userRate,
                        isWeekend,
                        currentStreak,
                        recentRate,
                        fatigue,
                        contextDrift,
                        failureSignals.DecayImpact,
                        day,
                        habitStart,
                        template,
                        persona,
                        activeEvent);

                    var completed = random.NextDouble() < probability;
                    var completedCount = completed
                        ? template.TargetCount
                            : random.NextDouble() < template.PartialChance
                                ? random.Next(1, Math.Max(2, template.TargetCount))
                                : 0;
                    var scheduleFeatures = CalculateScheduleFeatures(template.ActiveDays);
                    var input = new HabitModelInput
                    {
                        IsWeekend = isWeekend ? 1f : 0f,
                        TargetCount = template.TargetCount,
                        CurrentStreak = currentStreak,
                        Momentum = momentum,
                        FatigueScore = fatigue,
                        StreakVolatility = volatility,
                        RecentSuccessRate = recentRate,
                        UserOverallRate = (float)userRate,
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
                        EndogenousFailureRate14d = failureSignals.EndogenousRate14d,
                        ExogenousFailureRate14d = failureSignals.ExogenousRate14d,
                        MixedFailureRate14d = failureSignals.MixedRate14d,
                        EndogenousFailureRecency = failureSignals.EndogenousRecency,
                        ExogenousFailureRecency = failureSignals.ExogenousRecency,
                        FailureDecayImpact = failureSignals.DecayImpact,
                        SuccessRate3d = successRate3d,
                        SuccessRate7d = successRate7d,
                        SuccessRate30d = successRate30d,
                        DaysSinceLastSuccess = daysSinceLastSuccess,
                        DaysSinceLastFailure = daysSinceLastFailure,
                        ConsecutiveMisses = consecutiveMisses,
                        WeekdaySuccessRate = weekdaySuccessRate,
                        WeekendSuccessGap = weekendSuccessGap,
                        ActiveDaysPerWeek = scheduleFeatures.ActiveDaysPerWeek,
                        ScheduleDensity = scheduleFeatures.ScheduleDensity,
                        IsDailyHabit = scheduleFeatures.IsDailyHabit,
                        IsMultiCountHabit = template.TargetCount > 1 ? 1f : 0f,
                        IsWorkdayHabit = scheduleFeatures.IsWorkdayHabit,
                        HabitCategory = NormalizeHabitCategory(template.Category),
                        Label = completed
                    };

                    ApplyLagFeatures(input, recentOutcomes.Reverse());
                    ApplySyntheticProfileFeatures(input, persona.Name);
                    examples.Add(new SyntheticTrainingExample(userKey, persona.Name, PopulateDerivedFeatures(input)));

                    if (completed)
                    {
                        currentStreak++;
                        dayCompletionLoad[day] = dayCompletionLoad.GetValueOrDefault(day) + 1;
                        successHours.Enqueue(PickSyntheticLogHour(random, persona, preferredHour));
                    }
                    else
                    {
                        currentStreak = 0;
                        var reasonKind = activeEvent?.FailureKind ?? PickSyntheticFailureKind(random, persona.Name, isWeekend, fatigue, isHoliday == 1f);
                        recentFailures.Enqueue((day, reasonKind));
                    }

                    var confidenceScore = GenerateSyntheticConfidenceScore(random, probability, completed, persona, recentRate, activeEvent != null);
                    if (confidenceScore.HasValue)
                    {
                        input.UserOverallRate = Math.Clamp(input.UserOverallRate + (confidenceScore.Value - 5.5f) * 0.012f, 0.05f, 0.98f);
                    }

                    var isRetroactive = random.NextDouble() < persona.RetroactiveProbability || (!completed && random.NextDouble() < 0.04);
                    retroactiveFlags.Enqueue(isRetroactive);
                    recentOutcomes.Enqueue(completed);
                    weekdayOutcomes.Enqueue((day, completed));
                    streakByDate[day] = currentStreak;
                    lastCompleted = completed;

                    while (recentOutcomes.Count > 30) recentOutcomes.Dequeue();
                    while (weekdayOutcomes.Count > 60) weekdayOutcomes.Dequeue();
                    while (retroactiveFlags.Count > 30) retroactiveFlags.Dequeue();
                    while (successHours.Count > 30) successHours.Dequeue();
                    while (recentFailures.Count > 60 || (recentFailures.Count > 0 && recentFailures.Peek().Date < day.AddDays(-60))) recentFailures.Dequeue();

                    if (template.DropoutRisk > 0 && !lastCompleted && consecutiveMisses >= 8 && random.NextDouble() < template.DropoutRisk)
                    {
                        plannedEnd = day;
                    }
                }
            }
        }

        return examples;
    }

    private static List<SyntheticPersonaProfile> BuildSyntheticPersonaProfiles()
    {
        return new List<SyntheticPersonaProfile>
        {
            new("stable", 0.82, 0.35, 0.12, 0.65, 1.7, 0.03, 0.20, 0.03, 0.88, 5, 10),
            new("swingy", 0.62, 2.85, 0.22, 0.95, 3.5, 0.08, -0.10, 0.12, 0.50, 6, 13),
            new("lazy", 0.47, 2.25, 0.18, 1.05, 5.6, 0.11, -0.55, 0.06, 0.42, 4, 9),
            new("weekend_vulnerable", 0.66, 1.35, 1.10, 0.85, 2.8, 0.07, -0.15, 0.05, 0.62, 6, 12),
            new("evening_logger", 0.68, 1.20, 0.28, 0.82, 2.5, 0.18, 0.05, 0.05, 0.46, 6, 12),
            new("overloaded", 0.57, 1.90, 0.25, 1.65, 4.2, 0.12, -0.25, 0.09, 0.48, 12, 18),
            new("retroactive_logger", 0.63, 1.55, 0.30, 0.92, 3.1, 0.34, -0.05, 0.10, 0.34, 7, 14),
            new("early_dropout", 0.55, 2.60, 0.24, 1.15, 5.0, 0.10, 0.15, 0.18, 0.52, 5, 11),
            new("fast_recovery", 0.71, 1.05, 0.18, 0.78, 1.15, 0.06, 0.18, 0.04, 0.72, 6, 13),
            new("anxious_underconfident", 0.69, 1.45, 0.22, 1.00, 2.9, 0.09, -1.15, 0.00, 0.66, 5, 11),
            new("optimistic_overconfident", 0.59, 1.75, 0.26, 0.95, 3.8, 0.08, 0.95, 0.32, 0.56, 7, 15),
            new("seasonal_student", 0.65, 1.65, 0.34, 1.05, 3.0, 0.07, -0.05, 0.08, 0.60, 6, 14),
            new("busy_parent", 0.61, 1.85, 0.42, 1.28, 3.7, 0.13, -0.20, 0.07, 0.43, 8, 16),
            new("health_rebuilder", 0.58, 1.60, 0.20, 0.88, 2.1, 0.06, 0.05, 0.04, 0.70, 5, 10)
        };
    }

    private static List<SyntheticLifeEvent> BuildSyntheticLifeEvents(Random random, DateTime startDate, DateTime endDate, string persona)
    {
        var events = new List<SyntheticLifeEvent>();
        var totalDays = Math.Max(1, (endDate - startDate).Days);
        var eventCount = persona switch
        {
            "stable" => random.Next(1, 3),
            "overloaded" or "busy_parent" or "seasonal_student" => random.Next(4, 8),
            "swingy" or "early_dropout" => random.Next(3, 6),
            _ => random.Next(2, 5)
        };

        for (var i = 0; i < eventCount; i++)
        {
            var eventStart = startDate.AddDays(random.Next(5, Math.Max(6, totalDays - 7)));
            var kindRoll = random.NextDouble();
            string kind;
            int duration;
            double impact;
            float drift;
            FailureReasonKind failureKind;

            if (kindRoll < 0.22)
            {
                kind = "illness";
                duration = random.Next(3, 10);
                impact = -1.0 - random.NextDouble() * 0.7;
                drift = 1.6f;
                failureKind = FailureReasonKind.Exogenous;
            }
            else if (kindRoll < 0.42)
            {
                kind = "deadline";
                duration = random.Next(5, 16);
                impact = persona == "overloaded" ? -1.25 : -0.75;
                drift = 1.1f;
                failureKind = FailureReasonKind.Mixed;
            }
            else if (kindRoll < 0.58)
            {
                kind = "holiday";
                duration = random.Next(2, 8);
                impact = -0.65 - random.NextDouble() * 0.45;
                drift = 1.25f;
                failureKind = FailureReasonKind.Exogenous;
            }
            else if (kindRoll < 0.74)
            {
                kind = "motivation_boost";
                duration = random.Next(5, 18);
                impact = 0.35 + random.NextDouble() * 0.55;
                drift = 0.25f;
                failureKind = FailureReasonKind.None;
            }
            else if (kindRoll < 0.88)
            {
                kind = "routine_change";
                duration = random.Next(10, 30);
                impact = -0.38 - random.NextDouble() * 0.38;
                drift = 1.8f;
                failureKind = FailureReasonKind.Mixed;
            }
            else
            {
                kind = "burnout";
                duration = random.Next(12, 32);
                impact = -0.85 - random.NextDouble() * 0.6;
                drift = 1.4f;
                failureKind = FailureReasonKind.Endogenous;
            }

            events.Add(new SyntheticLifeEvent(eventStart, eventStart.AddDays(duration), kind, impact, drift, failureKind));
        }

        if (persona == "seasonal_student")
        {
            foreach (var year in Enumerable.Range(startDate.Year, endDate.Year - startDate.Year + 1))
            {
                events.Add(new SyntheticLifeEvent(new DateTime(year, 5, 15), new DateTime(year, 6, 20), "exam_season", -0.75, 1.2f, FailureReasonKind.Mixed));
                events.Add(new SyntheticLifeEvent(new DateTime(year, 9, 1), new DateTime(year, 9, 14), "study_restart", 0.45, 0.5f, FailureReasonKind.None));
            }
        }

        return events.Where(e => e.End >= startDate && e.Start <= endDate).ToList();
    }

    private static int PickSyntheticPreferredHour(Random random, string persona, string category)
    {
        if (persona == "evening_logger") return random.Next(19, 23);
        if (category is "Health" or "Mind") return random.Next(6, 10);
        if (category is "Learning" or "Creativity") return random.Next(18, 23);
        if (category == "Productivity") return random.Next(8, 12);
        return random.Next(7, 22);
    }

    private static int PickSyntheticLogHour(Random random, SyntheticPersonaProfile persona, int preferredHour)
    {
        var jitter = persona.RoutineStability > 0.75
            ? random.Next(-1, 2)
            : persona.RoutineStability < 0.45
                ? random.Next(-5, 6)
                : random.Next(-3, 4);
        return Math.Clamp(preferredHour + jitter, 0, 23);
    }

    private static int? GenerateSyntheticConfidenceScore(Random random, double probability, bool completed, SyntheticPersonaProfile persona, float recentRate, bool activeEvent)
    {
        var askChance = probability is > 0.35 and < 0.65 ? 0.36 : 0.08;
        if (activeEvent) askChance += 0.08;
        if (random.NextDouble() > askChance) return null;

        var baseScore = probability * 10 + persona.ConfidenceBias + (recentRate - 0.5) * 1.2;
        if (persona.Overconfidence > 0 && random.NextDouble() < persona.Overconfidence) baseScore += 1.6;
        if (!completed && persona.Name == "optimistic_overconfident") baseScore += 1.2;
        if (completed && persona.Name == "anxious_underconfident") baseScore -= 1.0;
        baseScore += random.NextDouble() * 1.4 - 0.7;
        return (int)Math.Clamp(Math.Round(baseScore), 1, 10);
    }

    private static float CalculateSyntheticWeekendGap(Queue<(DateTime Date, bool Completed)> outcomes, SyntheticPersonaProfile persona, Random random)
    {
        if (outcomes.Count < 10)
        {
            return (float)Math.Clamp(persona.WeekendSensitivity * 0.18 + random.NextDouble() * 0.08, -0.08, 0.55);
        }

        var weekday = outcomes.Where(o => o.Date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).ToList();
        var weekend = outcomes.Where(o => o.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).ToList();
        if (weekday.Count == 0 || weekend.Count == 0) return 0f;

        var weekdayRate = weekday.Count(o => o.Completed) / (float)weekday.Count;
        var weekendRate = weekend.Count(o => o.Completed) / (float)weekend.Count;
        return Math.Clamp(weekdayRate - weekendRate, -0.4f, 0.75f);
    }

    private static float CalculateSyntheticWeekdayRate(Queue<(DateTime Date, bool Completed)> outcomes, DateTime targetDate)
    {
        var selected = outcomes.Where(o => o.Date.DayOfWeek == targetDate.DayOfWeek).ToList();
        return selected.Count == 0 ? 0.5f : selected.Count(o => o.Completed) / (float)selected.Count;
    }

    private static FailureReasonKind PickSyntheticFailureKind(Random random, string persona, bool isWeekend, float fatigue, bool isHoliday)
    {
        if (persona == "lazy" && random.NextDouble() < 0.55) return FailureReasonKind.Endogenous;
        if (persona == "weekend_vulnerable" && isWeekend && random.NextDouble() < 0.55) return FailureReasonKind.Exogenous;
        if ((persona == "overloaded" || persona == "busy_parent") && fatigue > 8 && random.NextDouble() < 0.65) return FailureReasonKind.Mixed;
        if (persona == "seasonal_student" && random.NextDouble() < 0.42) return FailureReasonKind.Mixed;
        if (isHoliday && random.NextDouble() < 0.58) return FailureReasonKind.Exogenous;

        var roll = random.NextDouble();
        if (roll < 0.34) return FailureReasonKind.Mixed;
        if (roll < 0.62) return FailureReasonKind.Endogenous;
        if (roll < 0.88) return FailureReasonKind.Exogenous;
        return FailureReasonKind.Mixed;
    }

    private static (float EndogenousRate14d, float ExogenousRate14d, float MixedRate14d, float EndogenousRecency, float ExogenousRecency, float DecayImpact) CalculateSyntheticFailureSignals(
        Queue<(DateTime Date, FailureReasonKind Kind)> failures,
        DateTime targetDate)
    {
        var recentFailures = failures.Where(f => f.Date >= targetDate.AddDays(-14) && f.Date < targetDate).ToList();
        var denominator = 14f;
        var endogenousRate = recentFailures.Count(f => f.Kind == FailureReasonKind.Endogenous) / denominator;
        var exogenousRate = recentFailures.Count(f => f.Kind == FailureReasonKind.Exogenous) / denominator;
        var mixedRate = recentFailures.Count(f => f.Kind == FailureReasonKind.Mixed) / denominator;
        float endogenousRecency = 0f;
        float exogenousRecency = 0f;
        float decayImpact = 0f;

        foreach (var failure in failures.Where(f => f.Date < targetDate))
        {
            var daysAgo = Math.Max(0, (targetDate.Date - failure.Date.Date).TotalDays);
            var decayed = (float)(GetFailureSeverity(failure.Kind) * Math.Exp(-GetFailureDecayLambda(failure.Kind) * daysAgo));
            decayImpact += decayed;

            if (failure.Kind == FailureReasonKind.Endogenous)
            {
                endogenousRecency = Math.Max(endogenousRecency, (float)Math.Exp(-0.05 * daysAgo));
            }
            else if (failure.Kind == FailureReasonKind.Exogenous)
            {
                exogenousRecency = Math.Max(exogenousRecency, (float)Math.Exp(-0.5 * daysAgo));
            }
        }

        return (
            Math.Clamp(endogenousRate, 0f, 1f),
            Math.Clamp(exogenousRate, 0f, 1f),
            Math.Clamp(mixedRate, 0f, 1f),
            endogenousRecency,
            exogenousRecency,
            Math.Clamp(decayImpact, 0f, 3f)
        );
    }

    private static float CalculateQueueRate(Queue<bool> outcomes, int takeLast)
    {
        if (outcomes.Count == 0) return 0.5f;
        var slice = outcomes.Reverse().Take(takeLast).ToList();
        return slice.Count == 0 ? 0.5f : slice.Count(v => v) / (float)slice.Count;
    }

    private static float CalculateSyntheticDaysSinceLast(Queue<bool> outcomes, bool target)
    {
        var days = 1;
        foreach (var outcome in outcomes.Reverse())
        {
            if (outcome == target) return Math.Min(days, 60);
            days++;
        }
        return 60f;
    }

    private static float CalculateSyntheticConsecutiveMisses(Queue<bool> outcomes)
    {
        var misses = 0;
        foreach (var outcome in outcomes.Reverse())
        {
            if (outcome) break;
            misses++;
        }
        return misses;
    }

    private static float CalculateSyntheticWeekdayRate(Queue<bool> outcomes, DateTime targetDate)
    {
        if (outcomes.Count == 0) return 0.5f;
        var values = outcomes.ToList();
        var selected = new List<bool>();
        for (var i = 0; i < values.Count; i++)
        {
            var dayOffset = values.Count - i;
            if (targetDate.AddDays(-dayOffset).DayOfWeek == targetDate.DayOfWeek)
            {
                selected.Add(values[i]);
            }
        }

        return selected.Count == 0 ? CalculateQueueRate(outcomes, 14) : selected.Count(v => v) / (float)selected.Count;
    }

    private static float CalculateSyntheticContextDrift(string persona, DateTime day, DateTime habitStart, Random random)
    {
        var ageDays = (day.Date - habitStart.Date).TotalDays;
        var wave = persona == "swingy" ? Math.Abs(Math.Sin(ageDays / 5.0)) * 3.2 : 0.0;
        var transition = persona == "early_dropout" && ageDays > 18 ? 1.8 : 0.0;
        var noise = random.NextDouble() * 0.45;
        return (float)Math.Clamp(wave + transition + noise, 0, 6);
    }

    private static float CalculateSyntheticCueStrength(string persona, Queue<int> successHours, Random random)
    {
        if (successHours.Count < 2) return persona == "evening_logger" ? 1.8f : 8f;
        if (persona == "evening_logger") return 1.2f + (float)random.NextDouble();
        if (persona == "stable") return 2.0f + (float)random.NextDouble() * 1.5f;
        if (persona == "retroactive_logger") return 8f + (float)random.NextDouble() * 4f;
        return 4f + (float)random.NextDouble() * 5f;
    }

    private static List<SyntheticTrainingExample> SampleTrainingRows(List<SyntheticTrainingExample> rows, int limit)
    {
        if (rows.Count <= limit) return rows;

        var random = new Random(20260512);
        var positives = rows.Where(r => r.Input.Label).OrderBy(_ => random.Next()).ToList();
        var negatives = rows.Where(r => !r.Input.Label).OrderBy(_ => random.Next()).ToList();
        var targetNegativeTake = Math.Min(negatives.Count, (int)Math.Round(limit * 0.55));
        var positiveTake = Math.Min(positives.Count, limit - targetNegativeTake);
        var sampled = negatives.Take(targetNegativeTake).Concat(positives.Take(positiveTake)).ToList();

        if (sampled.Count < limit)
        {
            var used = sampled.ToHashSet();
            sampled.AddRange(rows.Where(r => !used.Contains(r)).OrderBy(_ => random.Next()).Take(limit - sampled.Count));
        }

        return sampled.OrderBy(_ => random.Next()).ToList();
    }

    private static List<SyntheticHabitTemplate> BuildSyntheticHabitTemplates()
    {
        var allDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var weekDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var mwf = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var tts = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday };

        return new List<SyntheticHabitTemplate>
        {
            new("Morning exercise", "Health", 1, allDays, 0.72, 0.14, 0.95, 0.70, 0.04, 0.03),
            new("Read 30 minutes", "Learning", 1, allDays, 0.68, 0.08, 0.70, 0.60, 0.05, 0.04),
            new("Drink water", "Health", 8, allDays, 0.58, 0.06, 0.55, 0.40, 0.72, 0.02),
            new("Take vitamins", "Health", 2, allDays, 0.76, 0.05, 0.35, 0.35, 0.20, 0.02),
            new("English practice", "Learning", 1, mwf, 0.70, 0.04, 0.62, 0.70, 0.05, 0.05),
            new("Budget check", "Finance", 1, weekDays, 0.64, 0.02, 0.45, 0.55, 0.04, 0.03),
            new("Evening walk", "Health", 1, tts, 0.66, 0.18, 0.80, 0.85, 0.04, 0.04),
            new("Sketching", "Creativity", 3, tts, 0.52, 0.02, 0.72, 0.55, 0.42, 0.09),
            new("No sweets", "Health", 1, allDays, 0.61, 0.26, 1.05, 0.60, 0.02, 0.05),
            new("Stretch back", "Health", 1, mwf, 0.62, 0.05, 0.88, 0.55, 0.04, 0.04),
            new("Meditation", "Mind", 1, allDays, 0.57, 0.12, 0.78, 0.70, 0.04, 0.06),
            new("Plan tomorrow", "Productivity", 1, weekDays, 0.67, 0.03, 0.50, 0.45, 0.03, 0.03),
            new("Deep work pomodoro", "Productivity", 2, weekDays, 0.60, 0.03, 1.20, 0.80, 0.35, 0.07),
            new("Sleep before midnight", "Health", 1, allDays, 0.54, 0.30, 1.15, 0.90, 0.02, 0.08),
            new("Journal mood", "Mind", 1, allDays, 0.59, 0.11, 0.65, 0.60, 0.04, 0.05),
            new("Clean desk", "Home", 1, weekDays, 0.63, 0.05, 0.55, 0.45, 0.03, 0.04),
            new("Cook healthy meal", "Health", 1, allDays, 0.56, 0.20, 0.95, 0.85, 0.06, 0.06),
            new("Call family", "Relationships", 1, new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Wednesday }, 0.69, -0.06, 0.35, 0.40, 0.03, 0.02),
            new("Study notes", "Learning", 2, weekDays, 0.58, 0.04, 1.00, 0.90, 0.35, 0.08),
            new("Limit social media", "Mind", 1, allDays, 0.50, 0.22, 1.05, 0.70, 0.03, 0.10),
            new("Weekly review", "Productivity", 1, new List<DayOfWeek> { DayOfWeek.Sunday }, 0.64, 0.00, 0.50, 0.45, 0.03, 0.04)
        };
    }

    private static double GetPersonaBaseReliability(string persona)
    {
        return persona switch
        {
            "stable" => 0.82,
            "swingy" => 0.63,
            "lazy" => 0.48,
            "weekend_vulnerable" => 0.66,
            "evening_logger" => 0.68,
            "overloaded" => 0.58,
            "retroactive_logger" => 0.62,
            "early_dropout" => 0.55,
            "fast_recovery" => 0.70,
            "anxious_underconfident" => 0.69,
            "optimistic_overconfident" => 0.59,
            "seasonal_student" => 0.65,
            "busy_parent" => 0.61,
            "health_rebuilder" => 0.58,
            _ => 0.62
        };
    }

    private static double CalculateSyntheticSuccessProbability(
        string persona,
        double habitReliability,
        double userReliability,
        bool isWeekend,
        float currentStreak,
        float recentRate,
        float fatigue,
        float contextDrift,
        float failureDecayImpact,
        DateTime day,
        DateTime startDate,
        SyntheticHabitTemplate? template = null,
        SyntheticPersonaProfile? profile = null,
        SyntheticLifeEvent? activeEvent = null)
    {
        var ageDays = Math.Max(0, (day.Date - startDate.Date).TotalDays);
        var score = -0.35
            + habitReliability * 1.5
            + userReliability * 1.1
            + (recentRate - 0.5) * 1.8
            + Math.Min(currentStreak, 21) * 0.035
            - fatigue * 0.045 * (float)(profile?.FatigueSensitivity ?? 1.0) * (float)(template?.FatigueSensitivity ?? 1.0)
            - contextDrift * 0.045 * (float)(template?.DriftSensitivity ?? 1.0)
            - failureDecayImpact * 0.42
            + Math.Log(ageDays + 1) * 0.05;

        if (isWeekend) score -= (profile?.WeekendSensitivity ?? (persona == "weekend_vulnerable" ? 1.0 : 0.16)) * (template?.WeekendPenalty ?? 0.12);
        if (activeEvent != null) score += activeEvent.Impact;
        if (persona == "overloaded" && fatigue > 8) score -= 0.8;
        if (persona == "early_dropout" && ageDays > 21) score -= 0.7;
        if (persona == "fast_recovery" && failureDecayImpact > 0.3) score += 0.35;
        if (persona == "health_rebuilder" && template?.Category == "Health" && ageDays > 12) score += 0.22;
        if (persona == "seasonal_student" && day.Month is 5 or 6 && template?.Category == "Learning") score -= 0.45;
        if (persona == "busy_parent" && isWeekend && template?.Category == "Productivity") score -= 0.35;
        if (persona == "swingy") score += Math.Sin(ageDays / 4.0) * 0.65;
        if (IsHolidayStatic(day)) score -= persona == "stable" ? 0.12 : 0.45;

        return Math.Clamp(1.0 / (1.0 + Math.Exp(-score)), 0.04, 0.98);
    }

    private static string PickSyntheticFailureReason(Random random, string persona, bool isWeekend, float fatigue, bool isHoliday)
    {
        if (persona == "lazy" && random.NextDouble() < 0.55) return "Лінь / Прокрастинація";
        if (persona == "weekend_vulnerable" && isWeekend && random.NextDouble() < 0.50) return "Свято / Гості";
        if (persona == "overloaded" && fatigue > 8 && random.NextDouble() < 0.55) return "Втома / Стрес";
        if (isHoliday && random.NextDouble() < 0.55) return "Свято / Гості";

        var reasons = new[]
        {
            "Втома / Стрес",
            "Брак часу",
            "Лінь / Прокрастинація",
            "Забув(ла)",
            "Хвороба",
            "Свято / Гості"
        };
        return reasons[random.Next(reasons.Length)];
    }

    private static bool IsHolidayStatic(DateTime date)
    {
        var holidays = new HashSet<(int Month, int Day)>
        {
            (1, 1), (3, 8), (5, 1), (5, 8), (6, 28), (7, 15), (7, 28), (8, 24), (10, 1), (12, 25)
        };
        return holidays.Contains((date.Month, date.Day));
    }

    private static double CalculateAccuracy(List<bool> labels, List<double> probabilities, double threshold = 0.5)
    {
        if (!labels.Any()) return 0;
        var correct = labels.Zip(probabilities).Count(p => p.First == (p.Second >= threshold));
        return correct / (double)labels.Count;
    }

    private static (double Threshold, double Accuracy) FindBestAccuracyThreshold(List<bool> labels, List<double> probabilities)
    {
        if (!labels.Any()) return (0.5, 0);

        var pairs = labels
            .Zip(probabilities, (label, probability) => (label, probability))
            .OrderByDescending(p => p.probability)
            .ToList();

        var correct = pairs.Count(p => !p.label);
        var bestCorrect = correct;
        var bestThreshold = 1.000001;

        for (var i = 0; i < pairs.Count;)
        {
            var j = i + 1;
            while (j < pairs.Count && Math.Abs(pairs[j].probability - pairs[i].probability) < 1e-12) j++;

            for (var k = i; k < j; k++)
            {
                correct += pairs[k].label ? 1 : -1;
            }

            if (correct > bestCorrect)
            {
                bestCorrect = correct;
                bestThreshold = pairs[i].probability;
            }

            i = j;
        }

        return (bestThreshold, bestCorrect / (double)pairs.Count);
    }

    private static double CalculateBrierScore(List<bool> labels, List<double> probabilities)
    {
        if (!labels.Any()) return 0;
        return labels.Zip(probabilities).Average(p =>
        {
            var y = p.First ? 1.0 : 0.0;
            return Math.Pow(p.Second - y, 2);
        });
    }

    private static double CalculateLogLoss(List<bool> labels, List<double> probabilities)
    {
        if (!labels.Any()) return 0;
        return labels.Zip(probabilities).Average(p =>
        {
            var y = p.First ? 1.0 : 0.0;
            var prob = Math.Clamp(p.Second, 0.000001, 0.999999);
            return -(y * Math.Log(prob) + (1 - y) * Math.Log(1 - prob));
        });
    }

    private static double CalculateRocAuc(List<bool> labels, List<double> probabilities)
    {
        var pairs = labels.Zip(probabilities, (label, probability) => (label, probability))
            .OrderBy(p => p.probability)
            .ToList();
        var positives = pairs.Count(p => p.label);
        var negatives = pairs.Count - positives;
        if (positives == 0 || negatives == 0) return 0.5;

        double rankSum = 0;
        var rank = 1;
        for (var i = 0; i < pairs.Count;)
        {
            var j = i + 1;
            while (j < pairs.Count && Math.Abs(pairs[j].probability - pairs[i].probability) < 1e-12) j++;
            var averageRank = (rank + rank + (j - i) - 1) / 2.0;
            for (var k = i; k < j; k++)
            {
                if (pairs[k].label) rankSum += averageRank;
            }
            rank += j - i;
            i = j;
        }

        var positiveCount = (double)positives;
        var negativeCount = (double)negatives;
        return (rankSum - positiveCount * (positiveCount + 1) / 2.0) / (positiveCount * negativeCount);
    }

    private static double CalculateExpectedCalibrationError(List<bool> labels, List<double> probabilities, int bins = 10)
    {
        if (!labels.Any()) return 0;

        double ece = 0;
        for (var bin = 0; bin < bins; bin++)
        {
            var lower = bin / (double)bins;
            var upper = (bin + 1) / (double)bins;
            var binPairs = labels.Zip(probabilities, (label, probability) => (label, probability))
                .Where(p => p.probability >= lower && (bin == bins - 1 ? p.probability <= upper : p.probability < upper))
                .ToList();

            if (!binPairs.Any()) continue;

            var confidence = binPairs.Average(p => p.probability);
            var accuracy = binPairs.Average(p => p.label ? 1.0 : 0.0);
            ece += binPairs.Count / (double)labels.Count * Math.Abs(accuracy - confidence);
        }

        return ece;
    }

    private void TuneBlendAndCalibration(List<HabitModelInput> validationInputs, List<ExternalHabitEnsembleOutput> externalPredictions)
    {
        if (!validationInputs.Any())
        {
            SetCalibration(0, 1, 0.45);
            return;
        }

        var labels = validationInputs.Select(i => i.Label).ToList();
        var mlProbabilities = validationInputs
            .Select(input => PredictWithEnsemble(input, allowExternalInference: false, applyCalibration: false).Probability)
            .ToList();

        var hasStacked = externalPredictions.Count == validationInputs.Count && externalPredictions.Any(p => p.StackedProbability.HasValue);
        var bestWeight = 0.0;
        var blended = mlProbabilities;

        if (hasStacked)
        {
            var stackedProbabilities = externalPredictions
                .Select(p => Math.Clamp((double)(p.StackedProbability ?? p.ModelProbabilities.Values.FirstOrDefault()), 0.000001, 0.999999))
                .ToList();

            var bestLoss = double.PositiveInfinity;
            for (var step = 0; step <= 100; step++)
            {
                var weight = step / 100.0;
                var candidate = mlProbabilities
                    .Zip(stackedProbabilities, (ml, stacked) => Math.Clamp(ml * (1 - weight) + stacked * weight, 0.000001, 0.999999))
                    .ToList();
                var loss = CalculateLogLoss(labels, candidate);
                if (loss < bestLoss)
                {
                    bestLoss = loss;
                    bestWeight = weight;
                    blended = candidate;
                }
            }
        }

        var calibration = FitPlattCalibration(labels, blended);
        SetCalibration(calibration.Intercept, calibration.Slope, bestWeight);
    }

    private void SetCalibration(double intercept, double slope, double stackedWeight)
    {
        lock (_trainingLock)
        {
            _calibrationIntercept = intercept;
            _calibrationSlope = slope;
            _stackedPythonBlendWeight = stackedWeight;
        }
    }

    private static (double Intercept, double Slope) FitPlattCalibration(List<bool> labels, List<double> probabilities)
    {
        if (labels.Count < 20 || labels.Distinct().Count() < 2) return (0, 1);

        var x = probabilities.Select(p => Logit(Math.Clamp(p, 0.000001, 0.999999))).ToList();
        var a = 0.0;
        var b = 1.0;

        for (var iteration = 0; iteration < 40; iteration++)
        {
            double g0 = 0, g1 = 0, h00 = 1e-6, h01 = 0, h11 = 1e-6;
            for (var i = 0; i < labels.Count; i++)
            {
                var z = a + b * x[i];
                var p = Sigmoid(z);
                var y = labels[i] ? 1.0 : 0.0;
                var diff = p - y;
                var w = Math.Max(p * (1 - p), 1e-6);
                g0 += diff;
                g1 += diff * x[i];
                h00 += w;
                h01 += w * x[i];
                h11 += w * x[i] * x[i];
            }

            var det = h00 * h11 - h01 * h01;
            if (Math.Abs(det) < 1e-10) break;

            var da = (h11 * g0 - h01 * g1) / det;
            var db = (-h01 * g0 + h00 * g1) / det;
            a -= Math.Clamp(da, -2, 2);
            b -= Math.Clamp(db, -2, 2);

            if (Math.Abs(da) + Math.Abs(db) < 1e-5) break;
        }

        return (Math.Clamp(a, -8, 8), Math.Clamp(b, 0.05, 5));
    }

    private static double ApplyCalibration(double probability, double intercept, double slope)
    {
        var clamped = Math.Clamp(probability, 0.000001, 0.999999);
        return Math.Clamp(Sigmoid(intercept + slope * Logit(clamped)), 0.000001, 0.999999);
    }

    private static double Logit(double probability)
    {
        return Math.Log(probability / (1 - probability));
    }

    private static double Sigmoid(double value)
    {
        if (value >= 0)
        {
            var exp = Math.Exp(-value);
            return 1 / (1 + exp);
        }

        var negExp = Math.Exp(value);
        return negExp / (1 + negExp);
    }

    private static Dictionary<string, SyntheticPersonaMetric> CalculatePersonaMetrics(List<SyntheticTrainingExample> examples, List<double> probabilities)
    {
        return examples
            .Zip(probabilities, (example, probability) => (example, probability))
            .GroupBy(p => p.example.Persona)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var labels = g.Select(p => p.example.Input.Label).ToList();
                    var probs = g.Select(p => p.probability).ToList();
                    var threshold = FindBestAccuracyThreshold(labels, probs);
                    return new SyntheticPersonaMetric
                    {
                        Rows = labels.Count,
                        Accuracy = Math.Round(threshold.Accuracy, 4),
                        DefaultThresholdAccuracy = Math.Round(CalculateAccuracy(labels, probs), 4),
                        DecisionThreshold = Math.Round(threshold.Threshold, 4),
                        RocAuc = Math.Round(CalculateRocAuc(labels, probs), 4),
                        BrierScore = Math.Round(CalculateBrierScore(labels, probs), 4),
                        LogLoss = Math.Round(CalculateLogLoss(labels, probs), 4),
                        ExpectedCalibrationError = Math.Round(CalculateExpectedCalibrationError(labels, probs), 4)
                    };
                });
    }

    private void TryAddTrainedModel(
        List<WeightedHabitModel> models,
        string name,
        double weight,
        Func<ITransformer> train)
    {
        try
        {
            models.Add(new WeightedHabitModel(name, weight, train()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model training failed for {ModelName}.", name);
        }
    }

    private IEstimator<ITransformer> BuildFeaturePipeline()
    {
        return _mlContext.Transforms.Categorical.OneHotEncoding("MaturityBucketEncoded", "MaturityBucket")
            .Append(_mlContext.Transforms.Categorical.OneHotEncoding("HabitCategoryEncoded", "HabitCategory"))
            .Append(_mlContext.Transforms.Categorical.OneHotEncoding("PreferredTimeOfDayEncoded", "PreferredTimeOfDay"))
            .Append(_mlContext.Transforms.Categorical.OneHotEncoding("InitialFailureReasonEncoded", "InitialFailureReason"))
            .Append(_mlContext.Transforms.Concatenate("Features",
                "IsWeekend", "TargetCount", "CurrentStreak", "Momentum", "FatigueScore", "StreakVolatility", "RecentSuccessRate", "UserOverallRate", "ContextDriftScore", "RecoverySpeed", "LogHabitAge", "SynergyScore", "IsHoliday", "SinDayOfYear", "CosDayOfYear", "RetroactiveLogRatio", "CueStrengthScore", "EndogenousFailureRate14d", "ExogenousFailureRate14d", "MixedFailureRate14d", "EndogenousFailureRecency", "ExogenousFailureRecency", "FailureDecayImpact", "SuccessRate3d", "SuccessRate7d", "SuccessRate30d", "DaysSinceLastSuccess", "DaysSinceLastFailure", "ConsecutiveMisses", "WeekdaySuccessRate", "WeekendSuccessGap", "ActiveDaysPerWeek", "ScheduleDensity", "IsDailyHabit", "IsMultiCountHabit", "IsWorkdayHabit", "WeekendRecentRisk", "FatigueRecentRisk", "DriftVolatilityRisk", "RecoveryPressure", "StreakMomentumSignal", "HabitIntensity", "SubjectiveDifficulty", "DisciplineSelf", "WeekendVulnerability", "Lag1Completed", "Lag2Completed", "Lag3Completed", "Lag4Completed", "Lag5Completed", "Lag6Completed", "Lag7Completed", "Lag8Completed", "Lag9Completed", "Lag10Completed", "Lag11Completed", "Lag12Completed", "Lag13Completed", "Lag14Completed", "MissPattern7d", "LagTrend14d", "MaturityBucketEncoded", "HabitCategoryEncoded", "PreferredTimeOfDayEncoded", "InitialFailureReasonEncoded"));
    }

    private static HabitModelInput PopulateDerivedFeatures(HabitModelInput input)
    {
        var recentMissRate = 1f - Math.Clamp(input.RecentSuccessRate, 0f, 1f);
        input.WeekendRecentRisk = input.IsWeekend * recentMissRate;
        input.FatigueRecentRisk = input.FatigueScore * recentMissRate;
        input.DriftVolatilityRisk = input.ContextDriftScore * input.StreakVolatility;
        input.RecoveryPressure = input.ConsecutiveMisses / (1f + Math.Max(0f, input.RecoverySpeed));
        input.StreakMomentumSignal = input.CurrentStreak + input.Momentum * 0.5f;
        input.HabitIntensity = input.TargetCount * input.ScheduleDensity;
        return input;
    }

    private static void ApplyUserProfileFeatures(HabitModelInput input, User? user)
    {
        input.SubjectiveDifficulty = user?.SubjectiveDifficulty ?? 5;
        input.DisciplineSelf = user?.OverallDisciplineSelf ?? 5;
        input.WeekendVulnerability = user?.WeekendVulnerability ?? 2;
        input.PreferredTimeOfDay = NormalizeChoiceFeature(user?.PreferredTimeOfDay, "flexible");
        input.InitialFailureReason = NormalizeChoiceFeature(user?.InitialFailureReason, "none");
    }

    private static void ApplyLagFeatures(HabitModelInput input, IEnumerable<bool> outcomesNewestFirst)
    {
        var lags = outcomesNewestFirst.Take(14).Select(v => v ? 1f : 0f).ToList();
        while (lags.Count < 14) lags.Add(0.5f);

        input.Lag1Completed = lags[0];
        input.Lag2Completed = lags[1];
        input.Lag3Completed = lags[2];
        input.Lag4Completed = lags[3];
        input.Lag5Completed = lags[4];
        input.Lag6Completed = lags[5];
        input.Lag7Completed = lags[6];
        input.Lag8Completed = lags[7];
        input.Lag9Completed = lags[8];
        input.Lag10Completed = lags[9];
        input.Lag11Completed = lags[10];
        input.Lag12Completed = lags[11];
        input.Lag13Completed = lags[12];
        input.Lag14Completed = lags[13];

        var first7 = lags.Take(7).Where(v => v != 0.5f).ToList();
        input.MissPattern7d = first7.Any() ? first7.Count(v => v == 0f) / (float)first7.Count : 0.5f;

        var recent = lags.Take(7).Where(v => v != 0.5f).DefaultIfEmpty(0.5f).Average();
        var older = lags.Skip(7).Take(7).Where(v => v != 0.5f).DefaultIfEmpty(0.5f).Average();
        input.LagTrend14d = (float)Math.Clamp(recent - older, -1.0, 1.0);
    }

    private static void ApplyLagFeatures(HabitModelInput input, List<HabitEntry> habitHistory, string? habitId, DateTime targetDate)
    {
        if (habitId == null)
        {
            ApplyLagFeatures(input, Array.Empty<bool>());
            return;
        }

        var outcomes = habitHistory
            .Where(e => e.HabitId == habitId && e.Date < targetDate)
            .OrderByDescending(e => e.Date)
            .Select(e => e.IsFullyCompleted);
        ApplyLagFeatures(input, outcomes);
    }

    private static string NormalizeChoiceFeature(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return value.Trim().ToLowerInvariant();
    }

    private static void ApplySyntheticProfileFeatures(HabitModelInput input, string persona)
    {
        input.SubjectiveDifficulty = persona switch
        {
            "stable" or "fast_recovery" => 3,
            "lazy" or "early_dropout" or "overloaded" => 8,
            "busy_parent" or "seasonal_student" or "swingy" => 7,
            _ => 5
        };
        input.DisciplineSelf = persona switch
        {
            "stable" => 8,
            "fast_recovery" or "anxious_underconfident" => 7,
            "lazy" or "early_dropout" => 3,
            "overloaded" or "busy_parent" => 5,
            _ => 6
        };
        input.WeekendVulnerability = persona == "weekend_vulnerable" ? 4 : persona is "busy_parent" or "overloaded" ? 3 : 2;
        input.PreferredTimeOfDay = persona switch
        {
            "evening_logger" => "evening",
            "seasonal_student" => "day",
            "stable" or "health_rebuilder" => "morning",
            _ => "flexible"
        };
        input.InitialFailureReason = persona switch
        {
            "lazy" or "early_dropout" => "motivation",
            "overloaded" or "busy_parent" => "time",
            "health_rebuilder" => "health",
            "weekend_vulnerable" => "events",
            "anxious_underconfident" => "mood",
            _ => "none"
        };
    }

    private static string NormalizeHabitCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Uncategorized";
        return category.Trim();
    }

    private static (float ActiveDaysPerWeek, float ScheduleDensity, float IsDailyHabit, float IsWorkdayHabit) CalculateScheduleFeatures(IReadOnlyCollection<DayOfWeek> activeDays)
    {
        var count = activeDays.Count;
        var workdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var isWorkdayOnly = count == 5 && workdays.All(activeDays.Contains);
        return (count, count / 7f, count == 7 ? 1f : 0f, isWorkdayOnly ? 1f : 0f);
    }

    private HabitEnsemblePrediction PredictWithEnsemble(
        HabitModelInput input,
        ExternalHabitEnsembleOutput? externalPrediction = null,
        bool allowExternalInference = false,
        bool applyCalibration = true)
    {
        List<CachedPredictionEngine> engines;
        bool externalReady;
        double stackedWeight;
        double calibrationIntercept;
        double calibrationSlope;
        lock (_trainingLock)
        {
            engines = _predictionEngines.ToList();
            externalReady = _externalEnsembleReady;
            stackedWeight = _stackedPythonBlendWeight;
            calibrationIntercept = _calibrationIntercept;
            calibrationSlope = _calibrationSlope;
        }

        if (!engines.Any() && !externalReady)
        {
            return new HabitEnsemblePrediction();
        }

        double weightedSum = 0;
        double totalWeight = 0;
        var modelProbabilities = new Dictionary<string, float>();

        foreach (var engine in engines)
        {
            HabitModelOutput result;
            lock (engine.SyncRoot)
            {
                result = engine.Engine.Predict(input);
            }
            var engineProbability = Math.Clamp(result.Probability, 0f, 1f);

            modelProbabilities[engine.Name] = engineProbability;
            weightedSum += engineProbability * engine.Weight;
            totalWeight += engine.Weight;
        }

        var mlProbability = totalWeight > 0 ? weightedSum / totalWeight : 0;
        var probability = mlProbability;

        if (externalReady && allowExternalInference)
        {
            externalPrediction ??= TryPredictExternalEnsemble(input);
            if (externalPrediction.StackedProbability.HasValue)
            {
                var stackedProbability = Math.Clamp(externalPrediction.StackedProbability.Value, 0f, 1f);
                modelProbabilities["StackedPython"] = stackedProbability;
                probability = mlProbability * (1 - stackedWeight) + stackedProbability * stackedWeight;
            }
            else
            {
                var externalSum = 0.0;
                var externalCount = 0;
                foreach (var (modelName, externalModelProbability) in externalPrediction.ModelProbabilities)
                {
                    var clampedProbability = Math.Clamp(externalModelProbability, 0f, 1f);
                    modelProbabilities[modelName] = clampedProbability;
                    externalSum += clampedProbability;
                    externalCount++;
                }

                if (externalCount > 0)
                {
                    var externalAverage = externalSum / externalCount;
                    probability = mlProbability * (1 - stackedWeight) + externalAverage * stackedWeight;
                }
            }
        }

        if (applyCalibration)
        {
            probability = ApplyCalibration(probability, calibrationIntercept, calibrationSlope);
        }

        return new HabitEnsemblePrediction
        {
            Probability = probability,
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

        var result = await RunPythonEnsembleAsync(scriptPath, "train", trainingDataPath, modelPath, timeoutMilliseconds: 600000);
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
            var timeout = Math.Clamp(12000 + inputs.Count * 15, 12000, 60000);
            var result = RunPythonEnsembleAsync(scriptPath, "predict", modelPath, inputPath, timeoutMilliseconds: timeout)
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

    private async Task<(bool Success, string Output)> RunPythonEnsembleAsync(
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
                _logger.LogWarning("Python ensemble command {Command} timed out after {Timeout} ms.", command, timeoutMilliseconds);
                return (false, string.Empty);
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Python ensemble command {Command} failed with exit code {ExitCode}: {Error}", command, process.ExitCode, stderr);
            }

            return (process.ExitCode == 0, stdout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python ensemble command {Command} could not be started.", command);
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
        var ensemblePrediction = PredictWithEnsemble(input, allowExternalInference: UsePythonRuntimeInferenceForUsers);
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
        var users = await _usersCollection.Find(u => userIds.Contains(u.Id!)).ToListAsync();
        var usersById = users.Where(u => u.Id != null).ToDictionary(u => u.Id!, u => u);

        var inputs = new List<(string HabitId, HabitModelInput Input)>();
        foreach (var habit in habits)
        {
            if (habit.Id == null) continue;
            entriesByUser.TryGetValue(habit.UserId, out var userEntries);
            entriesByHabit.TryGetValue(habit.Id, out var habitEntries);
            usersById.TryGetValue(habit.UserId, out var user);

            inputs.Add((
                habit.Id,
                BuildPredictionInputCached(habit, habitEntries ?? new List<HabitEntry>(), userEntries ?? new List<HabitEntry>(), user)
            ));
        }

        var uncachedInputs = inputs
            .Where(i => !_memoryCache.TryGetValue(BuildPredictionCacheKey(i.Input), out HabitEnsemblePrediction? _))
            .ToList();
        var externalPredictions = UsePythonRuntimeInferenceForUsers
            ? TryPredictExternalEnsembleBatch(uncachedInputs.Select(i => i.Input).ToList())
            : new List<ExternalHabitEnsembleOutput>();
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

            var ensemblePrediction = PredictWithEnsemble(
                inputs[i].Input,
                externalPrediction,
                allowExternalInference: UsePythonRuntimeInferenceForUsers);
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
        var user = await _usersCollection.Find(u => u.Id == habit.UserId).FirstOrDefaultAsync();
        return BuildPredictionInputCached(habit, habitHistory, allUserHistory, user);
    }

    private HabitModelInput BuildPredictionInputCached(Habit habit, List<HabitEntry> habitHistory, List<HabitEntry> allUserHistory, User? user = null)
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
        var failureSignals = CalculateFailureSignals(habitHistory, habit, today);
        var temporalSignals = CalculateTemporalSignals(habitHistory, habit, today);
        var scheduleFeatures = CalculateScheduleFeatures(habit.ActiveDays);

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
            CueStrengthScore = cueStrength,
            EndogenousFailureRate14d = failureSignals.EndogenousRate14d,
            ExogenousFailureRate14d = failureSignals.ExogenousRate14d,
            MixedFailureRate14d = failureSignals.MixedRate14d,
            EndogenousFailureRecency = failureSignals.EndogenousRecency,
            ExogenousFailureRecency = failureSignals.ExogenousRecency,
            FailureDecayImpact = failureSignals.DecayImpact,
            SuccessRate3d = temporalSignals.SuccessRate3d,
            SuccessRate7d = temporalSignals.SuccessRate7d,
            SuccessRate30d = temporalSignals.SuccessRate30d,
            DaysSinceLastSuccess = temporalSignals.DaysSinceLastSuccess,
            DaysSinceLastFailure = temporalSignals.DaysSinceLastFailure,
            ConsecutiveMisses = temporalSignals.ConsecutiveMisses,
            WeekdaySuccessRate = temporalSignals.WeekdaySuccessRate,
            WeekendSuccessGap = temporalSignals.WeekendSuccessGap,
            ActiveDaysPerWeek = scheduleFeatures.ActiveDaysPerWeek,
            ScheduleDensity = scheduleFeatures.ScheduleDensity,
            IsDailyHabit = scheduleFeatures.IsDailyHabit,
            IsMultiCountHabit = habit.TargetCount > 1 ? 1f : 0f,
            IsWorkdayHabit = scheduleFeatures.IsWorkdayHabit,
            HabitCategory = NormalizeHabitCategory(habit.Category)
        };

        ApplyLagFeatures(input, habitHistory, habit.Id, today);
        ApplyUserProfileFeatures(input, user);
        input = PopulateDerivedFeatures(input);
        _memoryCache.Set(featureCacheKey, input, FeatureCacheTtl);
        return input;
    }

    private string BuildFeatureCacheKey(Habit habit)
    {
        var version = _userCacheVersions.GetValueOrDefault(habit.UserId, 0);
        return $"habit_features:{habit.UserId}:{habit.Id}:{DateTime.UtcNow.Date:yyyyMMdd}:{version}";
    }

    private (float SuccessRate3d, float SuccessRate7d, float SuccessRate30d, float DaysSinceLastSuccess, float DaysSinceLastFailure, float ConsecutiveMisses, float WeekdaySuccessRate, float WeekendSuccessGap) CalculateTemporalSignals(
        List<HabitEntry> habitHistory,
        Habit habit,
        DateTime targetDate)
    {
        var habitId = habit.Id;
        if (habitId == null) return (0.5f, 0.5f, 0.5f, 60f, 60f, 0f, 0.5f, 0f);

        var successRate3d = CalculateRecentRate(habitHistory, habitId, targetDate, 3);
        var successRate7d = CalculateRecentRate(habitHistory, habitId, targetDate, 7);
        var successRate30d = CalculateRecentRate(habitHistory, habitId, targetDate, 30);

        var ordered = habitHistory
            .Where(e => e.HabitId == habitId && e.Date < targetDate)
            .OrderByDescending(e => e.Date)
            .ToList();

        var lastSuccess = ordered.FirstOrDefault(e => e.IsFullyCompleted);
        var lastFailure = ordered.FirstOrDefault(e => !e.IsFullyCompleted);
        var daysSinceLastSuccess = lastSuccess == null ? 60f : (float)Math.Clamp((targetDate.Date - lastSuccess.Date.Date).TotalDays, 0, 60);
        var daysSinceLastFailure = lastFailure == null ? 60f : (float)Math.Clamp((targetDate.Date - lastFailure.Date.Date).TotalDays, 0, 60);

        var consecutiveMisses = 0f;
        foreach (var entry in ordered)
        {
            if (entry.IsFullyCompleted) break;
            consecutiveMisses++;
        }

        var sameWeekday = ordered
            .Where(e => e.Date.DayOfWeek == targetDate.DayOfWeek)
            .Take(8)
            .ToList();
        var weekdaySuccessRate = sameWeekday.Any()
            ? sameWeekday.Count(e => e.IsFullyCompleted) / (float)sameWeekday.Count
            : successRate30d;

        var recent = ordered.Take(30).ToList();
        var weekendEntries = recent.Where(e => e.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).ToList();
        var weekdayEntries = recent.Where(e => e.Date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).ToList();
        var weekendRate = weekendEntries.Any() ? weekendEntries.Count(e => e.IsFullyCompleted) / (float)weekendEntries.Count : successRate30d;
        var weekdayRate = weekdayEntries.Any() ? weekdayEntries.Count(e => e.IsFullyCompleted) / (float)weekdayEntries.Count : successRate30d;
        var weekendSuccessGap = Math.Clamp(weekdayRate - weekendRate, -1f, 1f);

        return (successRate3d, successRate7d, successRate30d, daysSinceLastSuccess, daysSinceLastFailure, Math.Clamp(consecutiveMisses, 0f, 30f), weekdaySuccessRate, weekendSuccessGap);
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
        if (f.FailureDecayImpact > 0.7f) neg.Add("недавні зриви з вагомих причин 🟡");

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
            "EndogenousFailureRate14d" => "внутрішні причини зривів",
            "ExogenousFailureRate14d" => "зовнішні причини зривів",
            "MixedFailureRate14d" => "змішані причини зривів",
            "EndogenousFailureRecency" => "недавній внутрішній зрив",
            "ExogenousFailureRecency" => "недавній зовнішній зрив",
            "FailureDecayImpact" => "згасаючий вплив причин зривів",
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

    private (float EndogenousRate14d, float ExogenousRate14d, float MixedRate14d, float EndogenousRecency, float ExogenousRecency, float DecayImpact) CalculateFailureSignals(
        List<HabitEntry> habitHistory,
        Habit habit,
        DateTime targetDate)
    {
        var habitId = habit.Id;
        if (habitId == null) return (0f, 0f, 0f, 0f, 0f, 0f);

        var recentStart = targetDate.Date.AddDays(-14);
        var targetDays = CountActiveDays(habit.ActiveDays, recentStart, targetDate.Date);
        if (targetDays <= 0) targetDays = 1;

        var recentFailures = habitHistory
            .Where(e => e.HabitId == habitId && e.Date >= recentStart && e.Date < targetDate && !e.IsFullyCompleted && !string.IsNullOrWhiteSpace(e.FailureReason))
            .ToList();

        var endogenousCount = 0;
        var exogenousCount = 0;
        var mixedCount = 0;

        foreach (var failure in recentFailures)
        {
            switch (ClassifyFailureReason(failure.FailureReason))
            {
                case FailureReasonKind.Endogenous:
                    endogenousCount++;
                    break;
                case FailureReasonKind.Exogenous:
                    exogenousCount++;
                    break;
                case FailureReasonKind.Mixed:
                    mixedCount++;
                    break;
            }
        }

        var decayStart = targetDate.Date.AddDays(-60);
        var decayFailures = habitHistory
            .Where(e => e.HabitId == habitId && e.Date >= decayStart && e.Date < targetDate && !e.IsFullyCompleted && !string.IsNullOrWhiteSpace(e.FailureReason))
            .ToList();

        float endogenousRecency = 0f;
        float exogenousRecency = 0f;
        float decayImpact = 0f;

        foreach (var failure in decayFailures)
        {
            var daysAgo = Math.Max(0, (targetDate.Date - failure.Date.Date).TotalDays);
            var kind = ClassifyFailureReason(failure.FailureReason);
            var lambda = GetFailureDecayLambda(kind);
            var severity = GetFailureSeverity(kind);
            var decayed = (float)(severity * Math.Exp(-lambda * daysAgo));
            decayImpact += decayed;

            if (kind == FailureReasonKind.Endogenous)
            {
                endogenousRecency = Math.Max(endogenousRecency, (float)Math.Exp(-0.05 * daysAgo));
            }
            else if (kind == FailureReasonKind.Exogenous)
            {
                exogenousRecency = Math.Max(exogenousRecency, (float)Math.Exp(-0.5 * daysAgo));
            }
        }

        return (
            Math.Clamp((float)endogenousCount / targetDays, 0f, 1f),
            Math.Clamp((float)exogenousCount / targetDays, 0f, 1f),
            Math.Clamp((float)mixedCount / targetDays, 0f, 1f),
            endogenousRecency,
            exogenousRecency,
            Math.Clamp(decayImpact, 0f, 3f)
        );
    }

    private static int CountActiveDays(List<DayOfWeek> activeDays, DateTime start, DateTime endExclusive)
    {
        var count = 0;
        for (var day = start.Date; day < endExclusive.Date; day = day.AddDays(1))
        {
            if (activeDays.Contains(day.DayOfWeek)) count++;
        }
        return count;
    }

    private enum FailureReasonKind
    {
        None,
        Endogenous,
        Exogenous,
        Mixed
    }

    private static FailureReasonKind ClassifyFailureReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return FailureReasonKind.None;

        var normalized = reason.Trim().ToLowerInvariant();
        if (ContainsAny(normalized, "лін", "прокраст", "забув", "lazy", "procrast", "forgot"))
        {
            return FailureReasonKind.Endogenous;
        }

        if (ContainsAny(normalized, "хвор", "свят", "гост", "подорож", "поїзд", "ill", "sick", "holiday", "guest", "travel"))
        {
            return FailureReasonKind.Exogenous;
        }

        if (ContainsAny(normalized, "втом", "стрес", "брак часу", "час", "настр", "fatigue", "stress", "time", "mood"))
        {
            return FailureReasonKind.Mixed;
        }

        return FailureReasonKind.Mixed;
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        return patterns.Any(value.Contains);
    }

    private static double GetFailureDecayLambda(FailureReasonKind kind)
    {
        return kind switch
        {
            FailureReasonKind.Endogenous => 0.05,
            FailureReasonKind.Exogenous => 0.50,
            FailureReasonKind.Mixed => 0.15,
            _ => 0.20
        };
    }

    private static double GetFailureSeverity(FailureReasonKind kind)
    {
        return kind switch
        {
            FailureReasonKind.Endogenous => 1.00,
            FailureReasonKind.Mixed => 0.70,
            FailureReasonKind.Exogenous => 0.35,
            _ => 0.50
        };
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
