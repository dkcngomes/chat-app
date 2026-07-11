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
        var key = _config["Admin:Key"] ?? "admin123";
        var provided = Request.Query["key"].FirstOrDefault() ?? "";
        return provided == key;
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
                ImageUrl = string.IsNullOrEmpty(m.ImagePath) ? null : $"/uploads/{m.ImagePath}",
                m.Timestamp
            })
        });
    }
}
