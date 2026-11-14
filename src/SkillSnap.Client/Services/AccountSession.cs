using System.Net;
using System.Net.Http.Json;
using SkillSnap.Contracts;
using Microsoft.JSInterop;

namespace SkillSnap.Client.Services;

public sealed class AccountSession(ApiClient api, IJSRuntime js)
{
    public AccountInfo? Current { get; private set; }
    public bool IsSignedIn => Current is not null;
    public event Action? Changed;

    public async Task RefreshAsync()
    {
        using var response = await api.SendAsync(HttpMethod.Get, "api/auth/me");
        if (response.StatusCode == HttpStatusCode.Unauthorized) Current = null;
        else
        {
            response.EnsureSuccessStatusCode();
            Current = await response.Content.ReadFromJsonAsync<AccountInfo>();
        }
        Changed?.Invoke();
    }

    public async Task SignInAsync(AccountCredentials credentials, bool createAccount)
    {
        using var response = await api.SendAsync(HttpMethod.Post, createAccount ? "api/auth/register" : "api/auth/login", credentials);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Email or password is incorrect, or the account is temporarily locked. Try again later if you’ve had several failed attempts.",
                HttpStatusCode.TooManyRequests => "Too many attempts. Please wait five minutes and try again.",
                _ when createAccount => "Couldn’t create the account. Use a unique email and a password with 10–128 characters, uppercase, lowercase, a number, and a symbol.",
                _ => "Couldn’t sign in. Refresh and try again."
            };
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        await RefreshAsync();
    }

    public async Task SignOutAsync()
    {
        using var response = await api.SendAsync(HttpMethod.Post, "api/auth/logout");
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized) response.EnsureSuccessStatusCode();
        Current = null;
        Changed?.Invoke();
    }

    private async Task<string?> LegacyKeyAsync()
    {
        var key = await js.InvokeAsync<string?>("localStorage.getItem", "skillsnap.participantKey");
        return Guid.TryParse(key, out var parsed) && parsed != Guid.Empty ? parsed.ToString("N") : null;
    }

    public async Task<int> LegacyCountAsync()
    {
        var key = await LegacyKeyAsync();
        if (key is null) return 0;
        using var response = await api.SendAsync(HttpMethod.Get, "api/registrations/legacy-count", legacyKey: key);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LegacyBookingCount>())?.Count ?? 0;
    }

    public async Task ImportAsync()
    {
        var key = await LegacyKeyAsync() ?? throw new InvalidOperationException("No browser bookings were found.");
        using var response = await api.SendAsync(HttpMethod.Post, "api/registrations/import", legacyKey: key);
        response.EnsureSuccessStatusCode();
        // Keep the old key until a successful response; retries of the import are safe.
        await js.InvokeVoidAsync("localStorage.removeItem", "skillsnap.participantKey");
    }
}
