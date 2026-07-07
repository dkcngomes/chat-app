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

    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
