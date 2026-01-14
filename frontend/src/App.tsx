import { RouterProvider } from "react-router-dom";
import { useEffect } from "react";
import { router } from "./router";
import { ensureSignalRStarted, getSignalRConnection, areListenersRegistered, markListenersRegistered } from "./realtime/signalr";
import { startJourneysHub } from "./realtime/journeysHub";
import type {
  JourneyFavouriteChangedEvent,
  JourneyUpdatedEvent,
  JourneyDeletedEvent,
  JourneySharedEvent,
  JourneyUnsharedEvent,
  JourneyFavoritedEvent,
  JourneyDailyGoalAchieved,
} from "./realtime/events";

export function App() {
  useEffect(() => {
    let mounted = true;

    async function start() {
      const conn = getSignalRConnection();

      // Set up event listeners only once (they persist even if connection fails initially)
      // Check if listeners are already registered to avoid duplicates
      if (!areListenersRegistered()) {
        conn.on(
          "JourneyFavouriteChanged",
          (evt: JourneyFavouriteChangedEvent) => {
            window.dispatchEvent(
              new CustomEvent("journey:favourite-changed", {
                detail: evt,
              })
            );
          }
        );

        conn.on("JourneyUpdated", (evt: JourneyUpdatedEvent) => {
          window.dispatchEvent(
            new CustomEvent("journey:updated", { detail: evt })
          );
        });

        conn.on("JourneyDeleted", (evt: JourneyDeletedEvent) => {
          window.dispatchEvent(
            new CustomEvent("journey:deleted", { detail: evt })
          );
        });

        conn.on("JourneyShared", (evt: JourneySharedEvent) => {
          window.dispatchEvent(
            new CustomEvent("journey:shared", { detail: evt })
          );
        });

        conn.on("JourneyUnshared", (evt: JourneyUnsharedEvent) => {
          window.dispatchEvent(
            new CustomEvent("journey:unshared", { detail: evt })
          );
        });

        conn.on("JourneyFavorited", (evt: JourneyFavoritedEvent) => {
          window.dispatchEvent(
            new CustomEvent("journey:favorited", { detail: evt })
          );
        });

        conn.on("JourneyDailyGoalAchieved", (evt: JourneyDailyGoalAchieved) => {
          window.dispatchEvent(
            new CustomEvent("daily-goal-achieved", { detail: evt })
          );
        });

        markListenersRegistered();
      }

      // Now try to start the connections
      try {
        await ensureSignalRStarted();
        if (!mounted) return;
      } catch (err) {
        console.warn("SignalR notifications connection failed, will retry:", err);
        // Connection will be retried automatically by withAutomaticReconnect()
      }

      // Presence hub: used by backend to decide whether to fallback to email when offline.
      // It may fail with 401 before login; ignore and retry on next app load (after auth redirect).
      try {
        await startJourneysHub();
      } catch {
        // ignore
      }
    }

    start();

    return () => {
      mounted = false;
    };
  }, []);

  return <RouterProvider router={router} />;
}
