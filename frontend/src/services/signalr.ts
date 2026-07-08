import * as signalR from "@microsoft/signalr";

let connection: signalR.HubConnection | null = null;
let startPromise: Promise<void> | null = null;
const HUB_URL = `${import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223"}/hubs/chat`;

function createHub(): signalR.HubConnection {
    const token = localStorage.getItem("token");
    const hub = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL, {
            accessTokenFactory: () => token || "",
        })
        .withAutomaticReconnect()
        .build();

    startPromise = hub.start().catch((err) =>
        console.error("SignalR connection error:", err)
    );

    return hub;
}

/** Get the hub instance (synchronous — does not wait for connection). */
function getHub(): signalR.HubConnection {
    if (!connection) connection = createHub();
    return connection;
}

/** Get the hub and wait until the underlying connection is established. */
async function getConnectedHub(): Promise<signalR.HubConnection> {
    const hub = getHub();
    if (startPromise) await startPromise;
    return hub;
}

export async function joinRoom(roomId: number) {
    const hub = await getConnectedHub();
    await hub.invoke("JoinRoom", roomId);
}

export async function leaveRoom(roomId: number) {
    const hub = await getConnectedHub();
    await hub.invoke("LeaveRoom", roomId);
}

export async function sendMessage(chatRoomId: number, content: string) {
    const hub = await getConnectedHub();
    await hub.invoke("SendMessage", { chatRoomId, content });
}

export async function sendImage(roomId: number, imageUrl: string, caption: string) {
    const hub = await getConnectedHub();
    await hub.invoke("SendImage", roomId, imageUrl, caption);
}

export function onMessageReceived(callback: (message: any) => void) {
    const hub = getHub();
    hub.off("ReceiveMessage");
    hub.on("ReceiveMessage", callback);
}
