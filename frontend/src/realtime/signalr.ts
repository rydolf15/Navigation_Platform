import * as signalR from "@microsoft/signalr";

export const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/journeys", {
    withCredentials: true
  })
  .withAutomaticReconnect()
  .build();
