using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HabitTracker.Api.Models;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool OnboardingCompleted { get; set; } = false;
    public int? SubjectiveDifficulty { get; set; }
    public int? OverallDisciplineSelf { get; set; }
    public int? WeekendVulnerability { get; set; }
    public string? PreferredTimeOfDay { get; set; }
    public string? InitialFailureReason { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }
}
