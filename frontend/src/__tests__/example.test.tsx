import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { App } from "../App";

// Note: Mocks are defined in setup.ts, no need to duplicate here

describe("App", () => {
  it("renders without crashing", async () => {
    render(<App />);

    // The app should render without errors
    // Since we're mocking RequireAuth, it will just pass through children
    expect(document.body).toBeInTheDocument();
  });
});
