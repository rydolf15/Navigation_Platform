import { useEffect, useState } from "react";
import { getJourneys } from "../api/journeys";
import type { JourneyDto } from "../api/journeys";
import { JourneyCard } from "../components/JourneyCard";
import { DailyGoalBadge } from "../components/DailyGoalBadge";

import {
  getJourneysHub,
  startJourneysHub,
  stopJourneysHub,
} from "../realtime/journeysHub";

import type {
  JourneyUpdatedEvent,
  JourneyDeletedEvent,
  JourneyFavouriteChangedEvent,
} from "../realtime/events";

const PAGE_SIZE = 10;

export function JourneyListPage() {
  const [journeys, setJourneys] = useState<JourneyDto[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [dailyGoalAchieved, setDailyGoalAchieved] = useState(false);

  // Data loading (paged)
  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      const result = await getJourneys(page, PAGE_SIZE);

      if (!cancelled) {
        setJourneys(result.items);
        setTotal(result.totalCount);
        setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [page]);

  // Realtime updates (SignalR)
  useEffect(() => {
    let cancelled = false;
    const hub = getJourneysHub();

    async function connect() {
      await startJourneysHub();
      if (cancelled) return;

      hub.on("JourneyUpdated", (e: JourneyUpdatedEvent) => {
        setJourneys(items =>
          items.map(j =>
            j.id === e.journeyId ? { ...j } : j
          )
        );
      });

      hub.on("JourneyDeleted", (e: JourneyDeletedEvent) => {
        setJourneys(items =>
          items.filter(j => j.id !== e.journeyId)
        );
      });

      hub.on("JourneyFavouriteChanged", (e: JourneyFavouriteChangedEvent) => {
        setJourneys(items =>
          items.map(j =>
            j.id === e.journeyId
              ? { ...j, isFavourite: e.isFavourite }
              : j
          )
        );
      });

      hub.on("JourneyDailyGoalAchieved", e => {
        setDailyGoalAchieved(true);

        window.dispatchEvent(
          new CustomEvent("daily-goal-achieved", {
            detail: e,
          })
        );
      });
    }

    connect();

    return () => {
      cancelled = true;
      hub.off("JourneyUpdated");
      hub.off("JourneyDeleted");
      hub.off("JourneyFavouriteChanged");
      stopJourneysHub();
    };
  }, []);

  useEffect(() => {
    function onFavouriteChanged(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string; isFavourite: boolean }>;

      setJourneys(journeys =>
        journeys.map(j =>
          j.id === evt.detail.journeyId
            ? { ...j, isFavourite: evt.detail.isFavourite }
            : j
        )
      );
    }

    function onDeleted(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;

      setJourneys(journeys =>
        journeys.filter(j => j.id !== evt.detail.journeyId)
      );
    }

    function onDailyGoal() {
      setDailyGoalAchieved(true);
    }

    window.addEventListener("journey:favourite-changed", onFavouriteChanged);
    window.addEventListener("journey:deleted", onDeleted);
    window.addEventListener("daily-goal-achieved", onDailyGoal);

    return () => {
      window.removeEventListener(
        "journey:favourite-changed",
        onFavouriteChanged
      );
      window.removeEventListener("journey:deleted", onDeleted);
      window.removeEventListener("daily-goal-achieved", onDailyGoal);
    };
  }, []);

  useEffect(() => {
    fetch("/api/users/me/daily-goal", { credentials: "include" })
      .then(r => r.ok ? r.json() : null)
      .then(data => {
        if (data?.achieved) setDailyGoalAchieved(true);
      });
  }, []);

  <DailyGoalBadge achieved={dailyGoalAchieved} />

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    
    <main style={{ maxWidth: 720, margin: "2rem auto", padding: "0 1rem" }}>
      <h1 style={{ marginBottom: "1rem" }}>Your journeys</h1>

      {loading && <p>Loading…</p>}

      {!loading && journeys.length === 0 && (
        <p>No journeys yet.</p>
      )}

      {!loading &&
        journeys.map(j => (
          <JourneyCard
            key={j.id}
            journey={j}
            onFavouriteToggle={(next) =>
              setJourneys(items =>
                items.map(x =>
                  x.id === j.id ? { ...x, isFavourite: next } : x
                )
              )
            }
          />
        ))}

      {totalPages > 1 && (
        <nav
          style={{
            display: "flex",
            justifyContent: "space-between",
            marginTop: "1rem",
          }}
        >
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
          >
            Previous
          </button>

          <span>
            Page {page} of {totalPages}
          </span>

          <button
            onClick={() =>
              setPage(p => Math.min(totalPages, p + 1))
            }
            disabled={page === totalPages}
          >
            Next
          </button>
        </nav>
      )}
    </main>
  );
}