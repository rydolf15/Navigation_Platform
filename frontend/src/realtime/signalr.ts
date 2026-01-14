import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

let connection: HubConnection | null = null;
let listenersRegistered = false;

export function getSignalRConnection(): HubConnection {
  if (connection) return connection;

  connection = new HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
      withCredentials: true,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();

  return connection;
}

export function areListenersRegistered(): boolean {
  return listenersRegistered;
}

export function markListenersRegistered(): void {
  listenersRegistered = true;
}

export async function ensureSignalRStarted(): Promise<void> {
  const conn = getSignalRConnection();

  if (conn.state === HubConnectionState.Disconnected) {
    try {
      await conn.start();
    } catch {
      // Likely not authenticated yet (401). We'll retry after login (full page reload),
      // or the next time this function is called.
    }
  }
}
