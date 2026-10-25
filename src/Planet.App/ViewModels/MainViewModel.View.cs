using System.Collections.ObjectModel;
using System.Windows.Input;
using Planet.App.Models;

namespace Planet.App.ViewModels;

public sealed partial class MainViewModel
{
    public const int MinZoomSize = 10;

    public const int DetailThreshold = 40;

    private const int FocusZoomSize = 21;

    private int _zoomSize;
    private int _viewportX;
    private int _viewportY;

    public ObservableCollection<CellViewModel> ViewportCells { get; } = [];

    public int ZoomSize
    {
        get => _zoomSize;
        set
        {
            var size = Math.Clamp(value, MinZoomSize, BoardSize);
            var wasDetailed = IsDetailed;
            if (!SetProperty(ref _zoomSize, size)) return;
            OnPropertyChanged(nameof(IsDetailed));
            OnPropertyChanged(nameof(IsWholeBoard));
            OnPropertyChanged(nameof(ZoomDescription));
            ClampViewport();
            if (IsDetailed || wasDetailed)
                RebuildViewportCells();
        }
    }

    public int MaxZoomSize => BoardSize;

    public bool IsDetailed => ZoomSize <= DetailThreshold;

    public bool IsWholeBoard => ZoomSize >= BoardSize;

    public string ZoomDescription =>
        $"{ZoomSize} × {ZoomSize} pól · {(IsDetailed ? "widok szczegółowy" : "widok bitmapowy")}";

    public int MaxViewportOffset => Math.Max(0, BoardSize - ZoomSize);

    public int ViewportX
    {
        get => _viewportX;
        set
        {
            if (SetProperty(ref _viewportX, Math.Clamp(value, 0, MaxViewportOffset)))
                RefreshViewport();
        }
    }

    public int ViewportY
    {
        get => _viewportY;
        set
        {
            if (SetProperty(ref _viewportY, Math.Clamp(value, 0, MaxViewportOffset)))
                RefreshViewport();
        }
    }

    public ICommand ZoomInCommand { get; private set; } = null!;
    public ICommand ZoomOutCommand { get; private set; } = null!;
    public ICommand ShowWholeBoardCommand { get; private set; } = null!;

    public ICommand ZoomPresetCommand { get; private set; } = null!;

    public ICommand CenterViewportCommand { get; private set; } = null!;

    private void InitializeView()
    {
        _zoomSize = BoardSize;
        ZoomInCommand = new RelayCommand(() => ZoomAt(0.5, 0.5, 1), () => ZoomSize > MinZoomSize);
        ZoomOutCommand = new RelayCommand(() => ZoomAt(0.5, 0.5, -1), () => !IsWholeBoard);
        ShowWholeBoardCommand = new RelayCommand(() => ZoomSize = BoardSize);
        ZoomPresetCommand = new RelayCommand(p => ZoomCentered(Convert.ToInt32(p)));
        CenterViewportCommand = new RelayCommand(p =>
        {
            var request = (CellPaintRequest)p!;
            CenterOn(request.X, request.Y);
        });
    }

    public void ZoomAt(double fx, double fy, double steps)
    {
        var anchorX = ViewportX + fx * ZoomSize;
        var anchorY = ViewportY + fy * ZoomSize;

        var target = (int)Math.Round(ZoomSize * Math.Pow(0.85, steps));
        if (target == ZoomSize)
            target += steps > 0 ? -1 : 1;
        ZoomSize = target;

        ViewportX = (int)Math.Round(anchorX - fx * ZoomSize);
        ViewportY = (int)Math.Round(anchorY - fy * ZoomSize);
    }

    public void PanBy(int dx, int dy)
    {
        ViewportX += dx;
        ViewportY += dy;
    }

    public void CenterOn(int x, int y)
    {
        ViewportX = x - ZoomSize / 2;
        ViewportY = y - ZoomSize / 2;
    }

    private void ZoomCentered(int size)
    {
        var centerX = ViewportX + ZoomSize / 2;
        var centerY = ViewportY + ZoomSize / 2;
        ZoomSize = size;
        CenterOn(centerX, centerY);
    }

    private void OnBoardSizeChanged(bool showWhole)
    {
        OnPropertyChanged(nameof(MaxZoomSize));
        var size = showWhole ? BoardSize : Math.Min(_zoomSize, BoardSize);
        if (size == _zoomSize)
        {
            OnPropertyChanged(nameof(IsWholeBoard));
            OnPropertyChanged(nameof(ZoomDescription));
            ClampViewport();
            if (IsDetailed) RebuildViewportCells();
        }
        else
        {
            ZoomSize = size;
        }
    }

    private void ClampViewport()
    {
        OnPropertyChanged(nameof(MaxViewportOffset));
        _viewportX = Math.Clamp(_viewportX, 0, MaxViewportOffset);
        _viewportY = Math.Clamp(_viewportY, 0, MaxViewportOffset);
        OnPropertyChanged(nameof(ViewportX));
        OnPropertyChanged(nameof(ViewportY));
    }

    private void RebuildViewportCells()
    {
        var count = IsDetailed ? ZoomSize * ZoomSize : 0;
        if (ViewportCells.Count == count)
        {
            RefreshViewport();
            return;
        }
        ViewportCells.Clear();
        for (var i = 0; i < count; i++)
            ViewportCells.Add(new CellViewModel(EditCellCommand, EraseCellCommand));
        RefreshViewport();
    }

    private void RefreshViewport()
    {
        var size = ZoomSize;
        if (!IsDetailed || ViewportCells.Count != size * size) return;
        for (var dy = 0; dy < size; dy++)
        for (var dx = 0; dx < size; dx++)
        {
            int x = ViewportX + dx, y = ViewportY + dy;
            ViewportCells[dy * size + dx].Update(x, y, _world[x, y], TrackedAnimalId);
        }
    }
}
