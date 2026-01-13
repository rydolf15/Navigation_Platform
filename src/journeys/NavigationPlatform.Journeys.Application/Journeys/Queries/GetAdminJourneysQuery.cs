using MediatR;
using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Journeys.Queries;

public sealed record GetAdminJourneysQuery(
    Guid? UserId,
    string? TransportType,
    DateTime? StartDateFrom,
    DateTime? StartDateTo,
    DateTime? ArrivalDateFrom,
    DateTime? ArrivalDateTo,
    decimal? MinDistance,
    decimal? MaxDistance,
    int Page,
    int PageSize,
    string? OrderBy,
    string? Direction
) : IRequest<AdminJourneysResult>;

public sealed record AdminJourneysResult(
    IReadOnlyCollection<AdminJourneyDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

