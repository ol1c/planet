using System.Text;

namespace Planet.Core.Model;

public readonly struct CountSet : IEquatable<CountSet>
{
    public const int MaxCount = 8;

    private readonly ushort _mask;

    private CountSet(ushort mask) => _mask = mask;

    public static CountSet Empty => default;

    public bool IsEmpty => _mask == 0;

    public static CountSet Of(params int[] counts)
    {
        ushort mask = 0;
        foreach (var count in counts)
        {
            if (count is < 0 or > MaxCount)
                throw new ArgumentOutOfRangeException(nameof(counts), count, $"Liczba sąsiadów musi być z zakresu 0-{MaxCount}.");
            mask |= (ushort)(1 << count);
        }
        return new CountSet(mask);
    }

    public static CountSet Range(int from, int to) =>
        Of(Enumerable.Range(from, to - from + 1).ToArray());

    public bool Contains(int count) => count is >= 0 and <= MaxCount && (_mask & (1 << count)) != 0;

    public IEnumerable<int> Values
    {
        get
        {
            for (var i = 0; i <= MaxCount; i++)
                if (Contains(i))
                    yield return i;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var v in Values)
            sb.Append((char)('0' + v));
        return sb.ToString();
    }

    public static CountSet Parse(string? text) =>
        TryParse(text, out var set, out var error) ? set : throw new FormatException(error);

    public static bool TryParse(string? text, out CountSet set, out string? error)
    {
        set = Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        ushort mask = 0;
        foreach (var ch in text)
        {
            if (ch is ',' or ';' or ' ')
                continue;
            if (ch is < '0' or > '8')
            {
                error = $"Niedozwolony znak '{ch}' - dozwolone są cyfry 0-{MaxCount}.";
                return false;
            }
            mask |= (ushort)(1 << (ch - '0'));
        }
        set = new CountSet(mask);
        return true;
    }

    public bool Equals(CountSet other) => _mask == other._mask;
    public override bool Equals(object? obj) => obj is CountSet other && Equals(other);
    public override int GetHashCode() => _mask;
    public static bool operator ==(CountSet left, CountSet right) => left.Equals(right);
    public static bool operator !=(CountSet left, CountSet right) => !left.Equals(right);
}
