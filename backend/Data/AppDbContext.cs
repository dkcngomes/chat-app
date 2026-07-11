using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMember> ChatRoomMembers => Set<ChatRoomMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<ChatRoomMember>(e =>
        {
            e.HasIndex(m => new { m.ChatRoomId, m.UserId }).IsUnique();
            e.HasOne(m => m.ChatRoom).WithMany(r => r.Members).HasForeignKey(m => m.ChatRoomId);
            e.HasOne(m => m.User).WithMany(u => u.ChatRoomMembers).HasForeignKey(m => m.UserId);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.HasOne(m => m.ChatRoom).WithMany(r => r.Messages).HasForeignKey(m => m.ChatRoomId);
            e.HasOne(m => m.Sender).WithMany(u => u.Messages).HasForeignKey(m => m.SenderId);
        });

        modelBuilder.Entity<UserSession>(e =>
        {
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
        });

        // Seed the public room
        modelBuilder.Entity<ChatRoom>().HasData(new ChatRoom
        {
            Id = 1,
            Name = "General",
            Type = "public",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsClosed = false
        });
    }
}