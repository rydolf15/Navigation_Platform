import axios from "axios";

const adminClient = axios.create({
  baseURL: "",
  withCredentials: true,
});

export type TransportType = "Car" | "Bus" | "Train" | "Bike" | "Walk";

export interface AdminJourneyDto {
  id: string;
  userId: string;
  startLocation: string;
  startTime: string;
  arrivalLocation: string;
  arrivalTime: string;
  transportType: string;
  distanceKm: number;
  isDailyGoalAchieved: boolean;
}

export interface GetAdminJourneysParams {
  userId?: string;
  transportType?: TransportType | "";
  startDateFrom?: string;
  startDateTo?: string;
  arrivalDateFrom?: string;
  arrivalDateTo?: string;
  minDistance?: string;
  maxDistance?: string;
  page: number;
  pageSize: number;
  orderBy?: string;
  direction?: "asc" | "desc";
}

export async function getAdminJourneys(params: GetAdminJourneysParams): Promise<{
  items: AdminJourneyDto[];
  totalCount: number;
}> {
  const response = await adminClient.get<{ items: AdminJourneyDto[] }>("/admin/journeys", {
    params: {
      UserId: params.userId || undefined,
      TransportType: params.transportType || undefined,
      StartDateFrom: params.startDateFrom || undefined,
      StartDateTo: params.startDateTo || undefined,
      ArrivalDateFrom: params.arrivalDateFrom || undefined,
      ArrivalDateTo: params.arrivalDateTo || undefined,
      MinDistance: params.minDistance || undefined,
      MaxDistance: params.maxDistance || undefined,
      Page: params.page,
      PageSize: params.pageSize,
      OrderBy: params.orderBy || undefined,
      Direction: params.direction || undefined,
    },
  });

  const header = response.headers["xtotalcount"];
  const totalCount = header ? Number(header) : response.data.items.length;

  return { items: response.data.items, totalCount };
}

export interface MonthlyDistanceDto {
  userId: string;
  year: number;
  month: number;
  totalDistanceKm: number;
}

export async function getMonthlyDistanceStats(params: {
  page: number;
  pageSize: number;
  orderBy: "UserId" | "TotalDistanceKm";
  direction: "asc" | "desc";
}): Promise<{ items: MonthlyDistanceDto[]; totalCount: number }> {
  const response = await adminClient.get<{
    items: MonthlyDistanceDto[];
    page: number;
    pageSize: number;
    totalCount: number;
  }>("/admin/statistics/monthly-distance", {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      orderBy: params.orderBy,
      direction: params.direction,
    },
  });

  return { items: response.data.items, totalCount: response.data.totalCount };
}

export type UserStatus = "Active" | "Suspended" | "Deactivated";

export async function setUserStatus(userId: string, status: UserStatus): Promise<void> {
  await adminClient.patch(`/admin/users/${userId}/status`, { status });
}

