using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HabitTracker.Api.Models;

[BsonIgnoreExtraElements]
public class ActiveLearningPrompt
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string HabitId { get; set; } = null!;

    public DateTime ShownAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}
