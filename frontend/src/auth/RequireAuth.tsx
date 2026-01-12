import { useEffect, useState } from "react";
import type { ReactNode } from "react";

interface RequireAuthProps {
  children: ReactNode;
}

export function RequireAuth({ children }: RequireAuthProps) {
  const [authorized, setAuthorized] = useState<boolean | null>(null);

  useEffect(() => {
    fetch("/api/journeys?page=1&pageSize=1", {
      credentials: "include",
    })
      .then(r => setAuthorized(r.ok))
      .catch(() => setAuthorized(false));
  }, []);

  if (authorized === null) return null;

  if (!authorized) {
    window.location.href = "/login";
    return null;
  }

  return <>{children}</>;
}
