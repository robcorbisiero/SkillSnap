using System.Net.Http.Json;
using SkillSnap.Contracts;

namespace SkillSnap.Client.Services;

public sealed class LearningSessionService(ApiClient api)
{
    public async Task<List<LearningSessionDto>> GetSessionsAsync()
    {
        using var response = await api.SendAsync(HttpMethod.Get, "api/learning-sessions");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<LearningSessionDto>>()
            ?? throw new InvalidOperationException("The API returned an empty response body.");
    }

    public async Task SetRegistrationAsync(int sessionId, bool register)
    {
        using var response = await api.SendAsync(register ? HttpMethod.Post : HttpMethod.Delete,
            $"api/learning-sessions/{sessionId}/registration");
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Your session has ended. Sign in again to manage registrations.",
            System.Net.HttpStatusCode.Conflict => register
                ? "This session is full or has already started. Refresh to see the latest availability."
                : "Attendance is recorded. Ask an organizer to clear it before cancelling.",
            System.Net.HttpStatusCode.NotFound => "This session is no longer available. Refresh the sessions.",
            _ => "We couldn’t update your registration. Please try again."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
