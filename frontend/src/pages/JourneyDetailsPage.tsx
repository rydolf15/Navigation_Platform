import { useParams } from "react-router-dom";

export function JourneyDetailsPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <main>
      <h1>Journey Details</h1>
      <p>Journey ID: {id}</p>
    </main>
  );
}
