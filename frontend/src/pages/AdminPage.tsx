import { useEffect, useMemo, useState } from "react";
import { logout } from "../api/auth";
import {
  getAdminJourneys,
  getMonthlyDistanceStats,
  setUserStatus,
  type AdminJourneyDto,
  type TransportType,
  type UserStatus,
} from "../api/admin";
import "../styles/AdminPage.css";

type Tab = "journeys" | "monthly" | "users";

const TRANSPORT_TYPES: TransportType[] = ["Car", "Bus", "Train", "Bike", "Walk"];

export function AdminPage() {
  const [tab, setTab] = useState<Tab>("journeys");

  return (
    <main className="admin-page" style={{ maxWidth: 1100, margin: "2rem auto", padding: "0 1rem" }}>
      <header
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: "1rem",
          marginBottom: "1rem",
        }}
      >
        <div>
          <h1 style={{ margin: 0 }}>Admin</h1>
          <p style={{ margin: "0.25rem 0 0", color: "#4b5563" }}>
            Monitor activity, analytics, and user status.
          </p>
        </div>

        <button
          type="button"
          onClick={() => void logout()}
          style={{
            padding: "0.4rem 0.6rem",
            borderRadius: 6,
            border: "1px solid #e5e7eb",
            background: "white",
            cursor: "pointer",
          }}
        >
          Log out
        </button>
      </header>

      <nav style={{ display: "flex", gap: 8, marginBottom: 16 }}>
        <button
          type="button"
          onClick={() => setTab("journeys")}
          aria-pressed={tab === "journeys"}
        >
          Journeys
        </button>
        <button
          type="button"
          onClick={() => setTab("monthly")}
          aria-pressed={tab === "monthly"}
        >
          Monthly distance
        </button>
        <button
          type="button"
          onClick={() => setTab("users")}
          aria-pressed={tab === "users"}
        >
          Users
        </button>
      </nav>

      {tab === "journeys" && <AdminJourneys />}
      {tab === "monthly" && <MonthlyDistance />}
      {tab === "users" && <AdminUsers />}
    </main>
  );
}

