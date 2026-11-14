using System.Net;
using System.Net.Http.Json;
using SkillSnap.Contracts;

namespace SkillSnap.Client.Services;

public sealed class OrganizerService(ApiClient api)
{
    public async Task<List<OrganizerSessionDto>> GetSessionsAsync()
    {
        using var response = await api.SendAsync(HttpMethod.Get, "api/organizer/learning-sessions/");
        Check(response);
        return await response.Content.ReadFromJsonAsync<List<OrganizerSessionDto>>()
            ?? throw new HttpRequestException("The API returned no session data.");
    }

    public async Task<int> SaveAsync(int? id, SessionUpdate session)
    {
        using var response = await api.SendAsync(id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "api/organizer/learning-sessions/" : $"api/organizer/learning-sessions/{id}", session);
        Check(response);
        return id ?? (await response.Content.ReadFromJsonAsync<OrganizerSessionDto>())!.Id;
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new HttpRequestException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sign in again to use organizer tools.",
            HttpStatusCode.Forbidden => "Organizer access is required. Your account cannot make this change.",
            HttpStatusCode.BadRequest => "Check all fields and the future UTC start time, then refresh the page if the request still fails.",
            HttpStatusCode.NotFound => "This session could not be found. Refresh the list.",
            HttpStatusCode.Conflict => "Nothing was changed. Reload the latest session: another edit, new bookings, or a locked schedule prevented this update. Capacity cannot be below bookings, and booked sessions cannot change time or duration.",
            _ => "Couldn’t confirm the change. Refresh the list before trying again."
        }, null, response.StatusCode);
    }
}
