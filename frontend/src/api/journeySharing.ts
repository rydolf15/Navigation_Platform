import { apiClient } from "./client";

export async function shareJourneyWithUser(
  journeyId: string,
  userId: string
): Promise<void> {
  await apiClient.post(`/journeys/${journeyId}/share`, {
    userId,
  });
}

export async function createPublicShareLink(
  journeyId: string
): Promise<{ url: string }> {
  const response = await apiClient.post<{ url: string }>(
    `/journeys/${journeyId}/public-link`
  );
  return response.data;
}

export async function revokePublicShareLink(
  journeyId: string
): Promise<void> {
  await apiClient.delete(`/journeys/${journeyId}/public-link`);
}
