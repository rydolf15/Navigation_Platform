import axios from "axios";

export interface PublicJourneyDto {
  id: string;
  startLocation: string;
  startTime: string;
  arrivalLocation: string;
  arrivalTime: string;
  transportType: string;
  distanceKm: number;
  isDailyGoalAchieved?: boolean;
  isFavourite?: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canShare: boolean;
  canFavorite: boolean;
}

export async function getPublicJourney(linkId: string): Promise<PublicJourneyDto> {
  const response = await axios.get<PublicJourneyDto>(`/api/public/journeys/${linkId}`, {
    withCredentials: true, // Include credentials if user is authenticated
  });
  return response.data;
}
