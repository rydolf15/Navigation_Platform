import "@testing-library/jest-dom";
import { afterEach, vi } from "vitest";
import { cleanup } from "@testing-library/react";

// Mock SignalR
vi.mock("../realtime/signalr", () => ({
  ensureSignalRStarted: vi.fn(),
  getSignalRConnection: vi.fn(() => ({
    on: vi.fn(),
    off: vi.fn(),
  })),
}));

// Mock RequireAuth (named export)
vi.mock("../auth/RequireAuth", () => ({
  RequireAuth: ({ children }: { children: React.ReactNode }) => children,
}));

// Mock RequireAdmin (named export)
vi.mock("../auth/RequireAdmin", () => ({
  RequireAdmin: ({ children }: { children: React.ReactNode }) => children,
}));

// Mock journeys API
vi.mock("../api/journeys", () => ({
  getJourneys: vi.fn().mockResolvedValue({
    items: [],
    totalCount: 0,
  }),
  getJourneyById: vi.fn(),
  createJourney: vi.fn(),
  updateJourney: vi.fn(),
  deleteJourney: vi.fn(),
  favoriteJourney: vi.fn(),
  unfavoriteJourney: vi.fn(),
}));

// Mock auth API
vi.mock("../api/auth", () => ({
  logout: vi.fn(),
}));

afterEach(() => {
  cleanup();
});
