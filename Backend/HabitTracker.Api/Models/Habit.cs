using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HabitTracker.Api.Models;

public class Habit
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Icon { get; set; } 
    public string? Color { get; set; } 
    public int TargetCount { get; set; } = 1; 
    public List<DayOfWeek> ActiveDays { get; set; } = new List<DayOfWeek>();
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}