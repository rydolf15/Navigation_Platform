import { apiClient } from "./client";

export async function shareJourneyWithUser(
  journeyId: string,
  userId: string
): Promise<void> {
  await apiClient.post(`/journeys/${journeyId}/share`, {
    userIds: [userId],
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
  publicLinkId: string
): Promise<void> {
  await apiClient.delete(`/journeys/public-link/${publicLinkId}`);
}
