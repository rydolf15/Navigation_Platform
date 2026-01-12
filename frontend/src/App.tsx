import { RouterProvider } from "react-router-dom";
import { useEffect } from "react";
import { router } from "./router";
import { ensureSignalRStarted, getSignalRConnection } from "./realtime/signalr";
import type {
  JourneyFavouriteChangedEvent,
  JourneyUpdatedEvent,
  JourneyDeletedEvent,
} from "./realtime/events";

export function App() {
  useEffect(() => {
    let mounted = true;

    async function start() {
      await ensureSignalRStarted();
      if (!mounted) return;

      const conn = getSignalRConnection();

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
    }

    start();

    return () => {
      mounted = false;
    };
  }, []);

  return <RouterProvider router={router} />;
}
