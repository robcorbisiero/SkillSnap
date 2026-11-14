namespace SkillSnap.Contracts;

public sealed record LearningSessionDto(
    int Id, string Title, string Description, string Topic,
    string Instructor, DateTime StartsAtUtc, int DurationMinutes, int Capacity);
