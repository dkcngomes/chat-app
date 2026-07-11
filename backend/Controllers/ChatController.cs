using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Hubs;
using backend.Models;
using backend.Services;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatController(AppDbContext db, IWebHostEnvironment env, IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _env = env;
        _hubContext = hubContext;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string UserName => User.FindFirstValue(ClaimTypes.Name)!;

    [HttpGet("rooms")]
    public async Task<ActionResult<List<ChatRoomDto>>> GetRooms()
    {
        var rooms = await _db.ChatRooms
            .Include(r => r.Members).ThenInclude(m => m.User)
            .Where(r => r.Type == "public" || r.Members.Any(m => m.UserId == UserId))
            .ToListAsync();

        return rooms.Select(r => new ChatRoomDto(
            r.Id, r.Name, r.Type, r.CreatedAt,
            r.Members.Select(m => m.User.Username).ToList(),
            r.IsClosed
        )).ToList();
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<object>>> GetUsers()
    {
        var users = await _db.Users
            .Where(u => u.Id != UserId)
            .Select(u => new { u.Id, u.Username })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost("rooms")]
    public async Task<ActionResult<ChatRoomDto>> CreateRoom(CreateRoomRequest req)
    {
        var room = new ChatRoom
        {
            Name = req.Name,
            Type = "private",
            Members = new List<ChatRoomMember>
            {
                new() { UserId = UserId }
            }
        };

        foreach (var memberId in req.MemberIds.Distinct().Where(id => id != UserId))
        {
            room.Members.Add(new ChatRoomMember { UserId = memberId });
        }

        _db.ChatRooms.Add(room);
        await _db.SaveChangesAsync();

        // Reload with user nav properties
        await _db.Entry(room).Collection(r => r.Members).LoadAsync();
        foreach (var m in room.Members)
            await _db.Entry(m).Reference(x => x.User).LoadAsync();

        var dto = new ChatRoomDto(room.Id, room.Name, room.Type, room.CreatedAt,
            room.Members.Select(m => m.User?.Username ?? "").ToList(),
            room.IsClosed);

        // Notify all other members in real-time
        foreach (var memberId in req.MemberIds.Distinct().Where(id => id != UserId))
        {
            try
            {
                await _hubContext.Clients.User(memberId.ToString())
                    .SendAsync("RoomCreated", dto);
            }
            catch
            {
                // Offline — fine
            }
        }

        return Ok(dto);
    }

    [HttpPost("rooms/dm/{targetUserId}")]
    public async Task<ActionResult<ChatRoomDto>> GetOrCreateDM(int targetUserId)
    {
        // Find existing private room between these two users
        var room = await _db.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Type == "private" &&
                r.Members.Any(m => m.UserId == UserId) &&
                r.Members.Any(m => m.UserId == targetUserId) &&
                r.Members.Count == 2);

        if (room != null)
        {
            // Reload members with User nav for usernames
            await _db.Entry(room).Collection(r => r.Members).LoadAsync();
            foreach (var m in room.Members)
                await _db.Entry(m).Reference(x => x.User).LoadAsync();

            return Ok(new ChatRoomDto(room.Id, room.Name, room.Type, room.CreatedAt,
                room.Members.Select(m => m.User?.Username ?? "").ToList(),
                room.IsClosed));
        }

        // Create new one
        var targetUser = await _db.Users.FindAsync(targetUserId);
        if (targetUser == null) return NotFound("User not found.");

        var newRoom = new ChatRoom
        {
            Name = $"DM with {targetUser.Username}",
            Type = "private",
            Members = new List<ChatRoomMember>
            {
                new() { UserId = UserId },
                new() { UserId = targetUserId }
            }
        };

        _db.ChatRooms.Add(newRoom);
        await _db.SaveChangesAsync();

        // Reload with user nav properties
        await _db.Entry(newRoom).Collection(r => r.Members).LoadAsync();
        foreach (var m in newRoom.Members)
            await _db.Entry(m).Reference(x => x.User).LoadAsync();

        var dto = new ChatRoomDto(newRoom.Id, newRoom.Name, newRoom.Type, newRoom.CreatedAt,
            newRoom.Members.Select(m => m.User?.Username ?? "").ToList(),
            newRoom.IsClosed);

        // Notify the target user in real-time so the DM room appears in their sidebar
        try
        {
            await _hubContext.Clients.User(targetUserId.ToString())
                .SendAsync("RoomCreated", dto);
        }
        catch
        {
            // If the target user is offline, that's fine — they'll see it on refresh
        }

        return Ok(dto);
    }

    [HttpGet("rooms/{roomId}/messages")]
    public async Task<ActionResult<List<MessageDto>>> GetMessages(int roomId)
    {
        var room = await _db.ChatRooms.Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return NotFound();
        if (room.Type == "private" && !room.Members.Any(m => m.UserId == UserId))
            return Forbid();

        var messages = await _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.ChatRoomId == roomId)
            .OrderBy(m => m.Timestamp)
            .Take(200)
            .ToListAsync();

        return messages.Select(m => new MessageDto(
            m.Id, m.ChatRoomId, m.SenderId, m.Sender.Username,
            m.MessageType, m.Content,
            string.IsNullOrEmpty(m.ImagePath) ? null : $"/uploads/{m.ImagePath}",
            m.Timestamp
        )).ToList();
    }

    [HttpPost("upload")]
    public async Task<ActionResult<object>> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return Ok(new { fileName, url = $"/uploads/{fileName}" });
    }

    [HttpPost("sessions/start")]
    public async Task<ActionResult> StartSession([FromBody] StartSessionRequest? req)
    {
        // Capture client IP
        var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        _db.UserSessions.Add(new UserSession
        {
            UserId = UserId,
            IpAddress = ip,
            Latitude = req?.Latitude,
            Longitude = req?.Longitude
        });

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("sessions/end")]
    public async Task<ActionResult> EndSession()
    {
        var active = await _db.UserSessions
            .Where(s => s.UserId == UserId && s.EndedAt == null)
            .ToListAsync();

        foreach (var session in active)
            session.EndedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Close a private DM room. Either member can close it.
    /// The room's transcript is emailed to 'chatlankainfo@gmail.com' in the background.
    /// </summary>
    [HttpPost("rooms/{roomId}/close")]
    public async Task<ActionResult> CloseRoom(int roomId)
    {
        var room = await _db.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.Type == "private");

        if (room == null) return NotFound("Room not found.");
        if (!room.Members.Any(m => m.UserId == UserId))
            return Forbid("You are not a member of this room.");

        if (room.IsClosed)
            return BadRequest("Room is already closed.");

        room.IsClosed = true;
        room.ClosedAt = DateTime.UtcNow;
        room.ClosedByUserId = UserId;

        await _db.SaveChangesAsync();

        // Fire-and-forget transcript emailing
        var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        var emailService = HttpContext.RequestServices.GetRequiredService<EmailService>();
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Reload room with full data
                var fullRoom = await db.ChatRooms
                    .Include(r => r.Members).ThenInclude(m => m.User)
                    .FirstAsync(r => r.Id == roomId);

                var messages = await db.Messages
                    .Include(m => m.Sender)
                    .Where(m => m.ChatRoomId == roomId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                var html = BuildRoomTranscriptHtml(fullRoom, messages);
                var attachments = messages
                    .Where(m => m.MessageType == "image" && !string.IsNullOrEmpty(m.ImagePath))
                    .Select(m => Path.Combine(env.ContentRootPath, "wwwroot", "uploads", m.ImagePath))
                    .Where(p => System.IO.File.Exists(p))
                    .ToList();

                var toEmail = config["Email:DefaultTranscriptEmail"] ?? "chatlankainfo@gmail.com";
                if (string.IsNullOrEmpty(config["Email:Host"]))
                {
                    // If SMTP not configured, save transcript to file
                    var transcriptDir = Path.Combine(env.ContentRootPath, "transcripts");
                    Directory.CreateDirectory(transcriptDir);
                    var filePath = Path.Combine(transcriptDir, $"room_{roomId}_{fullRoom.ClosedAt:yyyyMMddHHmmss}.html");
                    await System.IO.File.WriteAllTextAsync(filePath, html);
                    return;
                }

                await emailService.SendTranscriptAsync(
                    toEmail,
                    $"[Chat Closed] Room #{roomId} - {fullRoom.Name} - {fullRoom.ClosedAt:yyyy-MM-dd HH:mm} UTC",
                    html,
                    attachments
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to send room transcript: {ex.Message}");
            }
        });

        // Notify both members via SignalR that the room was closed
        foreach (var member in room.Members)
        {
            try
            {
                await _hubContext.Clients.User(member.UserId.ToString())
                    .SendAsync("RoomClosed", new { roomId, roomName = room.Name });
            }
            catch { /* offline */ }
        }

        return Ok(new { message = "Room closed. Transcript will be emailed." });
    }

    private static string BuildRoomTranscriptHtml(ChatRoom room, List<Message> messages)
    {
        var closedBy = room.ClosedByUserId.HasValue
            ? room.Members.FirstOrDefault(m => m.UserId == room.ClosedByUserId)?.User?.Username ?? "Unknown"
            : "Unknown";

        var sb = new System.Text.StringBuilder();
        sb.Append($@"<!DOCTYPE html><html><head><meta charset='utf-8'></head><body>");
        sb.Append($@"<h2>🔐 Chat Transcript — Room Closed</h2>
<hr/>
<table border='1' cellpadding='6' style='border-collapse:collapse;width:100%;margin-bottom:16px'>
<tr><td><strong>Room</strong></td><td>{System.Net.WebUtility.HtmlEncode(room.Name)}</td></tr>
<tr><td><strong>Type</strong></td><td>{room.Type}</td></tr>
<tr><td><strong>Created</strong></td><td>{room.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
<tr><td><strong>Closed At</strong></td><td>{room.ClosedAt:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
<tr><td><strong>Closed By</strong></td><td>{System.Net.WebUtility.HtmlEncode(closedBy)}</td></tr>
<tr><td><strong>Total Messages</strong></td><td>{messages.Count}</td></tr>
<tr><td><strong>Members</strong></td><td>{string.Join(", ", room.Members.Select(m => System.Net.WebUtility.HtmlEncode(m.User?.Username ?? "?")))}</td></tr>
</table>
<hr/>
<h3>Message Log</h3>
<table border='1' cellpadding='6' style='border-collapse:collapse;width:100%'>
<tr style='background:#2d3748;color:#fff'><th>Time (UTC)</th><th>Sender</th><th>Type</th><th>Content</th></tr>");

        foreach (var m in messages)
        {
            var content = m.MessageType == "image"
                ? $"{System.Net.WebUtility.HtmlEncode(m.Content)} <br/><em>[Image: {m.ImagePath}]</em>"
                : System.Net.WebUtility.HtmlEncode(m.Content);
            sb.Append($"<tr><td>{m.Timestamp:HH:mm:ss}</td><td>{System.Net.WebUtility.HtmlEncode(m.Sender?.Username ?? "?")}</td><td>{m.MessageType}</td><td>{content}</td></tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }
}
