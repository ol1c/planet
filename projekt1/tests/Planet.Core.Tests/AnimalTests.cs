using Planet.Core.Model;
using Planet.Core.Simulation;

namespace Planet.Core.Tests;

public class AnimalTests
{
    private const int Size = World.MinSize;

    private static RuleSet Rules(string animalBirth = "", string animalBirthPlants = "012345678",
        string animalSurvival = "012345678", int moveRange = 1, int lifespan = 100) =>
        new(CountSet.Empty, CountSet.Range(0, 8), CountSet.Parse(animalBirth), CountSet.Parse(animalBirthPlants),
            CountSet.Parse(animalSurvival), moveRange, lifespan);

    [Fact]
    public void Animal_SurroundedOnlyByPlants_DoesNotDieOfCrowding()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0, 1);
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0)
                world[10 + dx, 10 + dy] = Cell.Empty.WithPlant(true);

        var stats = new Simulator(new Random(1)).Step(world, Rules(animalSurvival: "0"));

        Assert.Equal(0, stats.AnimalDeathsOfCrowding);
        Assert.Equal(1, world.AnimalCount);
    }

    [Fact]
    public void Animal_DiesOfCrowding_OnlyFromAnimalNeighbors()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0, 1);
        world[11, 10] = Cell.Empty.WithAnimal(0, 2);

        var stats = new Simulator(new Random(1)).Step(world, Rules(animalSurvival: "0"));

        Assert.Equal(2, stats.AnimalDeathsOfCrowding);
    }

    [Fact]
    public void Births_AreListedWithUniqueIdsAndPositions()
    {
        var world = new World(Size);
        world.AllocateAnimalId();
        world.AllocateAnimalId();
        world[10, 10] = Cell.Empty.WithAnimal(0, 1);
        world[12, 10] = Cell.Empty.WithAnimal(0, 2);

        var stats = new Simulator(new Random(1)).Step(world, Rules(animalBirth: "2"));

        Assert.NotEmpty(stats.Births);
        Assert.Equal(stats.Births.Count, stats.Births.Select(b => b.Id).Distinct().Count());
        Assert.All(stats.Births, b => Assert.True(b.Id > 2));
        foreach (var birth in stats.Births)
        {
            Assert.True(world.TryFindAnimal(birth.Id, out var x, out var y, out var cell));
            Assert.Equal((birth.X, birth.Y), (x, y));
            Assert.Equal(0, cell.AnimalAge);
        }
    }

    [Fact]
    public void LargeWorld_StepRuns()
    {
        var world = new World(World.MaxSize);
        WorldGenerator.Randomize(world, 0.3, 0.05, 10, new Random(1));
        var before = world.AnimalCount;

        var stats = new Simulator(new Random(1)).Step(world, RuleSet.Default);

        Assert.Equal(before + stats.AnimalBirths - stats.AnimalDeaths, world.AnimalCount);
    }
}
