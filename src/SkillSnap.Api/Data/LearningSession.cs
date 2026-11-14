namespace SkillSnap.Api.Data;

public sealed class LearningSession
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Instructor { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int Capacity { get; set; }
}
