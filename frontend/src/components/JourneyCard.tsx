import type { JourneyDto } from "../api/journeys";
import { Link } from "react-router-dom";

interface Props {
  journey: JourneyDto;
}

export function JourneyCard({ journey }: Props) {
  return (
    <article
      style={{
        border: "1px solid #e5e7eb",
        borderRadius: 6,
        padding: "1rem",
        marginBottom: "0.75rem"
      }}
    >
      <h2 style={{ marginBottom: "0.25rem" }}>
        {journey.startLocation} → {journey.arrivalLocation}
      </h2>

      <p style={{ fontSize: "0.9rem", color: "#4b5563" }}>
        Distance: {journey.distanceKm.toFixed(2)} km · {journey.transportType}
      </p>

      {journey.isDailyGoalAchieved && (
        <span
          style={{
            display: "inline-block",
            marginTop: "0.5rem",
            padding: "0.25rem 0.5rem",
            backgroundColor: "#16a34a",
            color: "white",
            borderRadius: 4,
            fontSize: "0.75rem"
          }}
        >
          Daily goal achieved
        </span>
      )}

      <div style={{ marginTop: "0.75rem" }}>
        <Link to={`/journeys/${journey.id}`}>View details</Link>
      </div>
    </article>
  );
}
