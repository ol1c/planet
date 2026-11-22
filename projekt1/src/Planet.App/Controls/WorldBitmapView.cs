using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Planet.App.Models;
using Planet.Core.Model;

namespace Planet.App.Controls;

public sealed class WorldBitmapView : FrameworkElement
{
    private const double GridMinCellPixels = 6;

    public static readonly DependencyProperty WorldProperty = DependencyProperty.Register(
        nameof(World), typeof(World), typeof(WorldBitmapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

    public static readonly DependencyProperty RenderVersionProperty = DependencyProperty.Register(
        nameof(RenderVersion), typeof(int), typeof(WorldBitmapView),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

    public static readonly DependencyProperty EmptyBrushProperty = RegisterColor(nameof(EmptyBrush), Colors.White);
    public static readonly DependencyProperty PlantBrushProperty = RegisterColor(nameof(PlantBrush), Colors.Green);
    public static readonly DependencyProperty AnimalBrushProperty = RegisterColor(nameof(AnimalBrush), Colors.SaddleBrown);
    public static readonly DependencyProperty AnimalOnPlantBrushProperty = RegisterColor(nameof(AnimalOnPlantBrush), Colors.Orange);

    public static readonly DependencyProperty BorderBrushProperty = RegisterBrush(nameof(BorderBrush), Brushes.Gray);
    public static readonly DependencyProperty GridBrushProperty = RegisterBrush(nameof(GridBrush), null);
    public static readonly DependencyProperty ViewportBrushProperty = RegisterBrush(nameof(ViewportBrush), Brushes.Red);
    public static readonly DependencyProperty MarkerBrushProperty = RegisterBrush(nameof(MarkerBrush), Brushes.Blue);

    public static readonly DependencyProperty SourceXProperty = RegisterInt(nameof(SourceX), 0);
    public static readonly DependencyProperty SourceYProperty = RegisterInt(nameof(SourceY), 0);
    public static readonly DependencyProperty SourceSizeProperty = RegisterInt(nameof(SourceSize), 0);

    public static readonly DependencyProperty ShowViewportProperty = DependencyProperty.Register(
        nameof(ShowViewport), typeof(bool), typeof(WorldBitmapView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportXProperty = RegisterInt(nameof(ViewportX), 0);
    public static readonly DependencyProperty ViewportYProperty = RegisterInt(nameof(ViewportY), 0);
    public static readonly DependencyProperty ViewportSizeProperty = RegisterInt(nameof(ViewportSize), 0);

    public static readonly DependencyProperty MarkerXProperty = RegisterInt(nameof(MarkerX), -1);
    public static readonly DependencyProperty MarkerYProperty = RegisterInt(nameof(MarkerY), -1);

    public static readonly DependencyProperty CellCommandProperty = DependencyProperty.Register(
        nameof(CellCommand), typeof(ICommand), typeof(WorldBitmapView));

    private WriteableBitmap? _bitmap;
    private int[] _pixels = [];
    private bool _bitmapDirty = true;
    private (int X, int Y)? _lastCell;

    public WorldBitmapView()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        Focusable = false;
    }

    public World? World { get => (World?)GetValue(WorldProperty); set => SetValue(WorldProperty, value); }
    public int RenderVersion { get => (int)GetValue(RenderVersionProperty); set => SetValue(RenderVersionProperty, value); }
    public SolidColorBrush EmptyBrush { get => (SolidColorBrush)GetValue(EmptyBrushProperty); set => SetValue(EmptyBrushProperty, value); }
    public SolidColorBrush PlantBrush { get => (SolidColorBrush)GetValue(PlantBrushProperty); set => SetValue(PlantBrushProperty, value); }
    public SolidColorBrush AnimalBrush { get => (SolidColorBrush)GetValue(AnimalBrushProperty); set => SetValue(AnimalBrushProperty, value); }
    public SolidColorBrush AnimalOnPlantBrush { get => (SolidColorBrush)GetValue(AnimalOnPlantBrushProperty); set => SetValue(AnimalOnPlantBrushProperty, value); }
    public Brush? BorderBrush { get => (Brush?)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public Brush? GridBrush { get => (Brush?)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush? ViewportBrush { get => (Brush?)GetValue(ViewportBrushProperty); set => SetValue(ViewportBrushProperty, value); }
    public Brush? MarkerBrush { get => (Brush?)GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }
    public int SourceX { get => (int)GetValue(SourceXProperty); set => SetValue(SourceXProperty, value); }
    public int SourceY { get => (int)GetValue(SourceYProperty); set => SetValue(SourceYProperty, value); }
    public int SourceSize { get => (int)GetValue(SourceSizeProperty); set => SetValue(SourceSizeProperty, value); }
    public bool ShowViewport { get => (bool)GetValue(ShowViewportProperty); set => SetValue(ShowViewportProperty, value); }
    public int ViewportX { get => (int)GetValue(ViewportXProperty); set => SetValue(ViewportXProperty, value); }
    public int ViewportY { get => (int)GetValue(ViewportYProperty); set => SetValue(ViewportYProperty, value); }
    public int ViewportSize { get => (int)GetValue(ViewportSizeProperty); set => SetValue(ViewportSizeProperty, value); }
    public int MarkerX { get => (int)GetValue(MarkerXProperty); set => SetValue(MarkerXProperty, value); }
    public int MarkerY { get => (int)GetValue(MarkerYProperty); set => SetValue(MarkerYProperty, value); }
    public ICommand? CellCommand { get => (ICommand?)GetValue(CellCommandProperty); set => SetValue(CellCommandProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = Math.Min(availableSize.Width, availableSize.Height);
        if (double.IsInfinity(side))
            side = 300;
        return new Size(side, side);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var world = World;
        var rect = BoardRect();
        if (world is null || rect.Width <= 0)
            return;

        if (_bitmapDirty || _bitmap is null || _bitmap.PixelWidth != world.Size)
            UpdateBitmap(world);

        var (sx, sy, size) = Source(world);
        var cell = rect.Width / size;

        dc.PushClip(new RectangleGeometry(rect));
        dc.DrawImage(_bitmap, new Rect(rect.X - sx * cell, rect.Y - sy * cell, world.Size * cell, world.Size * cell));

        if (GridBrush is { } gridBrush && cell >= GridMinCellPixels)
        {
            var pen = new Pen(gridBrush, 1);
            pen.Freeze();
            for (var i = 1; i < size; i++)
            {
                var offset = Math.Round(i * cell) + 0.5;
                dc.DrawLine(pen, new Point(rect.X + offset, rect.Top), new Point(rect.X + offset, rect.Bottom));
                dc.DrawLine(pen, new Point(rect.Left, rect.Y + offset), new Point(rect.Right, rect.Y + offset));
            }
        }

        if (MarkerX >= 0 && MarkerBrush is { } markerBrush)
        {
            var center = new Point(rect.X + (MarkerX - sx + 0.5) * cell, rect.Y + (MarkerY - sy + 0.5) * cell);
            var radius = Math.Max(cell * 1.5, 7);
            dc.DrawEllipse(null, new Pen(markerBrush, 2.5), center, radius, radius);
        }

        if (ShowViewport && ViewportSize > 0 && ViewportSize < world.Size && ViewportBrush is { } viewportBrush)
        {
            var viewport = new Rect(rect.X + (ViewportX - sx) * cell, rect.Y + (ViewportY - sy) * cell,
                ViewportSize * cell, ViewportSize * cell);
            dc.DrawRectangle(null, new Pen(viewportBrush, 2), viewport);
        }
        dc.Pop();

        if (BorderBrush is { } borderBrush)
            dc.DrawRectangle(null, new Pen(borderBrush, 1), rect);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right))
            return;
        CaptureMouse();
        _lastCell = null;
        SendCell(e.GetPosition(this), e.ChangedButton == MouseButton.Right);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured)
            return;
        SendCell(e.GetPosition(this), e.RightButton == MouseButtonState.Pressed);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
            ReleaseMouseCapture();
    }

    private void SendCell(Point position, bool erase)
    {
        var world = World;
        var rect = BoardRect();
        if (world is null || rect.Width <= 0 || !rect.Contains(position))
            return;

        var (sx, sy, size) = Source(world);
        var x = Math.Clamp(sx + (int)((position.X - rect.X) / rect.Width * size), 0, world.Size - 1);
        var y = Math.Clamp(sy + (int)((position.Y - rect.Y) / rect.Height * size), 0, world.Size - 1);
        if (_lastCell == (x, y))
            return;
        _lastCell = (x, y);

        var request = new CellPaintRequest(x, y, erase);
        if (CellCommand?.CanExecute(request) == true)
            CellCommand.Execute(request);
    }

    private (int X, int Y, int Size) Source(World world)
    {
        var size = SourceSize is > 0 ? Math.Min(SourceSize, world.Size) : world.Size;
        return (Math.Clamp(SourceX, 0, world.Size - size), Math.Clamp(SourceY, 0, world.Size - size), size);
    }

    private Rect BoardRect()
    {
        var side = Math.Min(ActualWidth, ActualHeight);
        return new Rect((ActualWidth - side) / 2, (ActualHeight - side) / 2, side, side);
    }

    private void UpdateBitmap(World world)
    {
        var size = world.Size;
        if (_bitmap is null || _bitmap.PixelWidth != size)
        {
            _bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            _pixels = new int[size * size];
        }

        int empty = ToArgb(EmptyBrush), plant = ToArgb(PlantBrush),
            animal = ToArgb(AnimalBrush), animalOnPlant = ToArgb(AnimalOnPlantBrush);

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var cell = world[x, y];
            _pixels[y * size + x] = cell switch
            {
                { HasAnimal: true, HasPlant: true } => animalOnPlant,
                { HasAnimal: true } => animal,
                { HasPlant: true } => plant,
                _ => empty,
            };
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, size, size), _pixels, size * 4, 0);
        _bitmapDirty = false;
    }

    private static int ToArgb(SolidColorBrush? brush)
    {
        var c = brush?.Color ?? Colors.Transparent;
        return (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((WorldBitmapView)d)._bitmapDirty = true;

    private static DependencyProperty RegisterColor(string name, Color fallback) =>
        DependencyProperty.Register(name, typeof(SolidColorBrush), typeof(WorldBitmapView),
            new FrameworkPropertyMetadata(new SolidColorBrush(fallback), FrameworkPropertyMetadataOptions.AffectsRender, OnContentChanged));

    private static DependencyProperty RegisterBrush(string name, Brush? fallback) =>
        DependencyProperty.Register(name, typeof(Brush), typeof(WorldBitmapView),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    private static DependencyProperty RegisterInt(string name, int fallback) =>
        DependencyProperty.Register(name, typeof(int), typeof(WorldBitmapView),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));
}
