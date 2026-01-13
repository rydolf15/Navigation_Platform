import { useEffect, useState } from "react";
import {
  getShareRecipients,
  createPublicShareLink,
  revokePublicShareLink,
  setShareRecipients,
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
  const [recipients, setRecipients] = useState<string[]>([]);
  const [loadingRecipients, setLoadingRecipients] = useState(true);
  const [publicUrl, setPublicUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;

    async function load() {
      setLoadingRecipients(true);
      setError(null);

      try {
        const userIds = await getShareRecipients(journeyId);
        if (!mounted) return;
        setRecipients(userIds);
      } catch {
        if (!mounted) return;
        setError("Failed to load sharing settings.");
      } finally {
        if (!mounted) return;
        setLoadingRecipients(false);
      }
    }

    load();

    return () => {
      mounted = false;
    };
  }, [journeyId]);

  async function shareWithUser() {
    const trimmed = userId.trim();
    if (!trimmed) return;

    setLoading(true);
    setError(null);

    try {
      const next = Array.from(new Set([...recipients, trimmed]));
      await setShareRecipients(journeyId, next);
      setRecipients(next);
      setUserId("");
    } catch {
      setError("Failed to share journey.");
    } finally {
      setLoading(false);
    }
  }

  async function unshareUser(recipientUserId: string) {
    setLoading(true);
    setError(null);

    try {
      const next = recipients.filter(x => x !== recipientUserId);
      await setShareRecipients(journeyId, next);
      setRecipients(next);
    } catch {
      setError("Failed to unshare journey.");
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
      const linkId = extractPublicLinkId(publicUrl);
      if (!linkId) {
        setError("Invalid public link.");
        return;
      }

      await revokePublicShareLink(linkId);
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
          <strong>Shared with</strong>
          {loadingRecipients ? (
            <p>Loading…</p>
          ) : recipients.length === 0 ? (
            <p>Not shared with anyone yet.</p>
          ) : (
            <ul style={{ paddingLeft: "1rem" }}>
              {recipients.map(id => (
                <li key={id} style={{ marginBottom: 6 }}>
                  <code>{id}</code>{" "}
                  <button
                    onClick={() => unshareUser(id)}
                    disabled={loading}
                    style={{ marginLeft: 8 }}
                  >
                    Remove
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

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

          <button onClick={shareWithUser} disabled={loading || loadingRecipients}>
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

function extractPublicLinkId(url: string | null): string | null {
  if (!url) return null;

  // Handles URLs like: /public/journeys/{guid} or http(s)://host/public/journeys/{guid}
  const match = url.match(/\/public\/journeys\/([0-9a-fA-F-]{36})/);
  return match?.[1] ?? null;
}
