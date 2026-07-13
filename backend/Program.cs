using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Hubs;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured. Set the Jwt__Key environment variable.");

// ── Database ──
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT Auth ──
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ChatApp",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ChatApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Allow SignalR to receive JWT from query string
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Services ──
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<TranscriptService>();

// ── File Storage (R2 or local) ──
var storageProvider = builder.Configuration["CloudStorage:Provider"]?.ToLowerInvariant();
if (storageProvider == "r2")
{
    builder.Services.AddSingleton<IFileStorageService, R2StorageService>();
    Console.WriteLine("Using Cloudflare R2 for file storage.");
}
else
{
    builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
    Console.WriteLine("Using local disk for file storage.");
}

// ── JSON: always serialize DateTime as UTC (preserve Z suffix) ──
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opt =>
{
    opt.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
});

// ── SignalR + Controllers ──
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── CORS (for React dev server) ──
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

// ── Middleware pipeline ──
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// ── Auto-migrate, seed, and clear stale sessions ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Close any sessions left open from a previous crash/restart
    var stale = db.UserSessions.Where(s => s.EndedAt == null);
    foreach (var s in stale)
        s.EndedAt = DateTime.UtcNow;
    db.SaveChanges();
}

// ── Background worker: email transcripts for ended sessions ──
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var transcript = scope.ServiceProvider.GetRequiredService<TranscriptService>();
            await transcript.ProcessEndedSessionsAsync();
        }
        catch { /* swallow background errors */ }
        await Task.Delay(TimeSpan.FromMinutes(1));
    }
});

app.Run();
