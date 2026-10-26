using System.Globalization;
using Planet.Core.Model;

namespace Planet.Core.IO;

public sealed record WorldDocument(World World, RuleSet Rules);

public sealed class WorldFileFormatException(string message, int lineNumber)
    : FormatException($"Linia {lineNumber}: {message}")
{
    public int LineNumber { get; } = lineNumber;
}

public static class WorldFile
{
    public const string FileExtension = ".txt";

    public static void Save(string path, World world, RuleSet rules)
    {
        using var writer = new StreamWriter(path);
        Save(writer, world, rules);
    }

    public static void Save(TextWriter writer, World world, RuleSet rules)
    {
        writer.WriteLine("# Planet - zapis ekosystemu");
        writer.WriteLine("# Pola: . puste, P roślina, A<wiek> zwierzę, PA<wiek> zwierzę na roślinie");
        writer.WriteLine($"RULES {rules}");
        writer.WriteLine($"SIZE {world.Size}");
        writer.WriteLine($"WRAP {(world.Wraps ? 1 : 0)}");
        writer.WriteLine($"GENERATION {world.Generation}");
        writer.WriteLine("GRID");

        var tokens = new string[world.Size];
        for (var y = 0; y < world.Size; y++)
        {
            for (var x = 0; x < world.Size; x++)
                tokens[x] = FormatCell(world[x, y]);
            writer.WriteLine(string.Join(' ', tokens));
        }
    }

    public static WorldDocument Load(string path)
    {
        using var reader = new StreamReader(path);
        return Load(reader);
    }

    public static WorldDocument Load(TextReader reader)
    {
        RuleSet? rules = null;
        int? size = null;
        var wraps = true;
        var generation = 0;
        var lineNumber = 0;

        while (true)
        {
            var line = reader.ReadLine();
            lineNumber++;
            if (line is null)
                throw new WorldFileFormatException("Nieoczekiwany koniec pliku - brak sekcji GRID.", lineNumber);

            line = line.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var keyword = parts[0].ToUpperInvariant();
            var value = parts.Length > 1 ? parts[1] : "";

            if (keyword == "GRID")
                break;

            switch (keyword)
            {
                case "RULES":
                    if (!RuleSet.TryParse(value, out rules, out var error))
                        throw new WorldFileFormatException($"Niepoprawne reguły: {error}", lineNumber);
                    break;
                case "SIZE":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var s) ||
                        s is < World.MinSize or > World.MaxSize)
                        throw new WorldFileFormatException($"Rozmiar musi być liczbą {World.MinSize}-{World.MaxSize}.", lineNumber);
                    size = s;
                    break;
                case "WRAP":
                    wraps = value switch
                    {
                        "1" => true,
                        "0" => false,
                        _ => throw new WorldFileFormatException("WRAP musi mieć wartość 0 lub 1.", lineNumber),
                    };
                    break;
                case "GENERATION":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out generation))
                        throw new WorldFileFormatException("GENERATION musi być nieujemną liczbą całkowitą.", lineNumber);
                    break;
                default:
                    throw new WorldFileFormatException($"Nieznane słowo kluczowe \"{parts[0]}\".", lineNumber);
            }
        }

        if (rules is null)
            throw new WorldFileFormatException("Brak linii RULES przed GRID.", lineNumber);
        if (size is null)
            throw new WorldFileFormatException("Brak linii SIZE przed GRID.", lineNumber);

        var world = new World(size.Value, wraps) { Generation = generation };

        for (var y = 0; y < size.Value; y++)
        {
            var line = reader.ReadLine();
            lineNumber++;
            if (line is null)
                throw new WorldFileFormatException($"Oczekiwano {size} wierszy planszy, znaleziono {y}.", lineNumber);

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != size.Value)
                throw new WorldFileFormatException($"Oczekiwano {size} pól w wierszu, znaleziono {tokens.Length}.", lineNumber);

            for (var x = 0; x < size.Value; x++)
            {
                if (!TryParseCell(tokens[x], out var cell))
                    throw new WorldFileFormatException($"Niepoprawne pole \"{tokens[x]}\" w kolumnie {x + 1}.", lineNumber);
                if (cell.HasAnimal)
                    cell = cell with { AnimalId = world.AllocateAnimalId() };
                world[x, y] = cell;
            }
        }

        return new WorldDocument(world, rules);
    }

    private static string FormatCell(Cell cell) => cell switch
    {
        { HasPlant: true, HasAnimal: true } => $"PA{cell.AnimalAge}",
        { HasAnimal: true } => $"A{cell.AnimalAge}",
        { HasPlant: true } => "P",
        _ => ".",
    };

    private static bool TryParseCell(string token, out Cell cell)
    {
        cell = Cell.Empty;
        if (token == ".")
            return true;

        var rest = token.AsSpan();
        if (rest.Length > 0 && char.ToUpperInvariant(rest[0]) == 'P')
        {
            cell = cell.WithPlant(true);
            rest = rest[1..];
        }
        if (rest.IsEmpty)
            return cell.HasPlant;

        if (char.ToUpperInvariant(rest[0]) != 'A')
            return false;
        rest = rest[1..];

        var age = 0;
        if (!rest.IsEmpty && !int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out age))
            return false;

        cell = cell.WithAnimal(age);
        return true;
    }
}
