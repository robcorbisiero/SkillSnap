namespace SkillSnap.Api.Data;

public sealed class Registration
{
    public int Id { get; set; }
    public int LearningSessionId { get; set; }
    public string? ParticipantHash { get; set; }
    public string? UserId { get; set; }
}
