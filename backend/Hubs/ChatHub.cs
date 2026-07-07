using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;
using System.Security.Claims;

namespace backend.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _db;

    public ChatHub(AppDbContext db)
    {
        _db = db;
    }

    private int UserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string UserName => Context.User!.FindFirstValue(ClaimTypes.Name)!;

    public async Task JoinRoom(int roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");
    }

    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
    }

    public async Task SendMessage(SendMessageRequest req)
    {
        var room = await _db.ChatRooms.Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == req.ChatRoomId);

        if (room == null) return;
        if (room.Type == "private" && !room.Members.Any(m => m.UserId == UserId))
            return;

        // Only allow empty content for image messages
        if (string.IsNullOrWhiteSpace(req.Content)) return;

        var msg = new Message
        {
            ChatRoomId = req.ChatRoomId,
            SenderId = UserId,
            Content = req.Content,
            MessageType = "text",
            Timestamp = DateTime.UtcNow
        };

        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new MessageDto(
            msg.Id, msg.ChatRoomId, msg.SenderId, UserName,
            "text", msg.Content, null, msg.Timestamp
        );

        await Clients.Group($"room_{req.ChatRoomId}").SendAsync("ReceiveMessage", dto);
    }

    public async Task SendImage(int roomId, string imageUrl, string caption)
    {
        var room = await _db.ChatRooms.Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return;
        if (room.Type == "private" && !room.Members.Any(m => m.UserId == UserId))
            return;

        var fileName = imageUrl.Replace("/uploads/", "");

        var msg = new Message
        {
            ChatRoomId = roomId,
            SenderId = UserId,
            Content = caption ?? "",
            MessageType = "image",
            ImagePath = fileName,
            Timestamp = DateTime.UtcNow
        };

        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new MessageDto(
            msg.Id, msg.ChatRoomId, msg.SenderId, UserName,
            "image", msg.Content, imageUrl, msg.Timestamp
        );

        await Clients.Group($"room_{roomId}").SendAsync("ReceiveMessage", dto);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
