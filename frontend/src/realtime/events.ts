export interface JourneyFavouriteChangedEvent {
  journeyId: string;
  isFavourite: boolean;
}

export interface JourneyUpdatedEvent {
  journeyId: string;
}

export interface JourneyDeletedEvent {
  journeyId: string;
}

export interface JourneySharedEvent {
  journeyId: string;
}

export interface JourneyUnsharedEvent {
  journeyId: string;
}

export interface JourneyFavoritedEvent {
  journeyId: string;
  favoritedByUserId: string;
}

export interface JourneyDailyGoalAchieved {
  date: string;
  totalDistanceKm: number;
}
