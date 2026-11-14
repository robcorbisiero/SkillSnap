using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Contracts;

public class SessionInput : IValidatableObject
{
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [Required, StringLength(2000)] public string Description { get; set; } = "";
    [Required, StringLength(80)] public string Topic { get; set; } = "";
    [Required, StringLength(120)] public string Instructor { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    [Range(1, 480)] public int DurationMinutes { get; set; } = 60;
    [Range(1, 1000)] public int Capacity { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAtUtc.Kind != DateTimeKind.Utc)
            yield return new ValidationResult("Start time must be UTC (ending in Z).", [nameof(StartsAtUtc)]);
        if (StartsAtUtc <= DateTime.UtcNow)
            yield return new ValidationResult("Start time must be in the future.", [nameof(StartsAtUtc)]);
    }
}

public sealed class SessionUpdate : SessionInput
{
    [Range(0, int.MaxValue)] public int Version { get; set; }
}

public sealed record OrganizerSessionDto(int Id, string Title, string Description, string Topic,
    string Instructor, DateTime StartsAtUtc, int DurationMinutes, int Capacity, int RegisteredCount, int Version);
