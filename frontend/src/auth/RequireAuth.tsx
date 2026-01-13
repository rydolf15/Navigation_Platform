import { useEffect, useState } from "react";
import type { ReactNode } from "react";

interface RequireAuthProps {
  children: ReactNode;
}

export function RequireAuth({ children }: RequireAuthProps) {
  const [authorized, setAuthorized] = useState<boolean | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function checkAuth() {
      try {
        const response = await fetch("/api/journeys?page=1&pageSize=1", {
          credentials: "include",
        });

        if (cancelled) return;

        if (response.status === 401) {
          setAuthorized(false);
        } else {
          setAuthorized(true);
        }
      } catch {
        if (!cancelled) {
          setAuthorized(true);
        }
      }
    }

    checkAuth();

    return () => {
      cancelled = true;
    };
  }, []);

  if (authorized === null) {
    return (
      <div style={{ padding: "2rem" }}>
        <p role="status" aria-live="polite">
          Checking your session…
        </p>
      </div>
    );
  }

  if (!authorized) {
    window.location.href = "/login";
    return null;
  }

  return <>{children}</>;
}
