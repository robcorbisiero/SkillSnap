using System.Net;
using System.Net.Http.Json;
using SkillSnap.Contracts;

namespace SkillSnap.Client.Services;

public sealed class AttendanceService(ApiClient api)
{
    public async Task<AttendanceRosterDto> GetAsync(int sessionId)
    {
        using var response = await api.SendAsync(HttpMethod.Get, $"api/organizer/learning-sessions/{sessionId}/attendance/");
        Check(response);
        return await response.Content.ReadFromJsonAsync<AttendanceRosterDto>()
            ?? throw new HttpRequestException("The roster response was empty.");
    }

    public async Task SetAsync(int sessionId, AttendanceEntryDto entry, AttendanceStatus status)
    {
        using var response = await api.SendAsync(HttpMethod.Put,
            $"api/organizer/learning-sessions/{sessionId}/attendance/{entry.RegistrationId}",
            new AttendanceUpdate(status.ToString(), entry.Version));
        Check(response);
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new HttpRequestException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sign in again to manage attendance.",
            HttpStatusCode.Forbidden => "Organizer access is required to view or update attendance.",
            HttpStatusCode.NotFound => "The session or registration no longer exists. Refresh the roster.",
            HttpStatusCode.Conflict => "Attendance changed, or this is an unclaimed browser booking. Refresh the roster; browser bookings must be imported before attendance can be recorded.",
            HttpStatusCode.BadRequest => "The request could not be validated. Refresh the page and try again.",
            _ => "Couldn’t confirm the update. Refresh the roster before retrying."
        }, null, response.StatusCode);
    }
}
