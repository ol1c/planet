using System.Windows;
using System.Windows.Input;
using Planet.App.Services;
using Planet.App.ViewModels;

namespace Planet.App;

public partial class MainWindow : Window
{
    private Point? _panStart;
    private (int X, int Y) _panOrigin;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DialogService());
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnBoardMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var (fx, fy, _) = BoardFraction(e.GetPosition(BoardHost));
        ViewModel.ZoomAt(fx, fy, e.Delta / 120.0);
        e.Handled = true;
    }

    private void OnBoardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;
        _panStart = e.GetPosition(BoardHost);
        _panOrigin = (ViewModel.ViewportX, ViewModel.ViewportY);
        BoardHost.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnBoardMouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is not { } start || e.MiddleButton != MouseButtonState.Pressed)
            return;
        var position = e.GetPosition(BoardHost);
        var (_, _, side) = BoardFraction(position);
        var cellPixels = side / ViewModel.ZoomSize;
        ViewModel.ViewportX = _panOrigin.X - (int)Math.Round((position.X - start.X) / cellPixels);
        ViewModel.ViewportY = _panOrigin.Y - (int)Math.Round((position.Y - start.Y) / cellPixels);
    }

    private void OnBoardMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _panStart is null)
            return;
        _panStart = null;
        BoardHost.ReleaseMouseCapture();
        Cursor = null;
        e.Handled = true;
    }

    private (double X, double Y, double Side) BoardFraction(Point position)
    {
        var side = Math.Min(BoardHost.ActualWidth, BoardHost.ActualHeight);
        if (side <= 0)
            return (0.5, 0.5, 1);
        var left = (BoardHost.ActualWidth - side) / 2;
        var top = (BoardHost.ActualHeight - side) / 2;
        return (Math.Clamp((position.X - left) / side, 0, 1), Math.Clamp((position.Y - top) / side, 0, 1), side);
    }
}
