using Planet.Core.Model;
using Planet.Core.Simulation;

namespace Planet.App.ViewModels;

public sealed class StatisticsViewModel : ObservableObject
{
    private int _generation;
    private int _plants;
    private int _animals;
    private StepStatistics _lastStep = StepStatistics.None;
    private long _totalPlantBirths;
    private long _totalPlantDeaths;
    private long _totalPlantsEaten;
    private long _totalAnimalBirths;
    private long _totalAnimalDeaths;
    private long _totalAnimalDeathsOfAge;
    private long _totalAnimalDeathsOfCrowding;

    public int Generation { get => _generation; private set => SetProperty(ref _generation, value); }
    public int Plants { get => _plants; private set => SetProperty(ref _plants, value); }
    public int Animals { get => _animals; private set => SetProperty(ref _animals, value); }

    public StepStatistics LastStep { get => _lastStep; private set => SetProperty(ref _lastStep, value); }

    public long TotalPlantBirths { get => _totalPlantBirths; private set => SetProperty(ref _totalPlantBirths, value); }
    public long TotalPlantDeaths { get => _totalPlantDeaths; private set => SetProperty(ref _totalPlantDeaths, value); }
    public long TotalPlantsEaten { get => _totalPlantsEaten; private set => SetProperty(ref _totalPlantsEaten, value); }
    public long TotalAnimalBirths { get => _totalAnimalBirths; private set => SetProperty(ref _totalAnimalBirths, value); }
    public long TotalAnimalDeaths { get => _totalAnimalDeaths; private set => SetProperty(ref _totalAnimalDeaths, value); }
    public long TotalAnimalDeathsOfAge { get => _totalAnimalDeathsOfAge; private set => SetProperty(ref _totalAnimalDeathsOfAge, value); }
    public long TotalAnimalDeathsOfCrowding { get => _totalAnimalDeathsOfCrowding; private set => SetProperty(ref _totalAnimalDeathsOfCrowding, value); }

    public void Update(World world, SimulationStatistics totals)
    {
        Generation = world.Generation;
        (Plants, Animals) = world.CountPopulation();
        LastStep = totals.LastStep;
        TotalPlantBirths = totals.PlantBirths;
        TotalPlantDeaths = totals.PlantDeaths;
        TotalPlantsEaten = totals.PlantsEaten;
        TotalAnimalBirths = totals.AnimalBirths;
        TotalAnimalDeaths = totals.AnimalDeaths;
        TotalAnimalDeathsOfAge = totals.AnimalDeathsOfAge;
        TotalAnimalDeathsOfCrowding = totals.AnimalDeathsOfCrowding;
    }
}
