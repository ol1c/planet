using System.IO;
using System.Windows.Input;
using Planet.App.Models;
using Planet.App.Services;
using Planet.Core.IO;

namespace Planet.App.ViewModels;

public sealed partial class MainViewModel
{
    private RecordFormat _recordFormat = RecordFormat.Gif;
    private bool _exportVisibleOnly;
    private IFrameRecorder? _recorder;
    private bool _recordVisibleOnly;
    private int _recordSize;
    private int _recordScale;
    private string _recordingInfo = "";

    public RecordFormat RecordFormat { get => _recordFormat; set => SetProperty(ref _recordFormat, value); }

    public bool ExportVisibleOnly { get => _exportVisibleOnly; set => SetProperty(ref _exportVisibleOnly, value); }

    public bool IsRecording => _recorder is not null;

    public string RecordingInfo { get => _recordingInfo; private set => SetProperty(ref _recordingInfo, value); }

    public ICommand ExportImageCommand { get; private set; } = null!;
    public ICommand ToggleRecordingCommand { get; private set; } = null!;

    private void InitializeExport()
    {
        ExportImageCommand = new RelayCommand(ExportImage);
        ToggleRecordingCommand = new RelayCommand(() =>
        {
            if (IsRecording) StopRecording();
            else StartRecording();
        });
    }

    private (int X, int Y, int Size) ExportRegion(bool visibleOnly, int? fixedSize = null)
    {
        if (!visibleOnly)
            return (0, 0, BoardSize);
        var size = Math.Min(fixedSize ?? ZoomSize, BoardSize);
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
            StatusMessage = $"Zapisano obraz {side}×{side} px: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Nie udało się zapisać obrazu.\n{ex.Message}");
        }
    }

    private void StartRecording()
    {
        var (_, _, size) = ExportRegion(ExportVisibleOnly);
        var scale = BoardImages.ScaleFor(size);
        var side = size * scale;
        try
        {
            if (RecordFormat == RecordFormat.Gif)
            {
                var path = _dialogs.ShowSaveFileDialog($"symulacja_krok{_world.Generation}.gif", FileKind.Gif);
                if (path is null) return;
                _recorder = new GifRecorder(path, side, side, (int)Math.Round(100 / StepsPerSecond));
            }
            else
            {
                var folder = _dialogs.ShowFolderDialog("Folder na klatki PNG");
                if (folder is null) return;
                _recorder = new PngSequenceRecorder(folder);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Nie udało się rozpocząć nagrywania.\n{ex.Message}");
            return;
        }

        _recordVisibleOnly = ExportVisibleOnly;
        _recordSize = size;
        _recordScale = scale;
        OnPropertyChanged(nameof(IsRecording));
        RecordFrame();
        StatusMessage = $"Nagrywanie do: {_recorder.Target}";
    }

    private void StopRecording()
    {
        if (_recorder is null) return;
        var target = _recorder.Target;
        var frames = _recorder.FrameCount;
        try
        {
            _recorder.Dispose();
        }
        catch (IOException ex)
        {
            _dialogs.ShowError($"Błąd przy zamykaniu nagrania.\n{ex.Message}");
        }
        _recorder = null;
        OnPropertyChanged(nameof(IsRecording));
        RecordingInfo = $"Ostatnie nagranie: {frames} klatek";
        StatusMessage = $"Zapisano nagranie ({frames} klatek): {target}";
    }

    private void RecordFrame()
    {
        if (_recorder is null) return;
        var (x, y, size) = ExportRegion(_recordVisibleOnly, _recordSize);
        var side = size * _recordScale;
        try
        {
            _recorder.AddFrame(WorldRasterizer.Rasterize(_world, x, y, size, _recordScale), side, side);
            RecordingInfo = $"Nagrywanie: {_recorder.FrameCount} klatek";
        }
        catch (IOException ex)
        {
            _recorder.Dispose();
            _recorder = null;
            OnPropertyChanged(nameof(IsRecording));
            _dialogs.ShowError($"Nagrywanie przerwane.\n{ex.Message}");
        }
    }
}
