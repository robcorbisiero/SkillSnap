using System.Text.Json.Serialization;

namespace SkillSnap.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<AttendanceStatus>))]
public enum AttendanceStatus { NotRecorded, Attended, Absent }

public sealed record AttendanceEntryDto(int RegistrationId, string? Email, AttendanceStatus Status,
    int Version, DateTime? UpdatedAtUtc, bool IsLegacy);
public sealed record AttendanceRosterDto(int SessionId, string Title, DateTime StartsAtUtc, List<AttendanceEntryDto> Entries);
public sealed record AttendanceUpdate(string? Status, int Version);
