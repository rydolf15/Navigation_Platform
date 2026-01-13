import { useEffect, useState } from "react";
import { getJourneys, favoriteJourney, unfavoriteJourney } from "../api/journeys";
import type { JourneyDto } from "../api/journeys";
import { DailyGoalBadge } from "../components/DailyGoalBadge";
import { logout } from "../api/auth";
import { CreateJourneyModal } from "../components/CreateJourneyModal";
import { Link } from "react-router-dom";
import { FavouriteButton } from "../components/FavouriteButton";
import "../styles/JourneyListPage.css";

const PAGE_SIZE = 10;

export function JourneyListPage() {
  const [journeys, setJourneys] = useState<JourneyDto[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [dailyGoalAchieved, setDailyGoalAchieved] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);

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
  }, [page, reloadToken]);

  useEffect(() => {
    fetch("/api/users/me/daily-goal", { credentials: "include" })
      .then(r => (r.ok ? r.json() : null))
      .then(data => {
        if (data?.achieved) setDailyGoalAchieved(true);
      });
  }, []);

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <main className="page">
      <header className="page-header">
        <h1>Your journeys</h1>

        <div className="header-actions">
          <button
            className="button button-primary"
            onClick={() => setShowCreate(true)}
          >
            Create journey
          </button>

          <button
            className="button button-secondary"
            onClick={() => void logout()}
          >
            Log out
          </button>
        </div>
      </header>

      <DailyGoalBadge achieved={dailyGoalAchieved} />

      {showCreate && (
        <CreateJourneyModal
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            setPage(1);
            setReloadToken(x => x + 1);
          }}
        />
      )}

      {loading && <p>Loading…</p>}

      {!loading && journeys.length === 0 && (
        <div className="empty-state">
          <p>No journeys yet.</p>
          <p>Create your first journey to get started.</p>
        </div>
      )}

      {!loading && journeys.length > 0 && (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Journey</th>
                <th>Distance</th>
                <th>Transport</th>
                <th style={{ textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {journeys.map(j => (
                <tr key={j.id}>
                  <td>{j.startLocation} → {j.arrivalLocation}</td>
                  <td>{j.distanceKm} km</td>
                  <td>{j.transportType}</td>
                  <td>
                    <div className="actions-cell">
                      <Link
                        to={`/journeys/${j.id}`}
                        className="icon-button"
                        title="View details"
                        aria-label="View journey details"
                      >
                        ℹ
                      </Link>

                      <FavouriteButton
                        isFavourite={j.isFavourite ?? false}
                        onToggle={async () => {
                          const next = !(j.isFavourite ?? false);

                          setJourneys(items =>
                            items.map(x =>
                              x.id === j.id ? { ...x, isFavourite: next } : x
                            )
                          );

                          try {
                            next
                              ? await favoriteJourney(j.id)
                              : await unfavoriteJourney(j.id);
                          } catch {
                            setJourneys(items =>
                              items.map(x =>
                                x.id === j.id
                                  ? { ...x, isFavourite: !next }
                                  : x
                              )
                            );
                          }
                        }}
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && (
        <nav className="pagination">
          <button
            className="button button-secondary"
            disabled={page === 1}
            onClick={() => setPage(p => Math.max(1, p - 1))}
          >
            Previous
          </button>

          <span>
            Page {page} of {totalPages}
          </span>

          <button
            className="button button-secondary"
            disabled={page === totalPages}
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
          >
            Next
          </button>
        </nav>
      )}
    </main>
  );
}
