import { useEffect, useState } from "react";
import { getJourneys } from "../api/journeys";
import type { JourneyDto } from "../api/journeys";
import { JourneyCard } from "../components/JourneyCard";

const PAGE_SIZE = 10;

export function JourneyListPage() {
  const [journeys, setJourneys] = useState<JourneyDto[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      const result = await getJourneys(page, PAGE_SIZE);

      if (!cancelled) {
        setJourneys(result.items);
        setTotal(result.totalCount);
        setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [page]);

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <main style={{ maxWidth: 720, margin: "2rem auto", padding: "0 1rem" }}>
      <h1 style={{ marginBottom: "1rem" }}>Your journeys</h1>

      {loading && <p>Loading…</p>}

      {!loading && journeys.length === 0 && (
        <p>No journeys yet.</p>
      )}

      {!loading &&
        journeys.map(j => (
          <JourneyCard key={j.id} journey={j} />
        ))}

      {totalPages > 1 && (
        <nav
          style={{
            display: "flex",
            justifyContent: "space-between",
            marginTop: "1rem"
          }}
        >
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
          >
            Previous
          </button>

          <span>
            Page {page} of {totalPages}
          </span>

          <button
            onClick={() =>
              setPage(p => Math.min(totalPages, p + 1))
            }
            disabled={page === totalPages}
          >
            Next
          </button>
        </nav>
      )}
    </main>
  );
}
