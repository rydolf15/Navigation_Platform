import { vi } from "vitest";

export const ensureSignalRStarted = vi.fn().mockResolvedValue(undefined);
export const stopSignalR = vi.fn().mockResolvedValue(undefined);
