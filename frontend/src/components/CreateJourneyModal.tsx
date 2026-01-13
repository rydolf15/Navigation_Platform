import { useState } from "react";
import { createJourney } from "../api/journeys";
import "../styles/CreateJourneyModal.css";

interface CreateJourneyModalProps {
  onClose(): void;
  onCreated?(id: string): void;
}

const TRANSPORT_TYPES = ["Car", "Bus", "Train", "Bike", "Walk"] as const;

export function CreateJourneyModal({ onClose, onCreated }: CreateJourneyModalProps) {
  const [startLocation, setStartLocation] = useState("");
  const [arrivalLocation, setArrivalLocation] = useState("");
  const [startTime, setStartTime] = useState("");
  const [arrivalTime, setArrivalTime] = useState("");
  const [transportType, setTransportType] =
    useState<(typeof TRANSPORT_TYPES)[number]>("Car");
  const [distanceKm, setDistanceKm] = useState("");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setError(null);

    const dist = Number(distanceKm);
    if (!startLocation.trim() || !arrivalLocation.trim()) {
      setError("Start and arrival locations are required.");
      return;
    }
    if (!startTime || !arrivalTime) {
      setError("Start and arrival times are required.");
      return;
    }
    if (!Number.isFinite(dist) || dist <= 0) {
      setError("Distance must be a positive number.");
      return;
    }
    if (new Date(startTime) >= new Date(arrivalTime)) {
      setError("Start time must be before arrival time.");
      return;
    }

    setLoading(true);

    try {
      const id = await createJourney({
        startLocation: startLocation.trim(),
        startTime,
        arrivalLocation: arrivalLocation.trim(),
        arrivalTime,
        transportType,
        distanceKm: dist,
      });

      onCreated?.(id);
      onClose();
    } catch {
      setError("Failed to create journey.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <section className="modal">
        <h2>Create journey</h2>

        {error && <div className="error" role="alert">{error}</div>}

        <div className="form-group">
          <label>Start location</label>
          <input value={startLocation} onChange={e => setStartLocation(e.target.value)} />
        </div>

        <div className="form-group">
          <label>Arrival location</label>
          <input value={arrivalLocation} onChange={e => setArrivalLocation(e.target.value)} />
        </div>

        <div className="form-group">
          <label>Start time</label>
          <input type="datetime-local" value={startTime} onChange={e => setStartTime(e.target.value)} />
        </div>

        <div className="form-group">
          <label>Arrival time</label>
          <input type="datetime-local" value={arrivalTime} onChange={e => setArrivalTime(e.target.value)} />
        </div>

        <div className="form-group">
          <label>Transport type</label>
          <select value={transportType} onChange={e => setTransportType(e.target.value as any)}>
            {TRANSPORT_TYPES.map(t => (
              <option key={t} value={t}>{t}</option>
            ))}
          </select>
        </div>

        <div className="form-group">
          <label>Distance (km)</label>
          <input inputMode="decimal" value={distanceKm} onChange={e => setDistanceKm(e.target.value)} />
        </div>

        <div className="actions">
          <button className="secondary" onClick={onClose} disabled={loading}>
            Cancel
          </button>
          <button className="primary" onClick={() => void submit()} disabled={loading}>
            Create
          </button>
        </div>
      </section>
    </div>
  );
}
