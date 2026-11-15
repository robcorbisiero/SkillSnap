using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using SkillSnap.Api.Data;
using SkillSnap.Api.Endpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using SkillSnap.Api.Security;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
var postgres = builder.Configuration["Database:Provider"]?.Equals("Postgres", StringComparison.OrdinalIgnoreCase) == true;
var onVercel = Environment.GetEnvironmentVariable("VERCEL") == "1";
if (onVercel && !postgres) throw new InvalidOperationException("Vercel requires persistent PostgreSQL; local SQLite is not allowed.");
if (postgres)
{
    var pg = new NpgsqlConnectionStringBuilder(builder.Configuration.GetConnectionString("SkillSnap")
        ?? throw new InvalidOperationException("A PostgreSQL connection string is required.")) { SearchPath = "skillsnap" };
    builder.Services.AddDbContext<PostgresSkillSnapDbContext>(options => options.UseNpgsql(pg.ConnectionString,
        provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "skillsnap")));
    builder.Services.AddScoped<SkillSnapDbContext>(services => services.GetRequiredService<PostgresSkillSnapDbContext>());
}
else
{
var connection = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("SkillSnap")
    ?? "Data Source=App_Data/skillsnap.db");
if (!Path.IsPathRooted(connection.DataSource))
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
builder.Services.AddDbContext<SkillSnapDbContext>(options =>
    options.UseSqlite(connection.ConnectionString));
}
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<SkillSnapDbContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SkillSnap.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options => options.AddPolicy("Organizer", policy =>
    policy.RequireAuthenticatedUser().AddRequirements(new OrganizerRequirement())));
builder.Services.AddScoped<IAuthorizationHandler, OrganizerHandler>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "SkillSnap.Csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
var protection = builder.Services.AddDataProtection().SetApplicationName("SkillSnap");
if (postgres) protection.PersistKeysToDbContext<PostgresSkillSnapDbContext>();
else protection.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("accounts", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5180"]).WithMethods("GET", "POST", "PUT", "DELETE").AllowAnyHeader().AllowCredentials()));
var app = builder.Build();
// Vercel terminates TLS and controls the forwarding header at its ingress.
// Do not trust arbitrary forwarded headers for other hosts.
if (onVercel)
    app.Use((context, next) =>
    {
        if (context.Request.Headers["x-forwarded-proto"] == "https") context.Request.Scheme = "https";
        return next(context);
    });
app.UseExceptionHandler();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (!postgres) Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
    await db.Database.MigrateAsync();
    if (builder.Configuration["grant-organizer"] is { } email)
    {
        await OrganizerProvisioner.GrantAsync(scope.ServiceProvider, email);
        app.Logger.LogInformation("Organizer access granted to the existing local account. Sign-in is unchanged.");
        return;
    }
}
app.MapSessionEndpoints();
app.MapAccountEndpoints();
app.MapOrganizerEndpoints();
app.MapAttendanceEndpoints();
app.MapGet("/health", async (SkillSnapDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
// An unknown API path must remain a 404, not return the SPA HTML.
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

// Expose the entry point to the HTTP integration test host.
public partial class Program { }
