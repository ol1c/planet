namespace Planet.Core.Model;

public sealed class World
{
    public const int MinSize = 50;

    public const int MaxSize = 1000;

    private Cell[] _cells;
    private int _nextAnimalId = 1;

    public World(int size, bool wraps = true)
    {
        if (size is < MinSize or > MaxSize)
            throw new ArgumentOutOfRangeException(nameof(size), size, $"Rozmiar planszy musi być z zakresu {MinSize}–{MaxSize}.");
        Size = size;
        Wraps = wraps;
        _cells = new Cell[size * size];
    }

    public int Size { get; }

    public bool Wraps { get; set; }

    public int Generation { get; set; }

    public Cell this[int x, int y]
    {
        get => _cells[Index(x, y)];
        set => _cells[Index(x, y)] = value;
    }

    internal Cell[] Cells => _cells;

    internal Cell[] SwapCells(Cell[] next)
    {
        var previous = _cells;
        _cells = next;
        return previous;
    }

    public int AllocateAnimalId() => _nextAnimalId++;

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

    public bool TryNormalize(ref int x, ref int y)
    {
        if (Wraps)
        {
            x = ((x % Size) + Size) % Size;
            y = ((y % Size) + Size) % Size;
            return true;
        }
        return Contains(x, y);
    }

    public int CountPlantNeighbors(int x, int y) => CountNeighbors(x, y, static c => c.HasPlant);

    public int CountAnimalNeighbors(int x, int y) => CountNeighbors(x, y, static c => c.HasAnimal);

    private int CountNeighbors(int x, int y, Func<Cell, bool> predicate)
    {
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0)
                continue;
            int nx = x + dx, ny = y + dy;
            if (TryNormalize(ref nx, ref ny) && predicate(this[nx, ny]))
                count++;
        }
        return count;
    }

    public int PlantCount => CountPopulation().Plants;

    public int AnimalCount => CountPopulation().Animals;

    public (int Plants, int Animals) CountPopulation()
    {
        int plants = 0, animals = 0;
        foreach (var cell in _cells)
        {
            if (cell.HasPlant) plants++;
            if (cell.HasAnimal) animals++;
        }
        return (plants, animals);
    }

    public bool TryFindAnimal(int id, out int x, out int y, out Cell cell)
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i].HasAnimal && _cells[i].AnimalId == id)
            {
                (x, y, cell) = (i % Size, i / Size, _cells[i]);
                return true;
            }
        }
        (x, y, cell) = (-1, -1, Cell.Empty);
        return false;
    }

    public void Clear()
    {
        Array.Clear(_cells);
        Generation = 0;
    }

    public World Clone()
    {
        var copy = new World(Size, Wraps) { Generation = Generation, _nextAnimalId = _nextAnimalId };
        _cells.CopyTo(copy._cells, 0);
        return copy;
    }

    private int Index(int x, int y)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException($"({x}, {y})", "Współrzędne poza planszą.");
        return y * Size + x;
    }
}
