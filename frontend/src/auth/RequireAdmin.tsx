import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";

interface RequireAdminProps {
  children: ReactNode;
}

// We determine admin access by calling an admin-only endpoint.
// 200 => admin, 403 => not admin, 401 => not authenticated.
export function RequireAdmin({ children }: RequireAdminProps) {
  const [state, setState] = useState<"checking" | "admin" | "not_admin" | "unauthorized">(
    "checking"
  );

  useEffect(() => {
    let cancelled = false;

    async function check() {
      try {
        const res = await fetch(
          "/admin/statistics/monthly-distance?page=1&pageSize=1",
          { credentials: "include" }
        );

        if (cancelled) return;

        if (res.status === 401) {
          setState("unauthorized");
          return;
        }

        if (res.status === 403) {
          setState("not_admin");
          return;
        }

        if (res.ok) {
          setState("admin");
          return;
        }

        // Any other error: treat as not admin to avoid getting stuck.
        setState("not_admin");
      } catch {
        if (!cancelled) setState("not_admin");
      }
    }

    check();

    return () => {
      cancelled = true;
    };
  }, []);

  if (state === "checking") {
    return (
      <div style={{ padding: "2rem" }}>
        <p role="status" aria-live="polite">
          Checking admin access…
        </p>
      </div>
    );
  }

  if (state === "unauthorized") {
    window.location.href = "/login";
    return null;
  }

  if (state === "not_admin") {
    return (
      <main style={{ maxWidth: 720, margin: "2rem auto", padding: "0 1rem" }}>
        <h1>Forbidden</h1>
        <p role="alert">You do not have access to the admin area.</p>
        <Link to="/journeys">Go to journeys</Link>
      </main>
    );
  }

  return <>{children}</>;
}

