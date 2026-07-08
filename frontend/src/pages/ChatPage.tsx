import { useEffect, useState, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { API_ORIGIN, chatApi } from "../services/api";
import { joinRoom, leaveRoom, sendMessage, sendImage, onMessageReceived } from "../services/signalr";
import type { ChatRoom, Message, UserInfo } from "../types";

export default function ChatPage() {
    const navigate = useNavigate();
    const [rooms, setRooms] = useState<ChatRoom[]>([]);
    const [activeRoom, setActiveRoom] = useState<ChatRoom | null>(null);
    const [messages, setMessages] = useState<Message[]>([]);
    const [input, setInput] = useState("");
    const [showCreate, setShowCreate] = useState(false);
    const [users, setUsers] = useState<UserInfo[]>([]);
    const msgEndRef = useRef<HTMLDivElement>(null);
    const username = localStorage.getItem("username") || "";
    const userId = parseInt(localStorage.getItem("userId") || "0");
    const fileRef = useRef<HTMLInputElement>(null);
    const sessionEnded = useRef(false);

    useEffect(() => {
        if (!localStorage.getItem("token")) {
            navigate("/");
            return;
        }
        loadRooms();
        chatApi.startSession().catch(() => {});

        // Ensure session ends on tab close
        const handleBeforeUnload = () => {
            if (!sessionEnded.current) {
                sessionEnded.current = true;
                const token = localStorage.getItem("token");
                if (token) {
                    fetch(`${API_ORIGIN}/api/chat/sessions/end`, {
                        method: "POST",
                        headers: {
                            Authorization: `Bearer ${token}`,
                            "Content-Type": "application/json",
                        },
                        body: "{}",
                        keepalive: true,
                    }).catch(() => {});
                }
            }
        };
        window.addEventListener("beforeunload", handleBeforeUnload);

        return () => {
            window.removeEventListener("beforeunload", handleBeforeUnload);
            if (!sessionEnded.current) {
                sessionEnded.current = true;
                chatApi.endSession().catch(() => {});
            }
        };
    }, []);

    useEffect(() => {
        onMessageReceived((msg: Message) => {
            if (msg.chatRoomId === activeRoom?.id) {
                setMessages((prev) => [...prev, msg]);
            }
        });
    }, [activeRoom]);

    useEffect(() => {
        msgEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages]);

    async function loadRooms() {
        const r = await chatApi.getRooms();
        setRooms(r);
        if (r.length > 0 && !activeRoom) {
            selectRoom(r[0]);
        }
    }

    async function selectRoom(room: ChatRoom) {
        if (activeRoom) await leaveRoom(activeRoom.id);
        setActiveRoom(room);
        const msgs = await chatApi.getMessages(room.id);
        setMessages(msgs);
        await joinRoom(room.id);
    }

    async function handleSend(e: React.FormEvent) {
        e.preventDefault();
        if (!input.trim() || !activeRoom) return;
        try {
            await sendMessage(activeRoom.id, input);
            setInput("");
        } catch (err) {
            console.error("Failed to send message:", err);
        }
    }

    async function handleImageUpload(e: React.ChangeEvent<HTMLInputElement>) {
        const file = e.target.files?.[0];
        if (!file || !activeRoom) return;
        const result = await chatApi.uploadImage(file);
        await sendImage(activeRoom.id, result.url, file.name);
        if (fileRef.current) fileRef.current.value = "";
    }

    async function startDM(targetUserId: number) {
        const room = await chatApi.startDM(targetUserId);
        // If room is new, add to list
        if (!rooms.find(r => r.id === room.id)) {
            setRooms((prev) => [...prev, room]);
        }
        setShowCreate(false);
        selectRoom(room);
    }

    function handleLogout() {
        sessionEnded.current = true;
        chatApi.endSession().catch(() => {});
        localStorage.clear();
        navigate("/");
    }

    const formatTime = (ts: string) => {
        const d = new Date(ts);
        return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    };

    return (
        <div className="chat-page">
            {/* Sidebar */}
            <aside className="sidebar">
                <div className="sidebar-header">
                    <h2>Chats</h2>
                    <div className="sidebar-actions">
                        <button onClick={async () => {
                            const u = await chatApi.getUsers();
                            setUsers(u);
                            setShowCreate(true);
                        }}>➕ New DM</button>
                        <button className="logout-btn" onClick={handleLogout}>Logout</button>
                    </div>
                </div>

                {showCreate && (
                    <div className="create-room">
                        <h3>Start a DM</h3>
                        <div className="member-list">
                            {users.map((u) => (
                                <div key={u.id} className="member-item" onClick={() => startDM(u.id)}>
                                    {u.username}
                                </div>
                            ))}
                        </div>
                        <button onClick={() => setShowCreate(false)}>Close</button>
                    </div>
                )}

                <nav className="room-list">
                    {rooms.map((r) => (
                        <div
                            key={r.id}
                            className={`room-item ${activeRoom?.id === r.id ? "active" : ""}`}
                            onClick={() => selectRoom(r)}
                        >
                            <span className="room-icon">{r.type === "public" ? "🏠" : "🔒"}</span>
                            <div>
                                <strong>{r.name}</strong>
                                <small>{r.type === "public" ? "Public Room" : `${r.members.length} members`}</small>
                            </div>
                        </div>
                    ))}
                </nav>

                <div className="sidebar-footer">
                    <span>👤 {username}</span>
                </div>
            </aside>

            {/* Chat Area */}
            <main className="chat-area">
                {activeRoom ? (
                    <>
                        <div className="chat-header">
                            <h3>{activeRoom.name}</h3>
                            <span className="badge">{activeRoom.type}</span>
                        </div>

                        <div className="messages">
                            {messages.map((m) => (
                                <div
                                    key={m.id}
                                    className={`message ${m.senderId === userId ? "own" : ""}`}
                                >
                                    <div className="msg-sender">{m.senderName}</div>
                                    {m.messageType === "image" ? (
                                        <div className="msg-image">
                                            {m.content && <p>{m.content}</p>}
                                            {m.imageUrl && (
                                                <img
                                                    src={`${API_ORIGIN}${m.imageUrl}`}
                                                    alt="shared"
                                                    style={{ maxWidth: 300, borderRadius: 8 }}
                                                />
                                            )}
                                        </div>
                                    ) : (
                                        <div className="msg-text">{m.content}</div>
                                    )}
                                    <div className="msg-time">{formatTime(m.timestamp)}</div>
                                </div>
                            ))}
                            <div ref={msgEndRef} />
                        </div>

                        <form className="input-area" onSubmit={handleSend}>
                            {activeRoom.type === "private" && (
                                <>
                                    <input
                                        type="file"
                                        accept="image/*"
                                        ref={fileRef}
                                        style={{ display: "none" }}
                                        onChange={handleImageUpload}
                                    />
                                    <button
                                        type="button"
                                        className="attach-btn"
                                        onClick={() => fileRef.current?.click()}
                                        title="Send image"
                                    >
                                        📎
                                    </button>
                                </>
                            )}
                            <input
                                type="text"
                                placeholder="Type a message..."
                                value={input}
                                onChange={(e) => setInput(e.target.value)}
                            />
                            <button type="submit">Send</button>
                        </form>
                    </>
                ) : (
                    <div className="no-room">
                        <h2>Welcome, {username}!</h2>
                        <p>Select a chat room or create a new one to start messaging.</p>
                    </div>
                )}
            </main>
        </div>
    );
}
