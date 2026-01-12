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
