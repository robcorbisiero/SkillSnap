using SkillSnap.Contracts;

namespace SkillSnap.Api.Data;

public sealed class Registration
{
    public int Id { get; set; }
    public int LearningSessionId { get; set; }
    public string? ParticipantHash { get; set; }
    public string? UserId { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
    public int AttendanceVersion { get; set; }
    public DateTime? AttendanceUpdatedAtUtc { get; set; }
    public string? AttendanceUpdatedByUserId { get; set; }
}
