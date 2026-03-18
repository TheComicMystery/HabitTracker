using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HabitTracker.Api.Models;

public class HabitEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string HabitId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public DateTime Date { get; set; } 
    public int CompletedCount { get; set; } = 0;
    public bool IsFullyCompleted { get; set; } = false; 
    public string? FailureReason { get; set; }
    public int? ConfidenceScore { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}