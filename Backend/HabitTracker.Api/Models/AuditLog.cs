using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HabitTracker.Api.Models;

[BsonIgnoreExtraElements]
public class AuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public string Method { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string ResourceType { get; set; } = "unknown";
    public string? ResourceId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public int StatusCode { get; set; }
}
