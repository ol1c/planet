using Planet.Core.Model;

namespace Planet.Core.IO;

public static class WorldRasterizer
{
    public const byte EmptyIndex = 0;
    public const byte PlantIndex = 1;
    public const byte AnimalIndex = 2;
    public const byte AnimalOnPlantIndex = 3;

    public const byte OutsideIndex = 4;

    public const byte HighlightIndex = 5;

    public const int PaletteSize = 6;

    public static byte IndexOf(Cell cell) => cell switch
    {
        { HasAnimal: true, HasPlant: true } => AnimalOnPlantIndex,
        { HasAnimal: true } => AnimalIndex,
        { HasPlant: true } => PlantIndex,
        _ => EmptyIndex,
    };

    public static byte[] Rasterize(World world, int x0, int y0, int size, int scale) =>
        Rasterize((x, y) =>
        {
            int wx = x0 + x, wy = y0 + y;
            return world.TryNormalize(ref wx, ref wy) ? IndexOf(world[wx, wy]) : OutsideIndex;
        }, size, size, scale);

    public static Cell?[,] Snapshot(World world, int centerX, int centerY, int radius)
    {
        var side = radius * 2 + 1;
        var cells = new Cell?[side, side];
        for (var dy = 0; dy < side; dy++)
        for (var dx = 0; dx < side; dx++)
        {
            int x = centerX - radius + dx, y = centerY - radius + dy;
            cells[dx, dy] = world.TryNormalize(ref x, ref y) ? world[x, y] : null;
        }
        return cells;
    }

    public static byte[] Rasterize(Cell?[,] snapshot, int scale, bool highlightCenter)
    {
        int side = snapshot.GetLength(0), center = side / 2;
        var pixels = Rasterize((x, y) => snapshot[x, y] is { } c ? IndexOf(c) : OutsideIndex, side, side, scale);
        if (highlightCenter && scale >= 3)
        {
            var width = side * scale;
            int left = center * scale, top = center * scale, right = left + scale - 1, bottom = top + scale - 1;
            for (var i = left; i <= right; i++)
            {
                pixels[top * width + i] = HighlightIndex;
                pixels[bottom * width + i] = HighlightIndex;
            }
            for (var j = top; j <= bottom; j++)
            {
                pixels[j * width + left] = HighlightIndex;
                pixels[j * width + right] = HighlightIndex;
            }
        }
        return pixels;
    }

    private static byte[] Rasterize(Func<int, int, byte> indexAt, int columns, int rows, int scale)
    {
        if (scale < 1)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Skala musi być dodatnia.");
        var width = columns * scale;
        var pixels = new byte[width * rows * scale];
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var index = indexAt(x, y);
                var start = y * scale * width + x * scale;
                pixels.AsSpan(start, scale).Fill(index);
            }
            var rowStart = y * scale * width;
            for (var r = 1; r < scale; r++)
                pixels.AsSpan(rowStart, width).CopyTo(pixels.AsSpan(rowStart + r * width, width));
        }
        return pixels;
    }
}
