import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { getJourneyById, favoriteJourney, unfavoriteJourney } from "../api/journeys";
import type { JourneyDto } from "../api/journeys";
import { FavouriteButton } from "../components/FavouriteButton";
import { ShareJourneyModal } from "../components/ShareJourneyModal";
import { useNavigate } from "react-router-dom";
import { getJourneysHub, startJourneysHub } from "../realtime/journeysHub";
import type {
  JourneyUpdatedEvent,
  JourneyDeletedEvent,
  JourneyFavouriteChangedEvent,
} from "../realtime/events";

export function JourneyDetailsPage() {
  const { id } = useParams<{ id: string }>();

  const [journey, setJourney] = useState<JourneyDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [favourite, setFavourite] = useState(false);
  const [saving, setSaving] = useState(false);
  const [showShare, setShowShare] = useState(false);
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
    if (!journey) return;

    const journeyId = journey.id;
    const hub = getJourneysHub();

    let cancelled = false;

    async function connect() {
      await startJourneysHub();
      if (cancelled) return;

      hub.on("JourneyUpdated", async (e: JourneyUpdatedEvent) => {
        if (e.journeyId !== journeyId) return;

        try {
          const updated = await getJourneyById(journeyId);
          if (!cancelled) {
            setJourney(updated);
            setFavourite(updated.isFavourite ?? false);
          }
        } catch {
          //
        }
      });

      hub.on("JourneyFavouriteChanged", (e: JourneyFavouriteChangedEvent) => {
        if (e.journeyId === journeyId) {
          setFavourite(e.isFavourite);
        }
      });

      hub.on("JourneyDeleted", (e: JourneyDeletedEvent) => {
        if (e.journeyId === journeyId) {
          navigate("/");
        }
      });
    }

    connect();

    return () => {
      cancelled = true;
      hub.off("JourneyUpdated");
      hub.off("JourneyFavouriteChanged");
      hub.off("JourneyDeleted");
    };
    }, [journey?.id, navigate]);

  useEffect(() => {
    function onFavouriteChanged(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string; isFavourite: boolean }>;

      if (evt.detail.journeyId !== journey?.id) return;

      setJourney(j => ({ ...j!, isFavourite: evt.detail.isFavourite }));
    }

    function onDeleted(e: Event) {
      const evt = e as CustomEvent<{ journeyId: string }>;

      if (evt.detail.journeyId === journey?.id) {
        navigate("/journeys");
      }
    }

    window.addEventListener("journey:favourite-changed", onFavouriteChanged);
    window.addEventListener("journey:deleted", onDeleted);

    return () => {
      window.removeEventListener(
        "journey:favourite-changed",
        onFavouriteChanged
      );
      window.removeEventListener("journey:deleted", onDeleted);
    };
  }, [journey?.id]);


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
        <Link to="/">Back to journeys</Link>
      </main>
    );
  }

  if (!journey) return null;

  return (
    <main style={{ maxWidth: 720, margin: "2rem auto", padding: "0 1rem" }}>
      <header style={{ marginBottom: "1rem" }}>
        <h1>Journey details</h1>
        <Link to="/">← Back to journeys</Link>
      </header>

      <section>
        <p><strong>From:</strong> {journey.startLocation}</p>
        <p><strong>To:</strong> {journey.arrivalLocation}</p>
        <p><strong>Distance:</strong> {journey.distanceKm} km</p>
        <p><strong>Transport:</strong> {journey.transportType}</p>
      </section>

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

      </section>
    </main>
  );

  
}


