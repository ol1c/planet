using Planet.Core.Model;
using Planet.Core.Simulation;

namespace Planet.Core.Tests;

public class HistoryTests
{
    private const int Size = World.MinSize;

    private static RuleSet Rules(string animalBirth = "", string animalBirthPlants = "012345678",
        string animalSurvival = "012345678", int moveRange = 1, int lifespan = 100,
        int companyTime = 0, int maturityAge = 0, int starvationTime = 0) =>
        new(CountSet.Empty, CountSet.Range(0, 8), CountSet.Parse(animalBirth), CountSet.Parse(animalBirthPlants),
            CountSet.Parse(animalSurvival), moveRange, lifespan, companyTime, maturityAge, starvationTime);

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
    public void Starvation_KillsAnimalAfterHStepsWithoutFood()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0, 1);
        var rules = Rules(starvationTime: 3);
        var simulator = new Simulator(new Random(1));

        simulator.Step(world, rules);
        simulator.Step(world, rules);
        Assert.Equal(1, world.AnimalCount);
        var stats = simulator.Step(world, rules);

        Assert.Equal(1, stats.AnimalDeathsOfHunger);
        Assert.Equal(0, world.AnimalCount);
    }

    [Fact]
    public void Eating_ResetsHunger_AndStartsCompanySeeking()
    {
        var world = new World(Size);
        world[10, 10] = new Cell(true, true, 2) { AnimalId = 1, Hunger = 5 };

        new Simulator(new Random(1)).Step(world, Rules(companyTime: 2));

        Assert.True(world.TryFindAnimal(1, out _, out _, out var cell));
        Assert.Equal(0, cell.Hunger);
        Assert.Equal(2, cell.Satiety);
    }

    [Fact]
    public void SatedAnimal_MovesTowardsOtherAnimal_InsteadOfFood()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0, 1) with { Satiety = 2 };
        world[13, 10] = Cell.Empty.WithAnimal(0, 2) with { Satiety = 2 };
        world[10, 11] = Cell.Empty.WithPlant(true);

        new Simulator(new Random(1)).Step(world, Rules(moveRange: 3, animalSurvival: "012345678"));

        Assert.True(world.TryFindAnimal(1, out var x, out var y, out var cell));
        Assert.True(Math.Abs(x - 13) <= 2 && y == 10, $"Zwierzę powinno zbliżyć się do drugiego, jest w ({x},{y}).");
        Assert.Equal(1, cell.Satiety);
    }

    [Fact]
    public void HungryAnimal_StillGoesForFood()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0, 1);
        world[13, 10] = Cell.Empty.WithAnimal(0, 2);
        world[10, 11] = Cell.Empty.WithPlant(true);

        new Simulator(new Random(1)).Step(world, Rules(moveRange: 3));

        Assert.True(world[10, 11].HasAnimal);
    }

    [Fact]
    public void YoungAnimals_DoNotCountAsParents_BeforeMaturityAge()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(1, 1);
        world[12, 10] = Cell.Empty.WithAnimal(1, 2);

        var stats = new Simulator(new Random(1)).Step(world, Rules(animalBirth: "2", maturityAge: 3));
        Assert.Equal(0, stats.AnimalBirths);

        var adults = new World(Size);
        adults[10, 10] = Cell.Empty.WithAnimal(3, 1);
        adults[12, 10] = Cell.Empty.WithAnimal(3, 2);
        stats = new Simulator(new Random(1)).Step(adults, Rules(animalBirth: "2", maturityAge: 3));
        Assert.True(stats.AnimalBirths > 0);
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
    public void RuleSet_HistoryParameters_AreOptionalInText()
    {
        var rules = RuleSet.Parse("R1234/W123456/B23/P01/Z0123/M2/L10/S2/D3/H8");
        Assert.Equal((2, 3, 8), (rules.CompanyTime, rules.MaturityAge, rules.StarvationTime));
        Assert.Equal("R1234/W123456/B23/P01/Z0123/M2/L10/S2/D3/H8", rules.ToString());
        Assert.Equal(0, RuleSet.Default.CompanyTime);
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
