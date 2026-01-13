import { apiClient } from "./client";

export interface JourneyDto {
  id: string;
  startLocation: string;
  startTime: string;
  arrivalLocation: string;
  arrivalTime: string;
  transportType: string;
  distanceKm: number;
  isDailyGoalAchieved: boolean;

  isFavourite?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateJourneyInput {
  startLocation: string;
  startTime: string;
  arrivalLocation: string;
  arrivalTime: string;
  transportType: string;
  distanceKm: number;
}

export async function getJourneys(
  page: number,
  pageSize: number
): Promise<PagedResult<JourneyDto>> {
  const response = await apiClient.get<PagedResult<JourneyDto>>(
    `/journeys?page=${page}&pageSize=${pageSize}`
  );
  return response.data;
}

export async function getJourneyById(id: string): Promise<JourneyDto> {
  const response = await apiClient.get<JourneyDto>(`/journeys/${id}`);
  return response.data;
}

export async function createJourney(input: CreateJourneyInput): Promise<string> {
  const response = await apiClient.post<string>("/journeys", input);
  return response.data;
}

export interface UpdateJourneyInput {
  startLocation: string;
  startTime: string;
  arrivalLocation: string;
  arrivalTime: string;
  transportType: string;
  distanceKm: number;
}

export async function updateJourney(
  id: string,
  input: UpdateJourneyInput
): Promise<void> {
  await apiClient.put(`/journeys/${id}`, {
    journeyId: id,
    ...input,
  });
}

export async function deleteJourney(id: string): Promise<void> {
  await apiClient.delete(`/journeys/${id}`);
}

export async function favoriteJourney(id: string): Promise<void> {
  await apiClient.post(`/journeys/${id}/favorite`);
}

export async function unfavoriteJourney(id: string): Promise<void> {
  await apiClient.delete(`/journeys/${id}/favorite`);
}