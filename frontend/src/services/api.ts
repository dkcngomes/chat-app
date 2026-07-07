const API_BASE = "http://localhost:5223/api";

function getToken(): string | null {
    return localStorage.getItem("token");
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const token = getToken();
    const headers: Record<string, string> = {
        ...(options.headers as Record<string, string>),
    };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    if (!(options.body instanceof FormData)) {
        headers["Content-Type"] = "application/json";
    }

    const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
    if (!res.ok) {
        const text = await res.text();
        throw new Error(text || res.statusText);
    }
    return res.json();
}

export const authApi = {
    login: (username: string) =>
        request<import("../types").AuthResponse>("/auth/login", {
            method: "POST",
            body: JSON.stringify({ username }),
        }),
};

export const chatApi = {
    getRooms: () => request<import("../types").ChatRoom[]>("/chat/rooms"),

    getUsers: () => request<import("../types").UserInfo[]>("/chat/users"),

    createRoom: (name: string, memberIds: number[]) =>
        request<import("../types").ChatRoom>("/chat/rooms", {
            method: "POST",
            body: JSON.stringify({ name, memberIds }),
        }),

    startDM: (userId: number) =>
        request<import("../types").ChatRoom>(`/chat/rooms/dm/${userId}`, {
            method: "POST",
        }),

    getMessages: (roomId: number) =>
        request<import("../types").Message[]>(`/chat/rooms/${roomId}/messages`),

    uploadImage: async (file: File) => {
        const token = getToken();
        const formData = new FormData();
        formData.append("file", file);
        const res = await fetch(`${API_BASE}/chat/upload`, {
            method: "POST",
            headers: token ? { Authorization: `Bearer ${token}` } : {},
            body: formData,
        });
        if (!res.ok) throw new Error(await res.text());
        return res.json() as Promise<{ fileName: string; url: string }>;
    },

    startSession: (latitude?: number | null, longitude?: number | null) =>
        request("/chat/sessions/start", {
            method: "POST",
            body: JSON.stringify({ latitude, longitude }),
        }),

    endSession: () => request("/chat/sessions/end", { method: "POST" }),
};
