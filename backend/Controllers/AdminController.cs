using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;

namespace backend.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AdminController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private bool IsAuthorized()
    {
        var key = _config["Admin:Key"];
        var provided = Request.Query["key"].FirstOrDefault() ?? "";
        return !string.IsNullOrEmpty(key) && provided == key;
    }

    /// <summary>
    /// GET /api/admin/rooms?key=xxx — list all chat rooms with message counts
    /// </summary>
    [HttpGet("rooms")]
    public async Task<ActionResult> GetRooms()
    {
        if (!IsAuthorized())
            return Unauthorized(new { error = "Invalid or missing admin key. Pass ?key=yourkey" });

        var rooms = await _db.ChatRooms
            .Include(r => r.Members).ThenInclude(m => m.User)
            .Include(r => r.Messages)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var result = rooms.Select(r => new
        {
            r.Id,
            r.Name,
            r.Type,
            r.IsClosed,
            r.ClosedAt,
            r.CreatedAt,
            MemberCount = r.Members.Count,
            Members = r.Members.Select(m => m.User?.Username ?? "?").ToList(),
            MessageCount = r.Messages.Count,
            LastMessageAt = r.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault()?.Timestamp
        });

        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/rooms/{roomId}/messages?key=xxx — all messages in a room
    /// </summary>
    [HttpGet("rooms/{roomId}/messages")]
    public async Task<ActionResult> GetRoomMessages(int roomId)
    {
        if (!IsAuthorized())
            return Unauthorized(new { error = "Invalid or missing admin key. Pass ?key=yourkey" });

        var room = await _db.ChatRooms
            .Include(r => r.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null)
            return NotFound(new { error = "Room not found." });

        var messages = await _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.ChatRoomId == roomId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Ok(new
        {
            Room = new
            {
                room.Id,
                room.Name,
                room.Type,
                room.IsClosed,
                room.CreatedAt,
                Members = room.Members.Select(m => m.User?.Username ?? "?").ToList()
            },
            Messages = messages.Select(m => new
            {
                m.Id,
                Sender = m.Sender?.Username ?? "?",
                m.MessageType,
                m.Content,
                ImageUrl = ResolveImageUrl(m.ImagePath),
                m.Timestamp,
                m.IpAddress
            })
        });
    }

    /// <summary>
    /// GET /api/admin/gallery?key=xxx — all images across every room
    /// </summary>
    [HttpGet("gallery")]
    public async Task<ActionResult> GetGallery()
    {
        if (!IsAuthorized())
            return Unauthorized(new { error = "Invalid or missing admin key. Pass ?key=yourkey" });

        var images = await _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.ChatRoom)
            .Where(m => m.MessageType == "image" && m.ImagePath != null && m.ImagePath != "")
            .OrderByDescending(m => m.Timestamp)
            .Select(m => new
            {
                m.Id,
                RoomId = m.ChatRoomId,
                RoomName = m.ChatRoom.Name,
                Sender = m.Sender!.Username,
                Caption = m.Content,
                ImageUrl = ResolveImageUrl(m.ImagePath),
                m.Timestamp
            })
            .ToListAsync();

        return Ok(images);
    }

    private static string? ResolveImageUrl(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;
        if (imagePath.StartsWith("http") || imagePath.StartsWith("/")) return imagePath;
        return $"/uploads/{imagePath}";
    }

    /// <summary>
    /// GET /api/admin/sessions?key=xxx — list all user sessions with IP and GPS data
    /// </summary>
    [HttpGet("sessions")]
    public async Task<ActionResult> GetSessions()
    {
        if (!IsAuthorized())
            return Unauthorized(new { error = "Invalid or missing admin key. Pass ?key=yourkey" });

        var sessions = await _db.UserSessions
            .Include(s => s.User)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                Username = s.User.Username,
                s.IpAddress,
                s.Latitude,
                s.Longitude,
                s.StartedAt,
                s.EndedAt,
                s.Emailed
            })
            .ToListAsync();

        return Ok(sessions);
    }

    /// <summary>
    /// GET /api/admin/users?key=xxx — list all users with session count and last known IP
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult> GetUsers()
    {
        if (!IsAuthorized())
            return Unauthorized(new { error = "Invalid or missing admin key. Pass ?key=yourkey" });

        var users = await _db.Users
            .Include(u => u.ChatRoomMembers)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.CreatedAt,
                RoomCount = u.ChatRoomMembers.Count,
                SessionCount = _db.UserSessions.Count(s => s.UserId == u.Id),
                LastSession = _db.UserSessions
                    .Where(s => s.UserId == u.Id)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => new { s.IpAddress, s.StartedAt })
                    .FirstOrDefault()
            })
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }
}
