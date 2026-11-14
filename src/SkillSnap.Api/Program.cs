using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using SkillSnap.Api.Data;
using SkillSnap.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
var connection = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("SkillSnap")
    ?? "Data Source=App_Data/skillsnap.db");
if (!Path.IsPathRooted(connection.DataSource))
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
builder.Services.AddDbContext<SkillSnapDbContext>(options =>
    options.UseSqlite(connection.ConnectionString));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5180"]).WithMethods("GET", "POST", "DELETE").AllowAnyHeader()));
var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
    await db.Database.MigrateAsync();
}
app.MapSessionEndpoints();
app.Run();

// Expose the entry point to the HTTP integration test host.
public partial class Program { }
