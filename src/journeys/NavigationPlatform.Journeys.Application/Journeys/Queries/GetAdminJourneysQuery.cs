using MediatR;
using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Journeys.Queries;

public sealed record GetAdminJourneysQuery(
    Guid? UserId,
    string? TransportType,
    string? StartDateFrom,
    string? StartDateTo,
    string? ArrivalDateFrom,
    string? ArrivalDateTo,
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

