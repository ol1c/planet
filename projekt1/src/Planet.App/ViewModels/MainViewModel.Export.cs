using System.IO;
using System.Windows.Input;
using Planet.App.Models;
using Planet.App.Services;
using Planet.Core.IO;

namespace Planet.App.ViewModels;

public sealed partial class MainViewModel
{
    private bool _exportVisibleOnly;

    public bool ExportVisibleOnly { get => _exportVisibleOnly; set => SetProperty(ref _exportVisibleOnly, value); }

    public ICommand ExportImageCommand { get; private set; } = null!;

    private void InitializeExport()
    {
        ExportImageCommand = new RelayCommand(ExportImage);
    }

    private (int X, int Y, int Size) ExportRegion(bool visibleOnly)
    {
        if (!visibleOnly)
            return (0, 0, BoardSize);
        var size = Math.Min(ZoomSize, BoardSize);
        return (Math.Clamp(ViewportX, 0, BoardSize - size), Math.Clamp(ViewportY, 0, BoardSize - size), size);
    }

    private void ExportImage()
    {
        var path = _dialogs.ShowSaveFileDialog($"plansza_krok{_world.Generation}.png", FileKind.Png);
        if (path is null) return;

        var (x, y, size) = ExportRegion(ExportVisibleOnly);
        var scale = BoardImages.ScaleFor(size, 1200);
        try
        {
            var side = size * scale;
            BoardImages.SavePng(BoardImages.CreateBitmap(WorldRasterizer.Rasterize(_world, x, y, size, scale), side, side), path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Nie udało się zapisać obrazu.\n{ex.Message}");
        }
    }
}
