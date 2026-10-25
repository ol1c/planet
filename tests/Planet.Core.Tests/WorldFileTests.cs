using Planet.Core.IO;
using Planet.Core.Model;
using Planet.Core.Simulation;

namespace Planet.Core.Tests;

public class WorldFileTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsWorldAndRules()
    {
        var world = new World(60, wraps: false) { Generation = 42 };
        WorldGenerator.Randomize(world, 0.4, 0.2, 10, new Random(5));
        world.Generation = 42;
        var rules = RuleSet.Parse("R3/W23/B2/P0/Z012/M4/L10");

        var writer = new StringWriter();
        WorldFile.Save(writer, world, rules);
        var loaded = WorldFile.Load(new StringReader(writer.ToString()));

        Assert.Equal(rules, loaded.Rules);
        Assert.Equal(60, loaded.World.Size);
        Assert.False(loaded.World.Wraps);
        Assert.Equal(42, loaded.World.Generation);
        for (var y = 0; y < 60; y++)
        for (var x = 0; x < 60; x++)
            Assert.Equal(world[x, y], loaded.World[x, y]);
    }

    [Fact]
    public void Save_UsesReadableCellTokens()
    {
        var world = new World(50);
        world[0, 0] = Cell.Empty.WithPlant(true);
        world[1, 0] = Cell.Empty.WithAnimal(3);
        world[2, 0] = new Cell(true, true, 7);

        var writer = new StringWriter();
        WorldFile.Save(writer, world, RuleSet.Default);

        Assert.Contains("RULES R1234/W123456/B23/P01/Z0123/M2/L10", writer.ToString());
        Assert.Contains("\nP A3 PA7 . .", writer.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void SaveAndLoad_KeepsAnimalHistory()
    {
        var world = new World(50);
        world[3, 4] = new Cell(true, true, 5) { Hunger = 2, Satiety = 1 };
        var rules = RuleSet.Parse("R1/W1/B2/P0/Z0/M1/L9/S3/D2/H4");

        var writer = new StringWriter();
        WorldFile.Save(writer, world, rules);
        Assert.Contains(" PA5h2s1 ", writer.ToString());

        var loaded = WorldFile.Load(new StringReader(writer.ToString()));
        var cell = loaded.World[3, 4];
        Assert.Equal((5, (short)2, (short)1), (cell.AnimalAge, cell.Hunger, cell.Satiety));
        Assert.True(cell.AnimalId > 0);
        Assert.Equal(rules, loaded.Rules);
    }

    [Fact]
    public void Load_ReportsLineNumberOfBadCell()
    {
        var writer = new StringWriter();
        WorldFile.Save(writer, new World(50), RuleSet.Default);
        var lines = writer.ToString().ReplaceLineEndings("\n").Split('\n');
        var gridStart = Array.IndexOf(lines, "GRID") + 1;
        lines[gridStart + 2] = "Q" + lines[gridStart + 2][1..];

        var ex = Assert.Throws<WorldFileFormatException>(() => WorldFile.Load(new StringReader(string.Join('\n', lines))));
        Assert.Equal(gridStart + 3, ex.LineNumber);
    }

    [Theory]
    [InlineData("SIZE 50\nGRID\n", "RULES")]
    [InlineData("RULES R1/W1/B2/P0/Z0/M1/L5\nGRID\n", "SIZE")]
    [InlineData("RULES R1/W1/B2/P0/Z0/M1/L5\nSIZE 10\nGRID\n", "50")]
    [InlineData("RULES R1/W1/B2/P0/Z0/M1/L5\nSIZE 50\n", "GRID")]
    public void Load_RejectsBrokenHeader(string text, string expectedInMessage)
    {
        var ex = Assert.Throws<WorldFileFormatException>(() => WorldFile.Load(new StringReader(text)));
        Assert.Contains(expectedInMessage, ex.Message);
    }
}