function AdminJourneys() {
  const [userId, setUserId] = useState("");
  const [transportType, setTransportType] = useState<TransportType | "">("");
  const [startDateFrom, setStartDateFrom] = useState("");
  const [startDateTo, setStartDateTo] = useState("");
  const [arrivalDateFrom, setArrivalDateFrom] = useState("");
  const [arrivalDateTo, setArrivalDateTo] = useState("");
  const [minDistance, setMinDistance] = useState("");
  const [maxDistance, setMaxDistance] = useState("");

  const [orderBy, setOrderBy] = useState<string>("StartTime");
  const [direction, setDirection] = useState<"asc" | "desc">("desc");

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  const [items, setItems] = useState<AdminJourneyDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Helper function to convert datetime-local string to UTC ISO string
  const toUtcIsoString = (dateTimeLocal: string): string | undefined => {
    if (!dateTimeLocal) return undefined;
    // datetime-local format is "YYYY-MM-DDTHH:mm"
    // Parse as local time and convert to UTC
    const localDate = new Date(dateTimeLocal);
    if (isNaN(localDate.getTime())) return undefined;
    // Return ISO string in UTC (ends with Z)
    return localDate.toISOString();
  };

  const query = useMemo(
    () => ({
      userId: userId.trim() || undefined,
      transportType: transportType || undefined,
      startDateFrom: toUtcIsoString(startDateFrom),
      startDateTo: toUtcIsoString(startDateTo),
      arrivalDateFrom: toUtcIsoString(arrivalDateFrom),
      arrivalDateTo: toUtcIsoString(arrivalDateTo),
      minDistance: minDistance || undefined,
      maxDistance: maxDistance || undefined,
      page,
      pageSize,
      orderBy,
      direction,
    }),
    [
      userId,
      transportType,
      startDateFrom,
      startDateTo,
      arrivalDateFrom,
      arrivalDateTo,
      minDistance,
      maxDistance,
      page,
      pageSize,
      orderBy,
      direction,
    ]
  );

  async function load() {
    setLoading(true);
    setError(null);

    try {
      const res = await getAdminJourneys(query);
      setItems(res.items);
      setTotalCount(res.totalCount);
    } catch {
      setError("Failed to load admin journeys.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, pageSize, orderBy, direction]);

  return (
    <section>
      <h2 style={{ marginTop: 0 }}>Journey filtering</h2>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
          gap: 12,
          marginBottom: 12,
        }}
      >
        <label>
          UserId
          <input value={userId} onChange={(e) => setUserId(e.target.value)} />
        </label>

        <label>
          TransportType
          <select
            value={transportType}
            onChange={(e) => setTransportType(e.target.value as TransportType | "")}
          >
            <option value="">(any)</option>
            {TRANSPORT_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </label>

        <label>
          StartDateFrom
          <input
            type="datetime-local"
            value={startDateFrom}
            onChange={(e) => setStartDateFrom(e.target.value)}
          />
        </label>

        <label>
          StartDateTo
          <input
            type="datetime-local"
            value={startDateTo}
            onChange={(e) => setStartDateTo(e.target.value)}
          />
        </label>

        <label>
          ArrivalDateFrom
          <input
            type="datetime-local"
            value={arrivalDateFrom}
            onChange={(e) => setArrivalDateFrom(e.target.value)}
          />
        </label>

        <label>
          ArrivalDateTo
          <input
            type="datetime-local"
            value={arrivalDateTo}
            onChange={(e) => setArrivalDateTo(e.target.value)}
          />
        </label>

        <label>
          MinDistance
          <input
            inputMode="decimal"
            value={minDistance}
            onChange={(e) => setMinDistance(e.target.value)}
          />
        </label>

        <label>
          MaxDistance
          <input
            inputMode="decimal"
            value={maxDistance}
            onChange={(e) => setMaxDistance(e.target.value)}
          />
        </label>
      </div>

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "flex-start", marginBottom: 12 }}>
        <label>
          <span style={{ visibility: "hidden" }}>Search</span>
          <button
            type="button"
            onClick={() => {
              setPage(1);
              void load();
            }}
            disabled={loading}
          >
            Search
          </button>
        </label>

        <label>
          OrderBy
          <div className="select-wrapper">
            <select value={orderBy} onChange={(e) => setOrderBy(e.target.value)}>
            <option value="StartTime">StartTime</option>
            <option value="ArrivalTime">ArrivalTime</option>
            <option value="UserId">UserId</option>
            <option value="TransportType">TransportType</option>
            <option value="DistanceKm">DistanceKm</option>
          </select>
          </div>
        </label>

        <label>
          Direction
          <div className="select-wrapper">
            <select
              value={direction}
              onChange={(e) => setDirection(e.target.value as "asc" | "desc")}
            >
            <option value="asc">asc</option>
            <option value="desc">desc</option>
          </select>
          </div>
        </label>

        <label>
          PageSize
          <div className="select-wrapper">
            <select
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
            >
            {[10, 25, 50, 100, 200].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
          </div>
        </label>
      </div>

      {error && <p role="alert">{error}</p>}
      {loading && <p>Loading…</p>}

      {!loading && items.length > 0 && (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>Id</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>UserId</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>Start</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>Arrival</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>Transport</th>
                <th style={{ textAlign: "right", borderBottom: "1px solid #e5e7eb" }}>Km</th>
              </tr>
            </thead>
            <tbody>
              {items.map((j) => (
                <tr key={j.id}>
                  <td style={{ padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    <code>{j.id}</code>
                  </td>
                  <td style={{ padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    <code>{j.userId}</code>
                  </td>
                  <td style={{ padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    {new Date(j.startTime).toLocaleString()}
                  </td>
                  <td style={{ padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    {new Date(j.arrivalTime).toLocaleString()}
                  </td>
                  <td style={{ padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    {j.transportType}
                  </td>
                  <td style={{ textAlign: "right", padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    {j.distanceKm}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div style={{ display: "flex", justifyContent: "space-between", marginTop: 12 }}>
        <button
          type="button"
          onClick={() => setPage((p) => Math.max(1, p - 1))}
          disabled={page <= 1 || loading}
        >
          Previous
        </button>
        <span>
          Page {page} of {totalPages} (total {totalCount})
        </span>
        <button
          type="button"
          onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          disabled={page >= totalPages || loading}
        >
          Next
        </button>
      </div>
    </section>
  );
}

function MonthlyDistance() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [orderBy, setOrderBy] = useState<"UserId" | "TotalDistanceKm">("UserId");
  const [direction, setDirection] = useState<"asc" | "desc">("asc");

  const [items, setItems] = useState<
    { userId: string; year: number; month: number; totalDistanceKm: number }[]
  >([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const res = await getMonthlyDistanceStats({ page, pageSize, orderBy, direction });
        if (cancelled) return;
        setItems(res.items);
        setTotalCount(res.totalCount);
      } catch {
        if (!cancelled) setError("Failed to load monthly distance stats.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [page, pageSize, orderBy, direction]);

  return (
    <section>
      <h2 style={{ marginTop: 0 }}>Monthly distance statistics</h2>

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "flex-start", marginBottom: 12 }}>
        <label>
          OrderBy
          <div className="select-wrapper">
            <select value={orderBy} onChange={(e) => setOrderBy(e.target.value as any)}>
            <option value="UserId">UserId</option>
            <option value="TotalDistanceKm">TotalDistanceKm</option>
          </select>
          </div>
        </label>

        <label>
          Direction
          <div className="select-wrapper">
            <select value={direction} onChange={(e) => setDirection(e.target.value as any)}>
            <option value="asc">asc</option>
            <option value="desc">desc</option>
          </select>
          </div>
        </label>

        <label>
          PageSize
          <div className="select-wrapper">
            <select value={pageSize} onChange={(e) => setPageSize(Number(e.target.value))}>
            {[10, 25, 50, 100, 200, 500].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
          </div>
        </label>
      </div>

      {error && <p role="alert">{error}</p>}
      {loading && <p>Loading…</p>}

      {!loading && items.length > 0 && (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb" }}>UserId</th>
                <th style={{ textAlign: "right", borderBottom: "1px solid #e5e7eb" }}>Year</th>
                <th style={{ textAlign: "right", borderBottom: "1px solid #e5e7eb" }}>Month</th>
                <th style={{ textAlign: "right", borderBottom: "1px solid #e5e7eb" }}>TotalDistanceKm</th>
              </tr>
            </thead>
            <tbody>
              {items.map((x) => (
                <tr key={`${x.userId}:${x.year}:${x.month}`}>
                  <td style={{ padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    <code>{x.userId}</code>
                  </td>
                  <td style={{ textAlign: "right", padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    {x.year}
                  </td>
                  <td style={{ textAlign: "right", padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    {x.month}
                  </td>
                  <td style={{ textAlign: "right", padding: "0.25rem 0", borderBottom: "1px solid #f3f4f6" }}>
                    {x.totalDistanceKm}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div style={{ display: "flex", justifyContent: "space-between", marginTop: 12 }}>
        <button type="button" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1 || loading}>
          Previous
        </button>
        <span>
          Page {page} of {totalPages} (total {totalCount})
        </span>
        <button type="button" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages || loading}>
          Next
        </button>
      </div>
    </section>
  );
}

function AdminUsers() {
  const [userId, setUserId] = useState("");
  const [status, setStatus] = useState<UserStatus>("Suspended");
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setError(null);
    setMessage(null);

    const id = userId.trim();
    if (!id) {
      setError("UserId is required.");
      return;
    }

    setLoading(true);
    try {
      await setUserStatus(id, status);
      setMessage(`User status updated to ${status}.`);
    } catch {
      setError("Failed to update user status.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <section>
      <h2 style={{ marginTop: 0 }}>User management</h2>
      <p style={{ color: "#4b5563" }}>
        Change account status via Keycloak. Suspended/Deactivated users will fail authentication.
      </p>

      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
        <label>
          UserId
          <input value={userId} onChange={(e) => setUserId(e.target.value)} />
        </label>

        <label>
          Status
          <div className="select-wrapper">
            <select value={status} onChange={(e) => setStatus(e.target.value as UserStatus)}>
            <option value="Active">Active</option>
            <option value="Suspended">Suspended</option>
            <option value="Deactivated">Deactivated</option>
          </select>
          </div>
        </label>
      </div>

      <div style={{ marginTop: 12 }}>
        <button type="button" onClick={() => void submit()} disabled={loading}>
          {loading ? "Saving…" : "Apply"}
        </button>
      </div>
    </section>
  );
}

