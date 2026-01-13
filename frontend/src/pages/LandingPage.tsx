import { useEffect } from "react";
import { useNavigate } from "react-router-dom";

export function LandingPage() {
  const navigate = useNavigate();

  useEffect(() => {
    let cancelled = false;

    async function decide() {
      try {
        // Admin-only endpoint: 200 => admin, 403 => not admin.
        const res = await fetch(
          "/admin/statistics/monthly-distance?page=1&pageSize=1",
          { credentials: "include" }
        );

        if (cancelled) return;

        if (res.ok) {
          navigate("/admin", { replace: true });
          return;
        }

        navigate("/journeys", { replace: true });
      } catch {
        if (!cancelled) navigate("/journeys", { replace: true });
      }
    }

    decide();

    return () => {
      cancelled = true;
    };
  }, [navigate]);

  return (
    <main style={{ padding: "2rem" }}>
      <p role="status" aria-live="polite">
        Loading…
      </p>
    </main>
  );
}

