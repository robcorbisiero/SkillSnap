using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SkillSnap.Client;
using SkillSnap.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
// An empty/relative address uses this deployment's origin, including preview URLs.
var apiBaseUrl = new Uri(new Uri(builder.HostEnvironment.BaseAddress), builder.Configuration["ApiBaseUrl"] ?? "");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseUrl, Timeout = TimeSpan.FromSeconds(15) });
builder.Services.AddScoped<LearningSessionService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AccountSession>();
builder.Services.AddScoped<OrganizerService>();
builder.Services.AddScoped<AttendanceService>();
await builder.Build().RunAsync();
