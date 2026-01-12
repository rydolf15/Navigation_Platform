import { apiClient } from "./client";

export async function getJourneys(page: number, pageSize: number) {
  const response = await apiClient.get(
    `/journeys?page=${page}&pageSize=${pageSize}`
  );

  return response.data;
}
