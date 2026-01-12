import { login } from "../api/auth";

export function LoginPage() {
  return (
    <main
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <section
        style={{
          maxWidth: 420,
          width: "100%",
          padding: "2rem",
          border: "1px solid #e5e7eb",
          borderRadius: 8,
        }}
        aria-labelledby="login-title"
      >
        <h1 id="login-title" style={{ marginBottom: "0.5rem" }}>
          Navigation Platform
        </h1>

        <p style={{ marginBottom: "1.5rem", color: "#4b5563" }}>
          Sign in to manage your journeys and rewards.
        </p>

        <button
          type="button"
          onClick={login}
          style={{
            width: "100%",
            padding: "0.75rem",
            fontSize: "1rem",
            fontWeight: 600,
            cursor: "pointer",
            borderRadius: 6,
            border: "none",
            backgroundColor: "#2563eb",
            color: "white",
          }}
          aria-label="Sign in"
        >
          Sign in
        </button>
      </section>
    </main>
  );
}
