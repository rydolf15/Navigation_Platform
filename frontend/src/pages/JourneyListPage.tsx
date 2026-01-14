import { useEffect, useState } from "react";
import { getJourneys, favoriteJourney, unfavoriteJourney, getJourneyById } from "../api/journeys";
import type { JourneyDto } from "../api/journeys";
import { DailyGoalBadge } from "../components/DailyGoalBadge";
import { logout } from "../api/auth";
import { CreateJourneyModal } from "../components/CreateJourneyModal";
import { Link } from "react-router-dom";
import { FavouriteButton } from "../components/FavouriteButton";
import { NotificationToast, type Notification } from "../components/NotificationToast";
import "../styles/JourneyListPage.css";
import "../styles/NotificationToast.css";

const PAGE_SIZE = 10;

export function JourneyListPage() {
  const [journeys, setJourneys] = useState<JourneyDto[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [dailyGoalAchieved, setDailyGoalAchieved] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [showNotificationsDrawer, setShowNotificationsDrawer] = useState(false);

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

  useEffect(() => {
    let cancelled = false;

    async function loadDailyGoal() {
      // The reward is computed asynchronously by a background worker.
      // Retry a few times so the UI updates shortly after create/edit.
      const maxAttempts = 5;
      const retryDelayMs = 750;

      for (let attempt = 1; attempt <= maxAttempts && !cancelled; attempt++) {
        try {
          const r = await fetch("/api/users/me/daily-goal", { credentials: "include" });
          if (!r.ok) throw new Error("Failed to load daily goal status");

          const data = await r.json();
          const achieved = Boolean(data?.achieved);
          if (!cancelled) setDailyGoalAchieved(achieved);

          if (achieved) return;
        } catch {
          // Keep the current state on transient failures.
        }

        if (attempt < maxAttempts) {
          await new Promise(resolve => setTimeout(resolve, retryDelayMs));
        }
      }
    }

    void loadDailyGoal();

    return () => {
      cancelled = true;
    };
  }, []);

  // Listen for journey notifications
  useEffect(() => {
    let cancelled = false;

    async function handleJourneyUpdated(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;
      if (cancelled) return;

      try {
        const journey = await getJourneyById(evt.detail.journeyId);
        const journeyName = `${journey.startLocation} → ${journey.arrivalLocation}`;
        
        // Optimistically update journey in list
        setJourneys(prev => prev.map(j => 
          j.id === evt.detail.journeyId ? journey : j
        ));
        
        const notification: Notification = {
          id: `updated-${evt.detail.journeyId}-${Date.now()}`,
          type: "updated",
          journeyId: evt.detail.journeyId,
          journeyName,
          timestamp: new Date(),
          read: false,
        };

        setNotifications(prev => [notification, ...prev]);
      } catch {
        // Journey might not exist or user doesn't have access
      }
    }

    function handleJourneyDeleted(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;
      if (cancelled) return;

      // Optimistically remove journey from list
      setJourneys(prev => prev.filter(j => j.id !== evt.detail.journeyId));

      // Try to get journey name from current list before removal
      const journey = journeys.find(j => j.id === evt.detail.journeyId);
      const journeyName = journey 
        ? `${journey.startLocation} → ${journey.arrivalLocation}`
        : undefined;

      const notification: Notification = {
        id: `deleted-${evt.detail.journeyId}-${Date.now()}`,
        type: "deleted",
        journeyId: evt.detail.journeyId,
        journeyName,
        timestamp: new Date(),
        read: false,
      };

      setNotifications(prev => [notification, ...prev]);
    }

    async function handleJourneyShared(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;
      if (cancelled) return;

      try {
        const journey = await getJourneyById(evt.detail.journeyId);
        const journeyName = `${journey.startLocation} → ${journey.arrivalLocation}`;
        
        // Optimistically add journey to list if not already present
        setJourneys(prev => {
          const exists = prev.some(j => j.id === evt.detail.journeyId);
          if (exists) return prev;
          return [journey, ...prev];
        });
        
        const notification: Notification = {
          id: `shared-${evt.detail.journeyId}-${Date.now()}`,
          type: "shared",
          journeyId: evt.detail.journeyId,
          journeyName,
          timestamp: new Date(),
          read: false,
        };

        setNotifications(prev => [notification, ...prev]);
      } catch {
        // Journey might not exist or user doesn't have access
      }
    }

    function handleJourneyUnshared(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;
      if (cancelled) return;

      // Optimistically remove journey from list
      setJourneys(prev => prev.filter(j => j.id !== evt.detail.journeyId));

      // Try to get journey name from current list before removal
      const journey = journeys.find(j => j.id === evt.detail.journeyId);
      const journeyName = journey 
        ? `${journey.startLocation} → ${journey.arrivalLocation}`
        : undefined;

      const notification: Notification = {
        id: `unshared-${evt.detail.journeyId}-${Date.now()}`,
        type: "unshared",
        journeyId: evt.detail.journeyId,
        journeyName,
        timestamp: new Date(),
        read: false,
      };

      setNotifications(prev => [notification, ...prev]);
    }

    async function handleJourneyFavorited(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string; favoritedByUserId: string }>;
      if (cancelled) return;

      try {
        const journey = await getJourneyById(evt.detail.journeyId);
        const journeyName = `${journey.startLocation} → ${journey.arrivalLocation}`;
        
        const notification: Notification = {
          id: `favorited-${evt.detail.journeyId}-${Date.now()}`,
          type: "favorited",
          journeyId: evt.detail.journeyId,
          journeyName,
          timestamp: new Date(),
          read: false,
        };

        setNotifications(prev => [notification, ...prev]);
      } catch {
        // Journey might not exist or user doesn't have access
      }
    }

    window.addEventListener("journey:updated", handleJourneyUpdated);
    window.addEventListener("journey:deleted", handleJourneyDeleted);
    window.addEventListener("journey:shared", handleJourneyShared);
    window.addEventListener("journey:unshared", handleJourneyUnshared);
    window.addEventListener("journey:favorited", handleJourneyFavorited);

    return () => {
      cancelled = true;
      window.removeEventListener("journey:updated", handleJourneyUpdated);
      window.removeEventListener("journey:deleted", handleJourneyDeleted);
      window.removeEventListener("journey:shared", handleJourneyShared);
      window.removeEventListener("journey:unshared", handleJourneyUnshared);
      window.removeEventListener("journey:favorited", handleJourneyFavorited);
    };
  }, [journeys]);

  const totalPages = Math.ceil(total / PAGE_SIZE);

  const unreadCount = notifications.filter(n => !n.read).length;

  const handleDismissNotification = (id: string) => {
    setNotifications(prev => prev.filter(n => n.id !== id));
  };

  const handleMarkAllRead = () => {
    setNotifications(prev => prev.map(n => ({ ...n, read: true })));
  };

  const handleNotificationClick = (notification: Notification) => {
    setNotifications(prev =>
      prev.map(n =>
        n.id === notification.id ? { ...n, read: true } : n
      )
    );
  };

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
            onClick={() => setShowNotificationsDrawer(true)}
            style={{ position: "relative" }}
            title="Notifications"
          >
            🔔
            {unreadCount > 0 && (
              <span
                style={{
                  position: "absolute",
                  top: "-8px",
                  right: "-8px",
                  background: "#dc3545",
                  color: "white",
                  borderRadius: "50%",
                  width: "20px",
                  height: "20px",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                }}
              >
                {unreadCount > 9 ? "9+" : unreadCount}
              </span>
            )}
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
          onCreated={async () => {
            setPage(1);
            // Reload journeys to show the newly created one
            try {
              const result = await getJourneys(1, PAGE_SIZE);
              setJourneys(result.items);
              setTotal(result.totalCount);
            } catch {
              // Ignore errors, user can refresh manually if needed
            }
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

      {/* Notifications Drawer */}
      <div
        className={`notifications-drawer ${
          showNotificationsDrawer ? "notifications-drawer--open" : ""
        }`}
      >
        <div className="notifications-drawer__header">
          <h2 className="notifications-drawer__title">
            Notifications {unreadCount > 0 && `(${unreadCount})`}
          </h2>
          <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
            {unreadCount > 0 && (
              <button
                className="button button-secondary"
                onClick={handleMarkAllRead}
                style={{ fontSize: "0.875rem", padding: "0.25rem 0.5rem" }}
              >
                Mark all read
              </button>
            )}
            <button
              className="notifications-drawer__close"
              onClick={() => setShowNotificationsDrawer(false)}
              aria-label="Close notifications"
            >
              ×
            </button>
          </div>
        </div>
        <div className="notifications-drawer__list">
          {notifications.length === 0 ? (
            <p style={{ textAlign: "center", color: "#6c757d", padding: "2rem" }}>
              No notifications yet
            </p>
          ) : (
            notifications.map(notification => (
              <div
                key={notification.id}
                className={`notification-item ${
                  !notification.read ? "notification-item--unread" : ""
                }`}
                onClick={() => handleNotificationClick(notification)}
              >
                <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                  <span style={{ fontSize: "1.5rem" }}>
                    {notification.type === "updated" && "✏️"}
                    {notification.type === "deleted" && "🗑️"}
                    {notification.type === "shared" && "🔗"}
                  </span>
                  <div style={{ flex: 1 }}>
                    <p style={{ margin: 0, fontWeight: notification.read ? 400 : 600 }}>
                      {notification.type === "updated" &&
                        `${notification.journeyName || "A journey"} was updated`}
                      {notification.type === "deleted" &&
                        `${notification.journeyName || "A journey"} was deleted`}
                      {notification.type === "shared" &&
                        `${notification.journeyName || "A journey"} was shared with you`}
                    </p>
                    <span style={{ fontSize: "0.75rem", color: "#6c757d" }}>
                      {formatTime(notification.timestamp)}
                    </span>
                  </div>
                  {!notification.read && (
                    <span
                      style={{
                        width: "8px",
                        height: "8px",
                        borderRadius: "50%",
                        background: "#007bff",
                      }}
                    />
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      {/* Overlay for drawer */}
      {showNotificationsDrawer && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: "rgba(0, 0, 0, 0.5)",
            zIndex: 1000,
          }}
          onClick={() => setShowNotificationsDrawer(false)}
        />
      )}

      {/* Toast Notifications */}
      <div className="notifications-container">
        {notifications
          .filter(n => !n.read)
          .slice(0, 3)
          .map(notification => (
            <NotificationToast
              key={notification.id}
              notification={notification}
              onDismiss={handleDismissNotification}
            />
          ))}
      </div>
    </main>
  );
}

function formatTime(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSecs = Math.floor(diffMs / 1000);
  const diffMins = Math.floor(diffSecs / 60);
  const diffHours = Math.floor(diffMins / 60);

  if (diffSecs < 60) return "Just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  return date.toLocaleDateString();
}
