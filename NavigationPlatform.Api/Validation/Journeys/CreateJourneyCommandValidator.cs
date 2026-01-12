using FluentValidation;
using NavigationPlatform.Application.Journeys.Commands;

public sealed class CreateJourneyCommandValidator
    : AbstractValidator<CreateJourneyCommand>
{
    public CreateJourneyCommandValidator()
    {
        RuleFor(x => x.StartLocation).NotEmpty();
        RuleFor(x => x.ArrivalLocation).NotEmpty();
        RuleFor(x => x.DistanceKm).GreaterThan(0).LessThanOrEqualTo(999.99m);
        RuleFor(x => x.StartTime).LessThan(x => x.ArrivalTime);
    }
}