using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HabitTracker.Api.Models;

[BsonIgnoreExtraElements]
public class PomodoroSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? HabitId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public string Mode { get; set; } = "pomodoro";
    public bool Completed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
