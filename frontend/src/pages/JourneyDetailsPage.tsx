import { useEffect, useRef, useState } from "react";
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
import "../styles/JourneyDetailsPage.css";


export function JourneyDetailsPage() {
  const { id } = useParams<{ id: string }>();

  const [journey, setJourney] = useState<JourneyDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const mountedRef = useRef(true);

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
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

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
        if (!cancelled && mountedRef.current) {
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
        if (!cancelled && mountedRef.current) {
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
      if (!mountedRef.current) return;

      setJourney(refreshed);
      setFavourite(refreshed.isFavourite ?? false);
      setEditing(false);

      window.dispatchEvent(
        new CustomEvent("journey:updated", { detail: { journeyId: journey.id } })
      );

      // Daily goal achievement is computed asynchronously by a background worker.
      // If this journey was the one that crossed the threshold, it may be marked shortly after.
      if (!refreshed.isDailyGoalAchieved) {
        void (async () => {
          const maxAttempts = 6;
          const retryDelayMs = 750;

          for (let attempt = 1; attempt <= maxAttempts; attempt++) {
            await new Promise(resolve => setTimeout(resolve, retryDelayMs));

            try {
              const latest = await getJourneyById(journey.id);
              if (!mountedRef.current) return;

              setJourney(latest);
              setFavourite(prev => latest.isFavourite ?? prev);

              if (latest.isDailyGoalAchieved) return;
            } catch {
              // ignore
            }
          }
        })();
      }
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

    if (loading) {
    return <main className="page">Loading…</main>;
  }

  if (error) {
    return (
      <main className="page">
        <p role="alert">{error}</p>
        <Link to="/journeys">Back to journeys</Link>
      </main>
    );
  }

  if (!journey) return null;

  return (
    <main className="page">
      <header className="header">
        <div>
          <h1 className="title">Journey details</h1>
          <Link to="/journeys" className="backLink">
            ← Back to journeys
          </Link>
        </div>

        <button className="logout" onClick={() => void logout()}>
          Log out
        </button>
      </header>

      <section className="card">
        <div className="kv">
          <strong>From</strong><span>{journey.startLocation}</span>
          <strong>To</strong><span>{journey.arrivalLocation}</span>
          <strong>Distance</strong><span>{journey.distanceKm} km</span>
          <strong>Transport</strong><span>{journey.transportType}</span>
        </div>

        {journey.isDailyGoalAchieved && (
          <div className="badge">🏅 Daily distance goal achieved</div>
        )}
      </section>

      {editing && (
        <section className="card editSection">
          <h2>Edit journey</h2>
          {editError && <p className="error">{editError}</p>}

          <div className="form-group">
            <label>Start location</label>
            <input value={editStartLocation} onChange={e => setEditStartLocation(e.target.value)} />
          </div>

          <div className="form-group">
            <label>Arrival location</label>
            <input value={editArrivalLocation} onChange={e => setEditArrivalLocation(e.target.value)} />
          </div>

          <div className="form-group">
            <label>Start time</label>
            <input type="datetime-local" value={editStartTime} onChange={e => setEditStartTime(e.target.value)} />
          </div>

          <div className="form-group">
            <label>Arrival time</label>
            <input type="datetime-local" value={editArrivalTime} onChange={e => setEditArrivalTime(e.target.value)} />
          </div>

          <div className="form-group">
            <label>Transport type</label>
            <select value={editTransportType} onChange={e => setEditTransportType(e.target.value)}>
              <option value="Car">Car</option>
              <option value="Bus">Bus</option>
              <option value="Train">Train</option>
              <option value="Bike">Bike</option>
              <option value="Walk">Walk</option>
            </select>
          </div>

          <div className="form-group">
            <label>Distance (km)</label>
            <input inputMode="decimal" value={editDistanceKm} onChange={e => setEditDistanceKm(e.target.value)} />
          </div>

          <div className="actions">
            <button onClick={saveEdits}>Save</button>
            <button onClick={() => setEditing(false)}>Cancel</button>
          </div>
        </section>
      )}

      <div className="actions">
        <FavouriteButton isFavourite={favourite} onToggle={toggleFavourite} />
        <button onClick={() => setShowShare(true)}>Share</button>
        <button onClick={() => setEditing(v => !v)}>Edit</button>
        <button className="danger" onClick={confirmDelete}>Delete</button>
      </div>

      {showShare && (
        <ShareJourneyModal
          journeyId={journey.id}
          onClose={() => setShowShare(false)}
        />
      )}
    </main>
  );
}


