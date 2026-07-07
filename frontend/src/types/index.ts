export interface AuthResponse {
    token: string;
    userId: number;
    username: string;
}

export interface ChatRoom {
    id: number;
    name: string;
    type: string;
    createdAt: string;
    members: string[];
}

export interface Message {
    id: number;
    chatRoomId: number;
    senderId: number;
    senderName: string;
    messageType: string;
    content: string;
    imageUrl: string | null;
    timestamp: string;
}

export interface UserInfo {
    id: number;
    username: string;
}
