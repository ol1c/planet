using Planet.Core.Model;
using Planet.Core.Simulation;

namespace Planet.Core.Tests;

public class SimulatorTests
{
    private const int Size = World.MinSize;

    private static RuleSet Rules(string plantBirth = "", string plantSurvival = "", string animalBirth = "",
        string animalBirthPlants = "", string animalSurvival = "012345678", int moveRange = 1, int lifespan = 100) =>
        new(CountSet.Parse(plantBirth), CountSet.Parse(plantSurvival), CountSet.Parse(animalBirth),
            CountSet.Parse(animalBirthPlants), CountSet.Parse(animalSurvival), moveRange, lifespan);

    private static StepStatistics Step(World world, RuleSet rules, int seed = 1) =>
        new Simulator(new Random(seed)).Step(world, rules);

    [Fact]
    public void Plant_IsBornOnEmptyCell_WhenNeighborCountInR()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithPlant(true);

        var stats = Step(world, Rules(plantBirth: "1"));

        Assert.Equal(8, stats.PlantBirths);
        Assert.True(world[11, 11].HasPlant);
        Assert.False(world[10, 10].HasPlant);
        Assert.Equal(1, stats.PlantDeaths);
    }

    [Fact]
    public void Plant_IsNotBornUnderAnimal()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithPlant(true);
        world[11, 10] = Cell.Empty.WithAnimal(0);

        Step(world, Rules(plantBirth: "1", animalSurvival: ""));

        Assert.False(world[11, 10].HasPlant);
    }

    [Fact]
    public void Plant_SurvivesWhenNeighborCountInW()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithPlant(true);
        world[11, 10] = Cell.Empty.WithPlant(true);

        Step(world, Rules(plantSurvival: "1"));

        Assert.True(world[10, 10].HasPlant);
        Assert.True(world[11, 10].HasPlant);
    }

    [Fact]
    public void Animal_EatsPlantItStandsOn_AndStays()
    {
        var world = new World(Size);
        world[10, 10] = new Cell(HasPlant: true, HasAnimal: true, AnimalAge: 3);

        var stats = Step(world, Rules(plantSurvival: "012345678"));

        Assert.Equal(1, stats.PlantsEaten);
        Assert.Equal(new Cell(false, true, 4), world[10, 10]);
    }

    [Fact]
    public void Animal_DiesOfOldAge()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(4);

        var stats = Step(world, Rules(lifespan: 5));

        Assert.Equal(1, stats.AnimalDeathsOfAge);
        Assert.Equal(0, world.AnimalCount);
    }

    [Fact]
    public void Animal_LivesExactlyLSteps()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0);
        var rules = Rules(lifespan: 3);
        var simulator = new Simulator(new Random(1));

        simulator.Step(world, rules);
        simulator.Step(world, rules);
        Assert.Equal(1, world.AnimalCount);
        simulator.Step(world, rules);
        Assert.Equal(0, world.AnimalCount);
    }

    [Fact]
    public void Animal_DiesOfCrowding_WhenNeighborCountNotInZ()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0);
        foreach (var (x, y) in new[] { (9, 9), (10, 9), (11, 9), (9, 10) })
            world[x, y] = Cell.Empty.WithAnimal(0);

        var stats = Step(world, Rules(animalSurvival: "0123"));

        Assert.True(stats.AnimalDeathsOfCrowding >= 1);
        Assert.Equal(5 - stats.AnimalDeathsOfCrowding, world.AnimalCount);
    }

    [Fact]
    public void HungryAnimal_MovesToAdjacentPlant()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0);
        world[11, 11] = Cell.Empty.WithPlant(true);

        var stats = Step(world, Rules(plantSurvival: "012345678"));

        Assert.Equal(1, stats.AnimalMoves);
        Assert.True(world[11, 11].HasAnimal);
        Assert.True(world[11, 11].HasPlant);
        Assert.False(world[10, 10].HasAnimal);
    }

    [Fact]
    public void HungryAnimal_JumpsUpToMCellsTowardsPlant()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0);
        world[13, 10] = Cell.Empty.WithPlant(true);

        Step(world, Rules(plantSurvival: "012345678", moveRange: 3));

        Assert.True(world[13, 10].HasAnimal);
    }

    [Fact]
    public void HungryAnimal_DoesNotSeePlantBeyondM()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0);
        world[14, 10] = Cell.Empty.WithPlant(true);

        Step(world, Rules(plantSurvival: "012345678", moveRange: 3));

        Assert.False(world[14, 10].HasAnimal);
        Assert.Equal(1, world.AnimalCount);
    }

    [Fact]
    public void Animals_NeverShareACell()
    {
        var world = new World(Size);
        WorldGenerator.Randomize(world, 0.3, 0.4, 10, new Random(7));
        var rules = Rules(plantBirth: "1234", plantSurvival: "123456", moveRange: 3, lifespan: 10);
        var simulator = new Simulator(new Random(3));

        for (var i = 0; i < 30; i++)
        {
            var before = world.AnimalCount;
            var stats = simulator.Step(world, rules);
            Assert.Equal(before + stats.AnimalBirths - stats.AnimalDeaths, world.AnimalCount);
        }
    }

    [Fact]
    public void Animal_IsBorn_WhenAnimalAndPlantNeighborsMatchBAndP()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0);
        world[12, 10] = Cell.Empty.WithAnimal(0);

        var stats = Step(world, Rules(animalBirth: "2", animalBirthPlants: "0"));

        Assert.True(stats.AnimalBirths >= 1);
        Assert.True(world.AnimalCount >= 3);
    }

    [Fact]
    public void Animal_IsNotBorn_WhenPlantCountNotInP()
    {
        var world = new World(Size);
        world[10, 10] = Cell.Empty.WithAnimal(0);
        world[12, 10] = Cell.Empty.WithAnimal(0);
        world[11, 9] = Cell.Empty.WithPlant(true);
        world[11, 11] = Cell.Empty.WithPlant(true);
        world[10, 9] = Cell.Empty.WithPlant(true);

        Step(world, Rules(animalBirth: "2", animalBirthPlants: "01", plantSurvival: "012345678"));

        Assert.False(world[11, 10].HasAnimal && world[11, 10].AnimalAge == 0);
    }

    [Fact]
    public void Neighbors_WrapAroundEdges_OnlyWhenEnabled()
    {
        var wrapped = new World(Size, wraps: true);
        wrapped[0, 0] = Cell.Empty.WithPlant(true);
        Assert.Equal(1, wrapped.CountPlantNeighbors(Size - 1, Size - 1));

        var bounded = new World(Size, wraps: false);
        bounded[0, 0] = Cell.Empty.WithPlant(true);
        Assert.Equal(0, bounded.CountPlantNeighbors(Size - 1, Size - 1));
    }

    [Fact]
    public void Step_IncrementsGeneration()
    {
        var world = new World(Size);
        Step(world, RuleSet.Default);
        Assert.Equal(1, world.Generation);
    }
}
