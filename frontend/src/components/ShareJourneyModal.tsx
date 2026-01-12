import { useState } from "react";
import {
  shareJourneyWithUser,
  createPublicShareLink,
  revokePublicShareLink,
} from "../api/journeySharing";

interface ShareJourneyModalProps {
  journeyId: string;
  onClose(): void;
}

export function ShareJourneyModal({
  journeyId,
  onClose,
}: ShareJourneyModalProps) {
  const [userId, setUserId] = useState("");
  const [publicUrl, setPublicUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function shareWithUser() {
    if (!userId) return;

    setLoading(true);
    setError(null);

    try {
      await shareJourneyWithUser(journeyId, userId);
      setUserId("");
    } catch {
      setError("Failed to share journey.");
    } finally {
      setLoading(false);
    }
  }

  async function generateLink() {
    setLoading(true);
    setError(null);

    try {
      const result = await createPublicShareLink(journeyId);
      setPublicUrl(result.url);
    } catch {
      setError("Failed to create public link.");
    } finally {
      setLoading(false);
    }
  }

  async function revokeLink() {
    setLoading(true);
    setError(null);

    try {
      await revokePublicShareLink(journeyId);
      setPublicUrl(null);
    } catch {
      setError("Failed to revoke link.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.4)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <section
        style={{
          background: "white",
          padding: "1rem",
          borderRadius: 6,
          width: 360,
        }}
      >
        <h2>Share journey</h2>

        {error && <p role="alert">{error}</p>}

        <div style={{ marginBottom: "1rem" }}>
          <label>
            Share with user ID
            <input
              type="text"
              value={userId}
              onChange={e => setUserId(e.target.value)}
              style={{ width: "100%" }}
            />
          </label>

          <button onClick={shareWithUser} disabled={loading}>
            Share
          </button>
        </div>

        <hr />

        <div style={{ marginTop: "1rem" }}>
          {!publicUrl && (
            <button onClick={generateLink} disabled={loading}>
              Create public link
            </button>
          )}

          {publicUrl && (
            <>
              <p>
                <input
                  readOnly
                  value={publicUrl}
                  style={{ width: "100%" }}
                />
              </p>

              <button onClick={() => navigator.clipboard.writeText(publicUrl)}>
                Copy
              </button>

              <button onClick={revokeLink} disabled={loading}>
                Revoke
              </button>
            </>
          )}
        </div>

        <div style={{ marginTop: "1rem" }}>
          <button onClick={onClose}>Close</button>
        </div>
      </section>
    </div>
  );
}
