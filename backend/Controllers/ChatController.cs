using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Hubs;
using backend.Models;
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

    [HttpGet("rooms")]
    public async Task<ActionResult<List<ChatRoomDto>>> GetRooms()
    {
        var rooms = await _db.ChatRooms
            .Include(r => r.Members).ThenInclude(m => m.User)
            .Where(r => r.Type == "public" || r.Members.Any(m => m.UserId == UserId))
            .ToListAsync();

        return rooms.Select(r => new ChatRoomDto(
            r.Id, r.Name, r.Type, r.CreatedAt,
            r.Members.Select(m => m.User.Username).ToList()
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
            room.Members.Select(m => m.User?.Username ?? "").ToList());

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
                room.Members.Select(m => m.User?.Username ?? "").ToList()));
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
            newRoom.Members.Select(m => m.User?.Username ?? "").ToList());

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
}
