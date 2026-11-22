using Planet.Core.Model;

namespace Planet.Core.Simulation;

public static class WorldGenerator
{
    public static void Randomize(World world, double plantDensity, double animalDensity, int lifespan, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(plantDensity, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(plantDensity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(animalDensity, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(animalDensity, 1);
        random ??= new Random();

        world.Clear();
        for (var y = 0; y < world.Size; y++)
        for (var x = 0; x < world.Size; x++)
        {
            var cell = Cell.Empty.WithPlant(random.NextDouble() < plantDensity);
            if (random.NextDouble() < animalDensity)
                cell = cell.WithAnimal(0, world.AllocateAnimalId());
            world[x, y] = cell;
        }
    }
}
