using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavigationPlatform.Application.Journeys.Dtos;

public sealed record DailyGoalStatusDto(
    bool Achieved,
    DateOnly? Date,
    decimal? TotalDistanceKm
);
