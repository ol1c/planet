using Planet.Core.Model;

namespace Planet.Core.Simulation;

public sealed class Simulator
{
    private static readonly (int Dx, int Dy)[] Directions =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    ];

    private readonly Random _random;

    private Cell[] _next = [];
    private byte[] _plantFlags = [], _animalFlags = [], _matureFlags = [];
    private byte[] _plantNeighbors = [], _animalNeighbors = [], _matureNeighbors = [];
    private bool[] _processed = [];

    public Simulator(Random? random = null) => _random = random ?? new Random();

    public StepStatistics Step(World world, RuleSet rules)
    {
        var n = world.Size;
        var length = n * n;
        EnsureBuffers(length);

        var current = world.Cells;
        var next = _next;
        Array.Clear(next);

        for (var i = 0; i < length; i++)
        {
            var c = current[i];
            _plantFlags[i] = c.HasPlant ? (byte)1 : (byte)0;
            _animalFlags[i] = c.HasAnimal ? (byte)1 : (byte)0;
            _matureFlags[i] = c.HasAnimal && c.AnimalAge >= rules.MaturityAge ? (byte)1 : (byte)0;
        }
        CountNeighbors(_plantFlags, _plantNeighbors, n, world.Wraps);
        CountNeighbors(_animalFlags, _animalNeighbors, n, world.Wraps);
        var parents = rules.MaturityAge > 0 ? _matureNeighbors : _animalNeighbors;
        if (rules.MaturityAge > 0)
            CountNeighbors(_matureFlags, _matureNeighbors, n, world.Wraps);

        int plantBirths = 0, plantDeaths = 0, plantsEaten = 0;
        Parallel.For(0, n, () => (Births: 0, Deaths: 0, Eaten: 0), (y, _, acc) =>
        {
            for (int i = y * n, end = i + n; i < end; i++)
            {
                var c = current[i];
                if (c.HasPlant)
                {
                    if (c.HasAnimal)
                        acc.Eaten++;
                    else if (rules.PlantSurvival.Contains(_plantNeighbors[i]))
                        next[i] = new Cell(true, false);
                    else
                        acc.Deaths++;
                }
                else if (!c.HasAnimal && rules.PlantBirth.Contains(_plantNeighbors[i]))
                {
                    next[i] = new Cell(true, false);
                    acc.Births++;
                }
            }
            return acc;
        }, acc =>
        {
            Interlocked.Add(ref plantBirths, acc.Births);
            Interlocked.Add(ref plantDeaths, acc.Deaths);
            Interlocked.Add(ref plantsEaten, acc.Eaten);
        });

        var births = new List<AnimalBirth>();
        for (var i = 0; i < length; i++)
        {
            if (current[i].HasAnimal)
                continue;
            if (rules.AnimalBirth.Contains(parents[i]) && rules.AnimalBirthPlants.Contains(_plantNeighbors[i]))
            {
                var id = world.AllocateAnimalId();
                next[i] = next[i].WithAnimal(0, id);
                births.Add(new AnimalBirth(id, i % n, i / n));
            }
        }

        var animals = new List<int>();
        for (var i = 0; i < length; i++)
            if (current[i].HasAnimal)
                animals.Add(i);
        Shuffle(animals);

        var processed = _processed;
        Array.Clear(processed);
        bool IsOccupied(int i) => next[i].HasAnimal || (current[i].HasAnimal && !processed[i]);

        int deathsOfAge = 0, deathsOfCrowding = 0, deathsOfHunger = 0, moves = 0;
        foreach (var index in animals)
        {
            processed[index] = true;
            var cell = current[index];

            var age = cell.AnimalAge + 1;
            if (age >= rules.Lifespan)
            {
                deathsOfAge++;
                continue;
            }

            if (!rules.AnimalSurvival.Contains(_animalNeighbors[index]))
            {
                deathsOfCrowding++;
                continue;
            }

            var eats = cell.HasPlant;
            var hunger = eats ? 0 : cell.Hunger + 1;
            if (!eats && rules.StarvationTime > 0 && hunger >= rules.StarvationTime)
            {
                deathsOfHunger++;
                continue;
            }

            int target;
            if (eats)
                target = index;
            else if (cell.Satiety > 0)
                target = SeekCompany(world, current, index, rules.MoveRange, IsOccupied);
            else
                target = SeekFood(world, current, index, rules.MoveRange, IsOccupied);

            if (target != index)
                moves++;

            var satiety = eats ? rules.CompanyTime : Math.Max(0, cell.Satiety - 1);
            next[target] = next[target] with
            {
                HasAnimal = true,
                AnimalAge = age,
                AnimalId = cell.AnimalId,
                Hunger = (short)Math.Min(hunger, short.MaxValue),
                Satiety = (short)satiety,
            };
        }

        _next = world.SwapCells(next);
        world.Generation++;

        return new StepStatistics
        {
            PlantBirths = plantBirths,
            PlantDeaths = plantDeaths,
            PlantsEaten = plantsEaten,
            Births = births,
            AnimalDeathsOfAge = deathsOfAge,
            AnimalDeathsOfCrowding = deathsOfCrowding,
            AnimalDeathsOfHunger = deathsOfHunger,
            AnimalMoves = moves,
        };
    }

    private int SeekFood(World world, Cell[] current, int index, int moveRange, Func<int, bool> isOccupied)
    {
        var candidates = new List<int>(8);
        for (var distance = 1; distance <= moveRange; distance++)
        {
            foreach (var (dx, dy) in Directions)
            {
                var i = Offset(world, index, dx * distance, dy * distance);
                if (i >= 0 && current[i].HasPlant && !isOccupied(i))
                    candidates.Add(i);
            }
            if (candidates.Count > 0)
                return candidates[_random.Next(candidates.Count)];
        }
        return RandomStep(world, index, isOccupied, candidates);
    }

    private int SeekCompany(World world, Cell[] current, int index, int moveRange, Func<int, bool> isOccupied)
    {
        var candidates = new List<int>(8);
        for (var distance = 1; distance <= moveRange; distance++)
        {
            foreach (var (dx, dy) in Directions)
            {
                var i = Offset(world, index, dx * distance, dy * distance);
                if (i < 0 || i == index || !current[i].HasAnimal)
                    continue;
                if (distance == 1)
                    return index;
                var before = Offset(world, index, dx * (distance - 1), dy * (distance - 1));
                if (before >= 0 && !isOccupied(before))
                    candidates.Add(before);
            }
            if (candidates.Count > 0)
                return candidates[_random.Next(candidates.Count)];
        }
        return RandomStep(world, index, isOccupied, candidates);
    }

    private int RandomStep(World world, int index, Func<int, bool> isOccupied, List<int> candidates)
    {
        candidates.Clear();
        foreach (var (dx, dy) in Directions)
        {
            var i = Offset(world, index, dx, dy);
            if (i >= 0 && !isOccupied(i))
                candidates.Add(i);
        }
        return candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : index;
    }

    private static int Offset(World world, int index, int dx, int dy)
    {
        int x = index % world.Size + dx, y = index / world.Size + dy;
        return world.TryNormalize(ref x, ref y) ? y * world.Size + x : -1;
    }

    private static void CountNeighbors(byte[] flags, byte[] result, int n, bool wraps)
    {
        Parallel.For(0, n, y =>
        {
            for (var x = 0; x < n; x++)
            {
                var sum = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    var ny = y + dy;
                    if (ny < 0 || ny >= n)
                    {
                        if (!wraps) continue;
                        ny = (ny + n) % n;
                    }
                    var row = ny * n;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var nx = x + dx;
                        if (nx < 0 || nx >= n)
                        {
                            if (!wraps) continue;
                            nx = (nx + n) % n;
                        }
                        sum += flags[row + nx];
                    }
                }
                result[y * n + x] = (byte)sum;
            }
        });
    }

    private void EnsureBuffers(int length)
    {
        if (_next.Length == length)
            return;
        _next = new Cell[length];
        _plantFlags = new byte[length];
        _animalFlags = new byte[length];
        _matureFlags = new byte[length];
        _plantNeighbors = new byte[length];
        _animalNeighbors = new byte[length];
        _matureNeighbors = new byte[length];
        _processed = new bool[length];
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
