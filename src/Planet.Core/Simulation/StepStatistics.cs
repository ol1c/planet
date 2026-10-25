namespace Planet.Core.Simulation;

public readonly record struct AnimalBirth(int Id, int X, int Y);

public sealed record StepStatistics
{
    public int PlantBirths { get; init; }
    public int PlantDeaths { get; init; }
    public int PlantsEaten { get; init; }
    public int AnimalBirths => Births.Count;
    public int AnimalDeathsOfAge { get; init; }
    public int AnimalDeathsOfCrowding { get; init; }
    public int AnimalDeathsOfHunger { get; init; }
    public int AnimalMoves { get; init; }

    public IReadOnlyList<AnimalBirth> Births { get; init; } = [];

    public int AnimalDeaths => AnimalDeathsOfAge + AnimalDeathsOfCrowding + AnimalDeathsOfHunger;

    public static StepStatistics None { get; } = new();
}

public sealed class SimulationStatistics
{
    public int Steps { get; private set; }
    public long PlantBirths { get; private set; }
    public long PlantDeaths { get; private set; }
    public long PlantsEaten { get; private set; }
    public long AnimalBirths { get; private set; }
    public long AnimalDeathsOfAge { get; private set; }
    public long AnimalDeathsOfCrowding { get; private set; }
    public long AnimalDeathsOfHunger { get; private set; }

    public long AnimalDeaths => AnimalDeathsOfAge + AnimalDeathsOfCrowding + AnimalDeathsOfHunger;

    public StepStatistics LastStep { get; private set; } = StepStatistics.None;

    public void Add(StepStatistics step)
    {
        Steps++;
        PlantBirths += step.PlantBirths;
        PlantDeaths += step.PlantDeaths;
        PlantsEaten += step.PlantsEaten;
        AnimalBirths += step.AnimalBirths;
        AnimalDeathsOfAge += step.AnimalDeathsOfAge;
        AnimalDeathsOfCrowding += step.AnimalDeathsOfCrowding;
        AnimalDeathsOfHunger += step.AnimalDeathsOfHunger;
        LastStep = step;
    }

    public void Reset()
    {
        Steps = 0;
        PlantBirths = PlantDeaths = PlantsEaten = 0;
        AnimalBirths = AnimalDeathsOfAge = AnimalDeathsOfCrowding = AnimalDeathsOfHunger = 0;
        LastStep = StepStatistics.None;
    }
}
