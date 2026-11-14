using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Contracts;

public sealed class AccountCredentials
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 10)]
    public string Password { get; set; } = "";
}

public sealed record AccountInfo(string Email);
public sealed record CsrfToken(string Token);
public sealed record LegacyBookingCount(int Count);
