namespace Planet.Core.Model;

public sealed class RuleSet : IEquatable<RuleSet>
{
    public const int MinMoveRange = 1;
    public const int MaxMoveRange = 20;
    public const int MinLifespan = 1;
    public const int MaxLifespan = 10_000;
    public const int MaxHistoryValue = 1_000;

    public static RuleSet Default { get; } = new(
        plantBirth: CountSet.Of(1, 2, 3, 4),
        plantSurvival: CountSet.Range(1, 6),
        animalBirth: CountSet.Of(2, 3),
        animalBirthPlants: CountSet.Of(0, 1),
        animalSurvival: CountSet.Of(0, 1, 2, 3),
        moveRange: 2,
        lifespan: 10);

    public RuleSet(CountSet plantBirth, CountSet plantSurvival, CountSet animalBirth,
        CountSet animalBirthPlants, CountSet animalSurvival, int moveRange, int lifespan,
        int companyTime = 0, int maturityAge = 0, int starvationTime = 0)
    {
        if (moveRange is < MinMoveRange or > MaxMoveRange)
            throw new ArgumentOutOfRangeException(nameof(moveRange), moveRange, $"M musi być z zakresu {MinMoveRange}–{MaxMoveRange}.");
        if (lifespan is < MinLifespan or > MaxLifespan)
            throw new ArgumentOutOfRangeException(nameof(lifespan), lifespan, $"L musi być z zakresu {MinLifespan}–{MaxLifespan}.");
        if (companyTime is < 0 or > MaxHistoryValue)
            throw new ArgumentOutOfRangeException(nameof(companyTime), companyTime, $"S musi być z zakresu 0–{MaxHistoryValue}.");
        if (maturityAge is < 0 or > MaxHistoryValue)
            throw new ArgumentOutOfRangeException(nameof(maturityAge), maturityAge, $"D musi być z zakresu 0–{MaxHistoryValue}.");
        if (starvationTime is < 0 or > MaxHistoryValue)
            throw new ArgumentOutOfRangeException(nameof(starvationTime), starvationTime, $"H musi być z zakresu 0–{MaxHistoryValue}.");

        PlantBirth = plantBirth;
        PlantSurvival = plantSurvival;
        AnimalBirth = animalBirth;
        AnimalBirthPlants = animalBirthPlants;
        AnimalSurvival = animalSurvival;
        MoveRange = moveRange;
        Lifespan = lifespan;
        CompanyTime = companyTime;
        MaturityAge = maturityAge;
        StarvationTime = starvationTime;
    }

    public CountSet PlantBirth { get; }

    public CountSet PlantSurvival { get; }

    public CountSet AnimalBirth { get; }

    public CountSet AnimalBirthPlants { get; }

    public CountSet AnimalSurvival { get; }

    public int MoveRange { get; }

    public int Lifespan { get; }

    public int CompanyTime { get; }

    public int MaturityAge { get; }

    public int StarvationTime { get; }

    public bool HasHistory => CompanyTime > 0 || MaturityAge > 0 || StarvationTime > 0;

    public override string ToString()
    {
        var text = $"R{PlantBirth}/W{PlantSurvival}/B{AnimalBirth}/P{AnimalBirthPlants}/Z{AnimalSurvival}/M{MoveRange}/L{Lifespan}";
        if (CompanyTime > 0) text += $"/S{CompanyTime}";
        if (MaturityAge > 0) text += $"/D{MaturityAge}";
        if (StarvationTime > 0) text += $"/H{StarvationTime}";
        return text;
    }

    public static RuleSet Parse(string text) =>
        TryParse(text, out var rules, out var error) ? rules! : throw new FormatException(error);

    public static bool TryParse(string? text, out RuleSet? rules, out string? error)
    {
        rules = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Pusty zapis reguł.";
            return false;
        }

        var sets = new Dictionary<char, CountSet>();
        var numbers = new Dictionary<char, int>();

        foreach (var raw in text.Trim().Split('/'))
        {
            var segment = raw.Trim();
            if (segment.Length == 0)
            {
                error = "Pusty segment w zapisie reguł.";
                return false;
            }

            var key = char.ToUpperInvariant(segment[0]);
            var value = segment[1..];
            switch (key)
            {
                case 'R' or 'W' or 'B' or 'P' or 'Z':
                    if (!CountSet.TryParse(value, out var set, out var setError))
                    {
                        error = $"{key}: {setError}";
                        return false;
                    }
                    sets[key] = set;
                    break;
                case 'M' or 'L' or 'S' or 'D' or 'H':
                    if (!int.TryParse(value, out var number))
                    {
                        error = $"{key}: oczekiwano liczby całkowitej, otrzymano \"{value}\".";
                        return false;
                    }
                    numbers[key] = number;
                    break;
                default:
                    error = $"Nieznany parametr '{segment[0]}'.";
                    return false;
            }
        }

        var missing = "RWBPZ".Where(k => !sets.ContainsKey(k))
            .Concat("ML".Where(k => !numbers.ContainsKey(k)))
            .Select(k => k.ToString()).ToList();
        if (missing.Count > 0)
        {
            error = $"Brak parametrów: {string.Join(", ", missing)}.";
            return false;
        }

        try
        {
            rules = new RuleSet(sets['R'], sets['W'], sets['B'], sets['P'], sets['Z'], numbers['M'], numbers['L'],
                numbers.GetValueOrDefault('S'), numbers.GetValueOrDefault('D'), numbers.GetValueOrDefault('H'));
            return true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool Equals(RuleSet? other) =>
        other is not null && ToString() == other.ToString();

    public override bool Equals(object? obj) => Equals(obj as RuleSet);
    public override int GetHashCode() => ToString().GetHashCode();
}
