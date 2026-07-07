namespace backend.DTOs;

public record LoginRequest(string Username);
public record AuthResponse(string Token, int UserId, string Username);

public record CreateRoomRequest(string Name, List<int> MemberIds);

public record SendMessageRequest(int ChatRoomId, string Content);

public record ChatRoomDto(int Id, string Name, string Type, DateTime CreatedAt, List<string> Members);

public record MessageDto(int Id, int ChatRoomId, int SenderId, string SenderName,
    string MessageType, string Content, string? ImageUrl, DateTime Timestamp);

public record StartSessionRequest(double? Latitude, double? Longitude);
