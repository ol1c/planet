using Planet.Core.IO;
using Planet.Core.Model;

namespace Planet.Core.Tests;

public class ImageExportTests
{
    [Fact]
    public void Rasterize_ScalesCellsToBlocks()
    {
        var world = new World(World.MinSize);
        world[0, 0] = Cell.Empty.WithPlant(true);
        world[1, 0] = Cell.Empty.WithAnimal(0);

        var pixels = WorldRasterizer.Rasterize(world, 0, 0, 2, 3);

        Assert.Equal(36, pixels.Length);
        Assert.Equal(WorldRasterizer.PlantIndex, pixels[2 * 6 + 2]);
        Assert.Equal(WorldRasterizer.AnimalIndex, pixels[2 * 6 + 3]);
        Assert.Equal(WorldRasterizer.EmptyIndex, pixels[5 * 6 + 0]);
    }

    [Fact]
    public void Snapshot_MarksOutsideCells_WhenNotWrapping()
    {
        var world = new World(World.MinSize, wraps: false);
        var snapshot = WorldRasterizer.Snapshot(world, 0, 0, 2);

        Assert.Null(snapshot[0, 0]);
        Assert.NotNull(snapshot[2, 2]);
        var pixels = WorldRasterizer.Rasterize(snapshot, 1, highlightCenter: false);
        Assert.Equal(WorldRasterizer.OutsideIndex, pixels[0]);
    }
}
