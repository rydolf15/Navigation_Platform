namespace NavigationPlatform.Domain.Journeys;

public readonly record struct DistanceKm
{
    public decimal Value { get; }   
    public DistanceKm(decimal value)
    {
        if (value <= 0 || value > 999.99m)
            throw new ArgumentOutOfRangeException(nameof(value));

        Value = decimal.Round(value, 2);
    }

    public static implicit operator decimal(DistanceKm d) => d.Value;
}