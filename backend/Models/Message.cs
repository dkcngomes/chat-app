using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Message
{
    [Key]
    public int Id { get; set; }

    public int ChatRoomId { get; set; }
    public ChatRoom ChatRoom { get; set; } = null!;

    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;

    /// <summary> "text" or "image" </summary>
    [Required, MaxLength(20)]
    public string MessageType { get; set; } = "text";

    /// <summary> Text content or image caption </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary> Relative path to stored image file (empty for text messages) </summary>
    public string ImagePath { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
