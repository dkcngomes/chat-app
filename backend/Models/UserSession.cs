using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class UserSession
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary> Null = still active </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary> Client IP address at session start </summary>
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary> GPS latitude from browser Geolocation API </summary>
    public double? Latitude { get; set; }

    /// <summary> GPS longitude from browser Geolocation API </summary>
    public double? Longitude { get; set; }

    /// <summary> Has the transcript been emailed? </summary>
    public bool Emailed { get; set; } = false;
}
