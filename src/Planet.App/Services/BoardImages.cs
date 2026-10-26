using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Planet.Core.IO;

namespace Planet.App.Services;

public static class BoardImages
{
    private static readonly string[] PaletteKeys =
    [
        "EmptyCellColor",
        "PlantColor",
        "AnimalColor",
        "AnimalOnPlantColor",
        "OutsideColor",
        "HighlightColor",
    ];

    public static IReadOnlyList<Color> Palette() =>
        PaletteKeys.Select(key => Application.Current.TryFindResource(key) is Color c ? c : Colors.Magenta).ToList();

    public static BitmapSource CreateBitmap(byte[] indices, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Indexed8,
            new BitmapPalette(Palette().ToList()), indices, width);
        bitmap.Freeze();
        return bitmap;
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    public static int ScaleFor(int cells, int targetPixels = 800) => Math.Clamp(targetPixels / Math.Max(1, cells), 1, 16);
}
