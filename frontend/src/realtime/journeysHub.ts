import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

let connection: HubConnection | null = null;

export function getJourneysHub(): HubConnection {
  if (connection) return connection;

  connection = new HubConnectionBuilder()
    .withUrl("/hubs/journeys", {
      withCredentials: true, // cookies
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build();

  return connection;
}

export async function startJourneysHub(): Promise<void> {
  const hub = getJourneysHub();

  if (hub.state === HubConnectionState.Disconnected) {
    await hub.start();
  }
}

export async function stopJourneysHub(): Promise<void> {
  if (connection && connection.state !== HubConnectionState.Disconnected) {
    await connection.stop();
  }
}
