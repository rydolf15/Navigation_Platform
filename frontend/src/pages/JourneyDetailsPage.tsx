import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import {
  deleteJourney,
  getJourneyById,
  favoriteJourney,
  unfavoriteJourney,
  updateJourney,
} from "../api/journeys";
import type { JourneyDto } from "../api/journeys";
import { logout } from "../api/auth";
import { FavouriteButton } from "../components/FavouriteButton";
import { ShareJourneyModal } from "../components/ShareJourneyModal";
import { useNavigate } from "react-router-dom";

export function JourneyDetailsPage() {
  const { id } = useParams<{ id: string }>();

  const [journey, setJourney] = useState<JourneyDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [favourite, setFavourite] = useState(false);
  const [saving, setSaving] = useState(false);
  const [showShare, setShowShare] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editStartLocation, setEditStartLocation] = useState("");
  const [editArrivalLocation, setEditArrivalLocation] = useState("");
  const [editStartTime, setEditStartTime] = useState("");
  const [editArrivalTime, setEditArrivalTime] = useState("");
  const [editTransportType, setEditTransportType] = useState("Car");
  const [editDistanceKm, setEditDistanceKm] = useState("");
  const [editError, setEditError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    if (!id) {
      setError("Invalid journey id.");
      setLoading(false);
      return;
    }

    let cancelled = false;

    async function load(journeyId: string) {
      try {
        const data = await getJourneyById(journeyId);
        if (!cancelled) {
          setJourney(data);
          setFavourite(data.isFavourite ?? false);
          setEditStartLocation(data.startLocation ?? "");
          setEditArrivalLocation(data.arrivalLocation ?? "");
          setEditStartTime((data.startTime ?? "").slice(0, 16));
          setEditArrivalTime((data.arrivalTime ?? "").slice(0, 16));
          setEditTransportType(data.transportType ?? "Car");
          setEditDistanceKm(String(data.distanceKm ?? ""));
          setLoading(false);
        }
      } catch {
        if (!cancelled) {
          setError("Failed to load journey.");
          setLoading(false);
        }
      }
    }

    load(id);

    return () => {
      cancelled = true;
    };
    }, [id]);

  useEffect(() => {
    let cancelled = false;

    function onFavouriteChanged(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string; isFavourite: boolean }>;

      if (evt.detail.journeyId !== journey?.id) return;

      setJourney(j => ({ ...j!, isFavourite: evt.detail.isFavourite }));
    }

    async function onUpdated(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;

      if (evt.detail.journeyId !== journey?.id) return;

      try {
        const updated = await getJourneyById(evt.detail.journeyId);
        if (!cancelled) {
          setJourney(updated);
          // Keep current favourite unless the API explicitly returns it.
          setFavourite(prev => updated.isFavourite ?? prev);
        }
      } catch {
        // ignore
      }
    }

    function onDeleted(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;

      if (evt.detail.journeyId === journey?.id) {
        navigate("/");
      }
    }

    window.addEventListener("journey:favourite-changed", onFavouriteChanged);
    window.addEventListener("journey:updated", onUpdated);
    window.addEventListener("journey:deleted", onDeleted);

    return () => {
      cancelled = true;
      window.removeEventListener(
        "journey:favourite-changed",
        onFavouriteChanged
      );
      window.removeEventListener("journey:updated", onUpdated);
      window.removeEventListener("journey:deleted", onDeleted);
    };
  }, [journey?.id, navigate]);


  async function toggleFavourite() {
    if (!journey || saving) return;

    const next = !favourite;
    setSaving(true);
    setFavourite(next); // optimistic

    try {
      if (next) {
        await favoriteJourney(journey.id);
      } else {
        await unfavoriteJourney(journey.id);
      }
    } catch {
      setFavourite(!next); // revert on failure
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <main style={{ padding: "2rem" }}>Loading…</main>;
  }

  if (error) {
    return (
      <main style={{ padding: "2rem" }}>
        <p role="alert">{error}</p>
        <Link to="/journeys">Back to journeys</Link>
      </main>
    );
  }

  if (!journey) return null;

  async function saveEdits() {
    if (!journey || saving) return;

    setEditError(null);

    const dist = Number(editDistanceKm);
    if (!editStartLocation.trim() || !editArrivalLocation.trim()) {
      setEditError("Start and arrival locations are required.");
      return;
    }
    if (!editStartTime || !editArrivalTime) {
      setEditError("Start and arrival times are required.");
      return;
    }
    if (!Number.isFinite(dist) || dist <= 0) {
      setEditError("Distance must be a positive number.");
      return;
    }
    if (new Date(editStartTime).getTime() >= new Date(editArrivalTime).getTime()) {
      setEditError("Start time must be before arrival time.");
      return;
    }

    setSaving(true);

    try {
      await updateJourney(journey.id, {
        startLocation: editStartLocation.trim(),
        startTime: editStartTime,
        arrivalLocation: editArrivalLocation.trim(),
        arrivalTime: editArrivalTime,
        transportType: editTransportType,
        distanceKm: dist,
      });

      const refreshed = await getJourneyById(journey.id);
      setJourney(refreshed);
      setFavourite(refreshed.isFavourite ?? false);
      setEditing(false);

      window.dispatchEvent(
        new CustomEvent("journey:updated", { detail: { journeyId: journey.id } })
      );
    } catch {
      setEditError("Failed to save changes.");
    } finally {
      setSaving(false);
    }
  }

  async function confirmDelete() {
    if (!journey || deleting) return;

    const ok = window.confirm("Delete this journey? This cannot be undone.");
    if (!ok) return;

    setDeleting(true);

    try {
      await deleteJourney(journey.id);
      window.dispatchEvent(
        new CustomEvent("journey:deleted", { detail: { journeyId: journey.id } })
      );
      navigate("/");
    } catch {
      setEditError("Failed to delete journey.");
    } finally {
      setDeleting(false);
    }
  }

  return (
    <main style={{ maxWidth: 720, margin: "2rem auto", padding: "0 1rem" }}>
      <header
        style={{
          marginBottom: "1rem",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          gap: "1rem",
        }}
      >
        <div>
          <h1>Journey details</h1>
          <Link to="/journeys">← Back to journeys</Link>
        </div>

        <button
          type="button"
          onClick={() => void logout()}
          style={{
            padding: "0.4rem 0.6rem",
            borderRadius: 6,
            border: "1px solid #e5e7eb",
            background: "white",
            cursor: "pointer",
          }}
        >
          Log out
        </button>
      </header>

      <section>
        <p><strong>From:</strong> {journey.startLocation}</p>
        <p><strong>To:</strong> {journey.arrivalLocation}</p>
        <p><strong>Distance:</strong> {journey.distanceKm} km</p>
        <p><strong>Transport:</strong> {journey.transportType}</p>
      </section>

      {editing && (
        <section style={{ marginTop: "1rem" }}>
          <h2 style={{ fontSize: "1rem" }}>Edit journey</h2>

          {editError && <p role="alert">{editError}</p>}

          <label style={{ display: "block", marginTop: 8 }}>
            Start location
            <input
              value={editStartLocation}
              onChange={(e) => setEditStartLocation(e.target.value)}
              style={{ width: "100%" }}
            />
          </label>

          <label style={{ display: "block", marginTop: 8 }}>
            Arrival location
            <input
              value={editArrivalLocation}
              onChange={(e) => setEditArrivalLocation(e.target.value)}
              style={{ width: "100%" }}
            />
          </label>

          <label style={{ display: "block", marginTop: 8 }}>
            Start time
            <input
              type="datetime-local"
              value={editStartTime}
              onChange={(e) => setEditStartTime(e.target.value)}
              style={{ width: "100%" }}
            />
          </label>

          <label style={{ display: "block", marginTop: 8 }}>
            Arrival time
            <input
              type="datetime-local"
              value={editArrivalTime}
              onChange={(e) => setEditArrivalTime(e.target.value)}
              style={{ width: "100%" }}
            />
          </label>

          <label style={{ display: "block", marginTop: 8 }}>
            Transport type
            <select
              value={editTransportType}
              onChange={(e) => setEditTransportType(e.target.value)}
              style={{ width: "100%" }}
            >
              {["Car", "Bus", "Train", "Bike", "Walk"].map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </label>

          <label style={{ display: "block", marginTop: 8 }}>
            Distance (km)
            <input
              inputMode="decimal"
              value={editDistanceKm}
              onChange={(e) => setEditDistanceKm(e.target.value)}
              style={{ width: "100%" }}
            />
          </label>

          <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
            <button type="button" onClick={() => void saveEdits()} disabled={saving}>
              Save
            </button>
            <button
              type="button"
              onClick={() => {
                setEditing(false);
                setEditError(null);
              }}
              disabled={saving}
            >
              Cancel
            </button>
          </div>
        </section>
      )}

      {journey.isDailyGoalAchieved && (
        <section style={{ marginTop: "1rem" }}>
          <span role="status">🏅 Daily distance goal achieved</span>
        </section>
      )}

      <section style={{ marginTop: "1.5rem", display: "flex", gap: "0.5rem" }}>
        <FavouriteButton
          isFavourite={favourite}
          onToggle={toggleFavourite}
          disabled={saving}
        />

        <button type="button" onClick={() => setShowShare(true)}>
          Share
        </button>
          {showShare && journey && (
            <ShareJourneyModal
              journeyId={journey.id}
              onClose={() => setShowShare(false)}
            />
          )}

        <button
          type="button"
          onClick={() => {
            setEditing((v) => !v);
            setEditError(null);
          }}
          disabled={saving || deleting}
        >
          {editing ? "Close edit" : "Edit"}
        </button>

        <button
          type="button"
          onClick={() => void confirmDelete()}
          disabled={saving || deleting}
          style={{ color: "#b91c1c" }}
        >
          {deleting ? "Deleting…" : "Delete"}
        </button>

      </section>
    </main>
  );

  
}


