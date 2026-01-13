using MediatR;
using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Journeys.Queries;

public sealed record GetJourneyByIdQuery(Guid Id) : IRequest<JourneyDto?>;