import { RouterProvider } from "react-router-dom";
import { useEffect } from "react";
import { router } from "./router";
import { ensureSignalRStarted, getSignalRConnection } from "./realtime/signalr";
import { startJourneysHub } from "./realtime/journeysHub";
import type {
  JourneyFavouriteChangedEvent,
  JourneyUpdatedEvent,
  JourneyDeletedEvent,
  JourneySharedEvent,
  JourneyDailyGoalAchieved,
} from "./realtime/events";

export function App() {
  useEffect(() => {
    let mounted = true;

    async function start() {
      await ensureSignalRStarted();
      if (!mounted) return;

      const conn = getSignalRConnection();

      // Presence hub: used by backend to decide whether to fallback to email when offline.
      // It may fail with 401 before login; ignore and retry on next app load (after auth redirect).
      try {
        await startJourneysHub();
      } catch {
        // ignore
      }

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

      conn.on("JourneyDailyGoalAchieved", (evt: JourneyDailyGoalAchieved) => {
        window.dispatchEvent(
          new CustomEvent("daily-goal-achieved", { detail: evt })
        );
      });
    }

    start();

    return () => {
      mounted = false;
    };
  }, []);

  return <RouterProvider router={router} />;
}
