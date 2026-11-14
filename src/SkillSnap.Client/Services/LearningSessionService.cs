using System.Net.Http.Json;
using SkillSnap.Contracts;
using Microsoft.JSInterop;

namespace SkillSnap.Client.Services;

public sealed class LearningSessionService(HttpClient http, IJSRuntime js)
{
    private string? participantKey;

    private async Task EnsureParticipantAsync()
    {
        if (participantKey is not null) return;
        var stored = await js.InvokeAsync<string?>("localStorage.getItem", "skillsnap.participantKey");
        if (!Guid.TryParse(stored, out var key) || key == Guid.Empty)
        {
            key = Guid.NewGuid();
            await js.InvokeVoidAsync("localStorage.setItem", "skillsnap.participantKey", key.ToString("N"));
        }
        participantKey = key.ToString("N");
        http.DefaultRequestHeaders.Remove("X-Participant-Key");
        http.DefaultRequestHeaders.Add("X-Participant-Key", participantKey);
    }

    public async Task<List<LearningSessionDto>> GetSessionsAsync()
    {
        await EnsureParticipantAsync();
        return await http.GetFromJsonAsync<List<LearningSessionDto>>("api/learning-sessions")
            ?? throw new InvalidOperationException("The API returned an empty response body.");
    }

    public async Task SetRegistrationAsync(int sessionId, bool register)
    {
        await EnsureParticipantAsync();
        using var request = new HttpRequestMessage(register ? HttpMethod.Post : HttpMethod.Delete,
            $"api/learning-sessions/{sessionId}/registration");
        using var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Conflict => "This session is full or has already started. Refresh to see the latest availability.",
            System.Net.HttpStatusCode.NotFound => "This session is no longer available. Refresh the sessions.",
            _ => "We couldn’t update your registration. Please try again."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
