import * as signalR from "@microsoft/signalr";

let connection: signalR.HubConnection | null = null;

export function getHub(): signalR.HubConnection {
    if (!connection) {
        const token = localStorage.getItem("token");
        connection = new signalR.HubConnectionBuilder()
            .withUrl("http://192.168.1.102:5000/hubs/chat", {
                accessTokenFactory: () => token || "",
            })
            .withAutomaticReconnect()
            .build();

        connection.start().catch((err) => console.error("SignalR error:", err));
    }
    return connection;
}

export async function joinRoom(roomId: number) {
    const hub = getHub();
    if (hub.state === signalR.HubConnectionState.Connected) {
        await hub.invoke("JoinRoom", roomId);
    }
}

export async function leaveRoom(roomId: number) {
    const hub = getHub();
    if (hub.state === signalR.HubConnectionState.Connected) {
        await hub.invoke("LeaveRoom", roomId);
    }
}

export async function sendMessage(chatRoomId: number, content: string) {
    const hub = getHub();
    await hub.invoke("SendMessage", { chatRoomId, content });
}

export async function sendImage(roomId: number, imageUrl: string, caption: string) {
    const hub = getHub();
    await hub.invoke("SendImage", roomId, imageUrl, caption);
}

export function onMessageReceived(callback: (message: any) => void) {
    const hub = getHub();
    hub.off("ReceiveMessage");
    hub.on("ReceiveMessage", callback);
}
