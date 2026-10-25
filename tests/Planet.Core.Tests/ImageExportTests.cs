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

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 7)]
    [InlineData(3, 20000)]
    public void Lzw_RoundTrips(int minCodeSize, int length)
    {
        var random = new Random(length);
        var data = new byte[length];
        for (var i = 0; i < length; i++)
            data[i] = (byte)(random.Next(4) == 0 ? random.Next(1 << minCodeSize) : 0);

        var compressed = GifWriter.Lzw(data, minCodeSize);

        Assert.Equal(data, LzwDecode(compressed, minCodeSize, length));
    }

    [Fact]
    public void GifWriter_WritesValidContainer()
    {
        var stream = new MemoryStream();
        using (var gif = new GifWriter(stream, 4, 2, [(255, 0, 0), (0, 255, 0), (0, 0, 255)], 10, leaveOpen: true))
        {
            gif.AddFrame(new byte[8]);
            gif.AddFrame([0, 1, 2, 0, 1, 2, 0, 1]);
            Assert.Equal(2, gif.FrameCount);
        }

        var bytes = stream.ToArray();
        Assert.Equal("GIF89a"u8.ToArray(), bytes[..6]);
        Assert.Equal(4, bytes[6] | bytes[7] << 8);
        Assert.Equal(0x3B, bytes[^1]);
    }

    private static byte[] LzwDecode(byte[] data, int minCodeSize, int expectedLength)
    {
        int clear = 1 << minCodeSize, end = clear + 1;
        var table = new List<byte[]>();
        void Reset()
        {
            table.Clear();
            for (var i = 0; i < clear; i++) table.Add([(byte)i]);
            table.Add([]);
            table.Add([]);
        }

        var output = new List<byte>();
        int codeSize = minCodeSize + 1, bitPos = 0;
        byte[]? previous = null;
        Reset();

        while (true)
        {
            var code = 0;
            for (var b = 0; b < codeSize; b++, bitPos++)
                if ((data[bitPos >> 3] >> (bitPos & 7) & 1) != 0)
                    code |= 1 << b;

            if (code == clear) { Reset(); codeSize = minCodeSize + 1; previous = null; continue; }
            if (code == end) break;

            byte[] entry;
            if (code < table.Count) entry = table[code];
            else entry = [.. previous!, previous![0]];

            output.AddRange(entry);
            if (previous is not null && table.Count < 4096)
                table.Add([.. previous, entry[0]]);
            if (table.Count == 1 << codeSize && codeSize < 12)
                codeSize++;
            previous = entry;
        }

        Assert.Equal(expectedLength, output.Count);
        return [.. output];
    }
}
