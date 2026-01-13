import { apiClient } from "./client";

export async function getShareRecipients(
  journeyId: string
): Promise<string[]> {
  const response = await apiClient.get<{ userIds: string[] }>(
    `/journeys/${journeyId}/share`
  );

  return response.data.userIds ?? [];
}

export async function setShareRecipients(
  journeyId: string,
  userIds: string[]
): Promise<void> {
  await apiClient.post(`/journeys/${journeyId}/share`, {
    userIds,
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
