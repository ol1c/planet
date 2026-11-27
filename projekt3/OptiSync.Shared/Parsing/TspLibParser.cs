using System.IO;
using System.Collections.Immutable;
using System.Globalization;
using OptiSync.Shared.Domain;

namespace OptiSync.Shared.Parsing;

public static class TspLibParser
{
    private const string NodeCoordSection = "NODE_COORD_SECTION";
    private const string EndOfFile = "EOF";

    public static TspInstance ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Nie znaleziono pliku danych: {path}", path);
        }

        return Parse(File.ReadLines(path), Path.GetFileNameWithoutExtension(path));
    }

    public static TspInstance Parse(IEnumerable<string> lines, string fallbackName)
    {
        string name = fallbackName;
        string comment = string.Empty;
        string edgeWeightType = "EUC_2D";
        int? declaredDimension = null;
        var cities = ImmutableArray.CreateBuilder<CityPoint>();
        bool readingCoordinates = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Equals(EndOfFile, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (readingCoordinates)
            {
                if (TryParseCoordinateLine(line, out var city))
                {
                    cities.Add(city);
                    continue;
                }

                readingCoordinates = false;
            }

            if (line.StartsWith(NodeCoordSection, StringComparison.OrdinalIgnoreCase))
            {
                readingCoordinates = true;
                continue;
            }

            if (!TrySplitHeader(line, out string key, out string value))
            {
                continue;
            }

            switch (key)
            {
                case "NAME":
                    name = value;
                    break;
                case "COMMENT":
                    comment = comment.Length == 0 ? value : $"{comment} {value}";
                    break;
                case "TYPE":
                    if (!value.StartsWith("TSP", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new FormatException($"Nieobsługiwany typ problemu TSPLIB: {value}");
                    }
                    break;
                case "DIMENSION":
                    declaredDimension = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "EDGE_WEIGHT_TYPE":
                    edgeWeightType = value.ToUpperInvariant();
                    break;
            }
        }

        if (edgeWeightType != "EUC_2D")
        {
            throw new FormatException($"Obsługiwany jest wyłącznie format EUC_2D (w pliku: {edgeWeightType}).");
        }

        if (cities.Count < 4)
        {
            throw new FormatException("Sekcja NODE_COORD_SECTION musi zawierać co najmniej 4 punkty.");
        }

        if (declaredDimension is int dimension && dimension != cities.Count)
        {
            throw new FormatException($"DIMENSION = {dimension}, a w sekcji współrzędnych znaleziono {cities.Count} punktów.");
        }

        return new TspInstance(name, comment, cities.ToImmutable());
    }

    private static bool TrySplitHeader(string line, out string key, out string value)
    {
        int separator = line.IndexOf(':');
        if (separator < 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = line[..separator].Trim().ToUpperInvariant();
        value = line[(separator + 1)..].Trim();
        return true;
    }

    private static bool TryParseCoordinateLine(string line, out CityPoint city)
    {
        city = default;
        string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
        {
            return false;
        }

        city = new CityPoint(id, x, y);
        return true;
    }
}
