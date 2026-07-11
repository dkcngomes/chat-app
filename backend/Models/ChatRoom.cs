using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class ChatRoom
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary> "public" or "private" </summary>
    [Required, MaxLength(20)]
    public string Type { get; set; } = "public";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary> Whether a private DM room has been closed by a user. Transcript sent when closed. </summary>
    public bool IsClosed { get; set; } = false;

    /// <summary> UTC timestamp when the room was closed (null = still active). </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary> ID of the user who closed the room (for audit). </summary>
    public int? ClosedByUserId { get; set; }

    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}