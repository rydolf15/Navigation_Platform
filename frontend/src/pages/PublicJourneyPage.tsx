import { useEffect, useState } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { getPublicJourney } from "../api/publicJourneys";
import type { PublicJourneyDto } from "../api/publicJourneys";
import { favoriteJourney, unfavoriteJourney, deleteJourney } from "../api/journeys";
import { FavouriteButton } from "../components/FavouriteButton";
import { ShareJourneyModal } from "../components/ShareJourneyModal";
import "../styles/JourneyDetailsPage.css";

export function PublicJourneyPage() {
  const { linkId } = useParams<{ linkId: string }>();
  const [journey, setJourney] = useState<PublicJourneyDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [favourite, setFavourite] = useState(false);
  const [saving, setSaving] = useState(false);
  const [showShare, setShowShare] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    if (!linkId) {
      setError("Invalid link id.");
      setLoading(false);
      return;
    }

    let cancelled = false;

    async function load() {
      if (!linkId) return;
      try {
        const data = await getPublicJourney(linkId);
        if (!cancelled) {
          setJourney(data);
          setFavourite(data.isFavourite ?? false);
          setLoading(false);
        }
      } catch (err: any) {
        if (!cancelled) {
          if (err.response?.status === 410) {
            setError("This public link has been revoked.");
          } else {
            setError("Failed to load journey.");
          }
          setLoading(false);
        }
      }
    }

    load();

    return () => {
      cancelled = true;
    };
  }, [linkId]);

  async function toggleFavourite() {
    if (!journey || saving || !journey.canFavorite) return;

    const next = !favourite;
    setSaving(true);
    setFavourite(next);

    try {
      if (next) {
        await favoriteJourney(journey.id);
      } else {
        await unfavoriteJourney(journey.id);
      }
    } catch {
      setFavourite(!next);
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <main className="page">Loading…</main>;
  }

  if (error) {
    return (
      <main className="page">
        <p role="alert">{error}</p>
        <Link to="/">Back to home</Link>
      </main>
    );
  }

  if (!journey) return null;

  return (
    <main className="page">
      <header className="header">
        <div>
          <h1 className="title">Journey details</h1>
          <Link to="/" className="backLink">
            ← Back to home
          </Link>
        </div>
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

      {(journey.canFavorite || journey.canShare || journey.canEdit || journey.canDelete) && (
        <div className="actions">
          {journey.canFavorite && (
            <FavouriteButton isFavourite={favourite} onToggle={toggleFavourite} />
          )}
          {journey.canShare && (
            <button onClick={() => setShowShare(true)}>Share</button>
          )}
          {journey.canEdit && (
            <button onClick={() => navigate(`/journeys/${journey.id}`)}>Edit</button>
          )}
          {journey.canDelete && (
            <button className="danger" onClick={async () => {
              if (window.confirm("Delete this journey? This cannot be undone.")) {
                try {
                  await deleteJourney(journey.id);
                  navigate("/");
                } catch {
                  // Handle error
                }
              }
            }}>Delete</button>
          )}
        </div>
      )}

      {showShare && journey.canShare && (
        <ShareJourneyModal
          journeyId={journey.id}
          onClose={() => setShowShare(false)}
        />
      )}
    </main>
  );
}
