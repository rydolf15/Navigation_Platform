import { Link } from "react-router-dom";
import type { JourneyDto } from "../api/journeys";
import { FavouriteButton } from "./FavouriteButton";
import {
  favoriteJourney,
  unfavoriteJourney,
} from "../api/journeys";

interface JourneyCardProps {
  journey: JourneyDto;
  onFavouriteToggle: (next: boolean) => void;
}

export function JourneyCard({
  journey,
  onFavouriteToggle,
}: JourneyCardProps) {
  return (
    <article
      style={{
        padding: "1rem",
        borderBottom: "1px solid #e5e7eb",
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        gap: "1rem",
      }}
    >
      <div>
        <h3 style={{ marginBottom: "0.25rem" }}>
          <Link to={`/journeys/${journey.id}`}>
            {journey.startLocation} → {journey.arrivalLocation}
          </Link>
        </h3>

        <p style={{ margin: 0 }}>
          {journey.distanceKm} km · {journey.transportType}
        </p>
      </div>

      <FavouriteButton
        isFavourite={journey.isFavourite ?? false}
        onToggle={async () => {
          const next = !(journey.isFavourite ?? false);

          // optimistic update
          onFavouriteToggle(next);

          try {
            if (next) {
              await favoriteJourney(journey.id);
            } else {
              await unfavoriteJourney(journey.id);
            }
          } catch {
            // revert on failure
            onFavouriteToggle(!next);
          }
        }}
      />
    </article>
  );
}
