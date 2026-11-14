using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using SkillSnap.Contracts;

namespace SkillSnap.Client.Services;

public sealed class ApiClient(HttpClient http)
{
    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? legacyKey = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        if (legacyKey is not null) request.Headers.Add("X-Participant-Key", legacyKey);
        if (method != HttpMethod.Get)
        {
            // Obtain a fresh, identity-bound token, including after login/logout or a tab switch.
            using var tokenResponse = await SendAsync(HttpMethod.Get, "api/auth/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadFromJsonAsync<CsrfToken>()
                ?? throw new HttpRequestException("Couldn’t prepare the request. Refresh and try again.");
            request.Headers.Add("X-CSRF-TOKEN", token.Token);
        }
        return await http.SendAsync(request);
    }
}
