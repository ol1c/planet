using System.Windows.Media.Imaging;
using Planet.App.Services;
using Planet.Core.IO;
using Planet.Core.Model;

namespace Planet.App.ViewModels;

public sealed class BirthRecordViewModel
{
    public const int SnapshotRadius = 7;

    private const int ThumbnailScale = 6;

    private BitmapSource? _thumbnail;

    public BirthRecordViewModel(int id, int generation, int x, int y, Cell?[,] snapshot)
    {
        Id = id;
        Generation = generation;
        X = x;
        Y = y;
        Snapshot = snapshot;
    }

    public int Id { get; }
    public int Generation { get; }
    public int X { get; }
    public int Y { get; }

    public Cell?[,] Snapshot { get; }

    public string Title => $"#{Id}";
    public string Details => $"krok {Generation} · pole ({X}, {Y})";

    public BitmapSource Thumbnail => _thumbnail ??= Render(ThumbnailScale);

    public BitmapSource Render(int scale)
    {
        var side = Snapshot.GetLength(0) * scale;
        return BoardImages.CreateBitmap(WorldRasterizer.Rasterize(Snapshot, scale, highlightCenter: true), side, side);
    }
}
