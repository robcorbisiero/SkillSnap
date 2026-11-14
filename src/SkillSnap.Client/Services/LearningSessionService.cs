using System.Net.Http.Json;
using SkillSnap.Contracts;

namespace SkillSnap.Client.Services;

public sealed class LearningSessionService(HttpClient http)
{
    public async Task<List<LearningSessionDto>> GetSessionsAsync() =>
        await http.GetFromJsonAsync<List<LearningSessionDto>>("api/learning-sessions")
        ?? throw new InvalidOperationException("The API returned an empty response body.");
}
