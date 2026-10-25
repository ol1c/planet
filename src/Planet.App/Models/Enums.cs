namespace Planet.App.Models;

public enum EditTool
{
    Plant,
    Animal,
    Erase,
}

public enum RecordFormat
{
    Gif,

    PngSequence,
}

public readonly record struct CellPaintRequest(int X, int Y, bool Erase);
