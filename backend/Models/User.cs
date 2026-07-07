using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatRoomMember> ChatRoomMembers { get; set; } = new List<ChatRoomMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
