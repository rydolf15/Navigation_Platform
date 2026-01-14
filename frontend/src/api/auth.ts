import { stopJourneysHub } from "../realtime/journeysHub";
import { getSignalRConnection } from "../realtime/signalr";

export const login = () => {
  window.location.href = "/api/auth/login";
};

export const logout = async () => {
  try {
    // Stop SignalR connections before logout to ensure OnDisconnectedAsync is called
    // This ensures users are marked as offline in Redis immediately
    try {
      const notificationsHub = getSignalRConnection();
      if (notificationsHub.state !== "Disconnected") {
        await notificationsHub.stop();
      }
    } catch {
      // ignore SignalR errors during logout
    }

    try {
      await stopJourneysHub();
    } catch {
      // ignore SignalR errors during logout
    }

    // Now call the logout endpoint
    await fetch("/api/auth/logout", {
      method: "POST",
      credentials: "include",
    });
  } catch {
    // ignore
  } finally {
    // Always route back to the sign-in screen.
    window.location.href = "/login";
  }
};
