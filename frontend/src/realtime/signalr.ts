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
    await conn.start();
  }
}
