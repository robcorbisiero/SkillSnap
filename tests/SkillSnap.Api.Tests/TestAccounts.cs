using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Tests;

internal static class TestAccounts
{
    // Synthetic test credentials only. No real account or secret is used by this suite.
    internal const string Password = "Test-Only-Password42!";
    internal static async Task<HttpClient> CreateAsync(SessionFactory factory, string? email = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        email ??= $"test-{Guid.NewGuid():N}@example.test";
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/auth/register", new AccountCredentials { Email = email, Password = Password });
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var login = await client.PostAsJsonAsync("/api/auth/login", new AccountCredentials { Email = email, Password = Password });
            login.EnsureSuccessStatusCode();
        }
        else response.EnsureSuccessStatusCode();
        await CsrfAsync(client);
        return client;
    }

    internal static async Task CsrfAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<CsrfToken>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
    }
}
