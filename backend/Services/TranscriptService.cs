using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Services;

public class TranscriptService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailService _emailService;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public TranscriptService(IServiceScopeFactory scopeFactory, EmailService emailService,
        IConfiguration config, IWebHostEnvironment env)
    {
        _scopeFactory = scopeFactory;
        _emailService = emailService;
        _config = config;
        _env = env;
    }

    public async Task ProcessEndedSessionsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sessions = await db.UserSessions
            .Include(s => s.User)
            .Where(s => s.EndedAt != null && !s.Emailed)
            .ToListAsync();

        foreach (var session in sessions)
        {
            await SendTranscriptForUserAsync(db, session);
            session.Emailed = true;
        }

        await db.SaveChangesAsync();
    }

    private async Task SendTranscriptForUserAsync(AppDbContext db, UserSession session)
    {
        var messages = await db.Messages
            .Include(m => m.ChatRoom)
            .Include(m => m.Sender)
            .Where(m => m.SenderId == session.UserId
                        && m.Timestamp >= session.StartedAt
                        && (session.EndedAt == null || m.Timestamp <= session.EndedAt))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        if (messages.Count == 0) return;

        var html = BuildTranscriptHtml(session, messages);
        var attachments = messages
            .Where(m => m.MessageType == "image" && !string.IsNullOrEmpty(m.ImagePath))
            .Select(m => Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", m.ImagePath))
            .Where(p => File.Exists(p))
            .ToList();

        var toEmail = _config["Email:DefaultTranscriptEmail"] ?? "admin@example.com";

        await _emailService.SendTranscriptAsync(
            toEmail,
            $"[Security Transcript] {session.User.Username} - {session.StartedAt:yyyy-MM-dd HH:mm} UTC",
            html,
            attachments
        );
    }

    private static string BuildTranscriptHtml(UserSession session, List<Message> messages)
    {
        var gps = session.Latitude.HasValue && session.Longitude.HasValue
            ? $"{session.Latitude:F6}, {session.Longitude:F6}  (<a href='https://www.google.com/maps?q={session.Latitude},{session.Longitude}'>view map</a>)"
            : "Not available";

        var sb = new System.Text.StringBuilder();
        sb.Append($@"<h2>🔐 Chat Session Transcript — Security Report</h2>
<hr/>
<table border='1' cellpadding='6' style='border-collapse:collapse;width:100%;margin-bottom:16px'>
<tr><td><strong>User</strong></td><td>{session.User.Username}</td></tr>
<tr><td><strong>Session Start</strong></td><td>{session.StartedAt:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
<tr><td><strong>Session End</strong></td><td>{session.EndedAt:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
<tr><td><strong>IP Address</strong></td><td>{session.IpAddress ?? "Unknown"}</td></tr>
<tr><td><strong>GPS Location</strong></td><td>{gps}</td></tr>
<tr><td><strong>Total Messages</strong></td><td>{messages.Count}</td></tr>
</table>
<hr/>
<h3>Message Log</h3>
<table border='1' cellpadding='6' style='border-collapse:collapse;width:100%'>
<tr style='background:#2d3748;color:#fff'><th>Time (UTC)</th><th>Room</th><th>Type</th><th>Content</th></tr>");

        foreach (var m in messages)
        {
            var content = m.MessageType == "image"
                ? $"{System.Net.WebUtility.HtmlEncode(m.Content)} <br/><em>[Image: {m.ImagePath}]</em>"
                : System.Net.WebUtility.HtmlEncode(m.Content);
            sb.Append($"<tr><td>{m.Timestamp:HH:mm:ss}</td><td>{System.Net.WebUtility.HtmlEncode(m.ChatRoom.Name)}</td><td>{m.MessageType}</td><td>{content}</td></tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }
}
