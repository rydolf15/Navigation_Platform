import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

let connection: HubConnection | null = null;

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
