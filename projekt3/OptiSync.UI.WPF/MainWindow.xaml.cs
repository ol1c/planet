using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using OptiSync.Shared.Domain;
using OptiSync.Shared.Ipc;
using OptiSync.Shared.Parsing;
using OptiSync.Shared.Scripting;
using OptiSync.UI.WPF.Models;
using OptiSync.UI.WPF.Services;

namespace OptiSync.UI.WPF;

public partial class MainWindow : Window
{
    private const double MapMargin = 24;
    private const int MaxLogEntries = 500;

    private static readonly Brush TourBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x6F, 0xD1));
    private static readonly Brush CityBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    private static readonly Brush StartCityBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0x3B, 0x1F));

    private TspInstance? _instance;
    private int[]? _bestTour;
    private WorkerSession? _session;
    private CancellationTokenSource? _sessionCts;
    private bool _isRunning;
    private bool _isPaused;
    private bool _stopRequested;
    private int _leaderWorkerId = -1;
    private long _lastTotalEvaluated;
    private double _lastElapsedSeconds;

    public MainWindow()
    {
        InitializeComponent();
        ScriptBox.Text = TourScriptCompiler.DefaultScript;
        TryLoadDefaultInstance();
        UpdateControlState();
    }

    private void TryLoadDefaultInstance()
    {
        string defaultPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "wi29.tsp");
        if (File.Exists(defaultPath))
        {
            LoadInstance(defaultPath);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz instancję problemu",
            Filter = "TSPLIB (*.tsp)|*.tsp|Wszystkie pliki (*.*)|*.*"
        };

        string dataFolder = System.IO.Path.Combine(AppContext.BaseDirectory, "Data");
        if (Directory.Exists(dataFolder))
        {
            dialog.InitialDirectory = dataFolder;
        }

        if (dialog.ShowDialog(this) == true)
        {
            LoadInstance(dialog.FileName);
        }
    }

    private void LoadInstance(string path)
    {
        try
        {
            _instance = TspLibParser.ParseFile(path);
            _bestTour = null;
            FilePathBox.Text = path;
            InstanceInfoText.Text = $"{_instance.Name}: {_instance.Dimension} miast. {_instance.Comment}";
            TourGrid.ItemsSource = null;
            RedrawMap();
            SetStatus($"Wczytano instancję {_instance.Name} ({_instance.Dimension} miast).");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Błąd wczytywania pliku", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CompileButton_Click(object sender, RoutedEventArgs e)
    {
        CompileButton.IsEnabled = false;
        try
        {
            await CompileScriptAsync(ScriptBox.Text);
        }
        catch (TourScriptCompilationException)
        {
        }
        finally
        {
            CompileButton.IsEnabled = !_isRunning;
        }
    }

    private async Task CompileScriptAsync(string code)
    {
        ScriptStatusText.Foreground = Brushes.Gray;
        ScriptStatusText.Text = "Kompilacja Roslyn w toku…";
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var mutation = await Task.Run(() => TourScriptCompiler.CompileAsync(code));
            var probe = Enumerable.Range(0, Math.Max(4, _instance?.Dimension ?? 10)).ToArray();
            mutation(probe, Random.Shared);
            bool isPermutation = probe.Order().SequenceEqual(Enumerable.Range(0, probe.Length));
            ScriptStatusText.Foreground = isPermutation ? Brushes.DarkGreen : Brushes.DarkOrange;
            ScriptStatusText.Text = isPermutation
                ? $"Skompilowano poprawnie ({stopwatch.ElapsedMilliseconds} ms), test na próbnej trasie zachowuje permutację."
                : "Skompilowano, ale próbna trasa przestała być permutacją – takie wyniki będą odrzucane przez wątki robocze.";
        }
        catch (TourScriptCompilationException ex)
        {
            ScriptStatusText.Foreground = Brushes.DarkRed;
            ScriptStatusText.Text = "Błędy kompilacji:\n" + ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            ScriptStatusText.Foreground = Brushes.DarkRed;
            ScriptStatusText.Text = "Skrypt zgłosił wyjątek podczas testu: " + ex.Message;
            throw new TourScriptCompilationException([ex.Message]);
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        if (_instance is null || string.IsNullOrWhiteSpace(FilePathBox.Text))
        {
            MessageBox.Show(this, "Najpierw wybierz plik instancji (.tsp).", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryReadConfiguration(out int workerCount, out int epochs, out double phase1, out double phase2, out string error))
        {
            MessageBox.Show(this, error, "Niepoprawna konfiguracja", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string? script = null;
        if (UseScriptCheck.IsChecked == true)
        {
            try
            {
                await CompileScriptAsync(ScriptBox.Text);
                script = ScriptBox.Text;
            }
            catch (TourScriptCompilationException)
            {
                return;
            }
        }

        var engine = EngineCombo.SelectedIndex == 1 ? WorkerEngineKind.ThreadQueue : WorkerEngineKind.Tpl;
        var configuration = new WorkerStartConfiguration(FilePathBox.Text, workerCount, epochs, phase1, phase2, script);

        ResetRunView(epochs);
        _isRunning = true;
        _isPaused = false;
        _stopRequested = false;
        UpdateControlState();

        _sessionCts = new CancellationTokenSource();
        try
        {
            SetStatus("Uruchamianie procesu roboczego i zestawianie potoku nazwanego…");
            _session = await WorkerSession.StartAsync(engine, configuration, _sessionCts.Token);
            ProcessText.Text = $"{_session.ProcessName} (PID {_session.ProcessId})";
            AppendLog($"Uruchomiono {_session.ProcessName}, PID {_session.ProcessId}, N = {workerCount}.");

            await foreach (var message in _session.ReadWorkerStreamAsync(_sessionCts.Token))
            {
                HandleWorkerMessage(message);
            }

            int exitCode = await _session.WaitForExitAsync(TimeSpan.FromSeconds(10));
            AppendLog($"Proces roboczy zakończył działanie (kod {exitCode}).");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Odbiór komunikatów anulowany.");
        }
        catch (Exception ex)
        {
            AppendLog("Błąd: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Błąd procesu roboczego", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (_session is not null)
            {
                await _session.DisposeAsync();
                _session = null;
            }

            _sessionCts.Dispose();
            _sessionCts = null;
            _isRunning = false;
            _isPaused = false;
            ProcessText.Text = string.Empty;
            UpdateControlState();
            if (!StatusText.Text.StartsWith("Zakończono", StringComparison.Ordinal))
            {
                SetStatus("Proces roboczy zatrzymany. Najlepszy wynik pozostaje na ekranie.");
            }
        }
    }

    private async void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        _isPaused = !_isPaused;
        UpdateControlState();
        await _session.SendCommandAsync(_isPaused ? IpcMessageTypes.Pause : IpcMessageTypes.Resume, EmptyPayload.Instance);
        SetStatus(_isPaused ? "Wstrzymano – wątki robocze czekają na bramce ManualResetEventSlim." : "Wznowiono obliczenia.");
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        _stopRequested = true;
        UpdateControlState();
        SetStatus("Wysłano Stop – oczekiwanie na kooperatywne zakończenie wątków…");
        await _session.SendCommandAsync(IpcMessageTypes.Stop, EmptyPayload.Instance);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isRunning)
        {
            e.Cancel = true;
            SetStatus("Zamknięcie okna jest zablokowane podczas obliczeń – najpierw zatrzymaj proces (Stop).");
        }
    }

    private async void PhaseTimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isRunning || _session is null)
        {
            return;
        }

        if (TryParsePositive(Phase1Box.Text, out double phase1) && TryParsePositive(Phase2Box.Text, out double phase2))
        {
            await _session.SendCommandAsync(IpcMessageTypes.UpdatePhaseTimes, new PhaseTimesUpdate(phase1, phase2));
            AppendLog($"Zmieniono czasy faz: Faza 1 = {phase1:0.##} s, Faza 2 = {phase2:0.##} s (od kolejnej tury).");
        }
    }

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawMap();

    private void HandleWorkerMessage(IpcMessage message)
    {
        switch (message.MessageType)
        {
            case IpcMessageTypes.WorkerReady:
                var ready = message.ReadPayload<WorkerReadyPayload>()!;
                EngineText.Text = ready.Engine;
                AppendLog($"Worker gotowy: {ready.Engine}, instancja {ready.InstanceName} ({ready.CityCount} miast){(ready.UsesScript ? ", skrypt Roslyn aktywny" : string.Empty)}.");
                break;

            case IpcMessageTypes.NewBestTour:
                var best = message.ReadPayload<NewBestTourPayload>()!;
                ApplyNewBest(best);
                break;

            case IpcMessageTypes.PhaseChanged:
                var phase = message.ReadPayload<PhaseChangedPayload>()!;
                PhaseHeadline.Text = $"Epoka {Math.Max(phase.Epoch, 1)}: {phase.Phase}";
                SetStatus(phase.Description);
                UpdateEpochProgress(phase.CompletedEpochs, phase.MaxEpochs);
                AppendLog(phase.Description);
                break;

            case IpcMessageTypes.TelemetryTick:
                ApplyTelemetry(message.ReadPayload<TelemetryTickPayload>()!);
                break;

            case IpcMessageTypes.WorkerLog:
                AppendLog(message.ReadPayload<WorkerLogPayload>()!.Message);
                break;

            case IpcMessageTypes.WorkerError:
                var error = message.ReadPayload<WorkerErrorPayload>()!;
                AppendLog("Błąd procesu roboczego: " + error.Message);
                MessageBox.Show(this, error.Message, "Błąd procesu roboczego", MessageBoxButton.OK, MessageBoxImage.Error);
                break;

            case IpcMessageTypes.WorkerFinished:
                var finished = message.ReadPayload<WorkerFinishedPayload>()!;
                PhaseHeadline.Text = "Zakończono: " + finished.Reason;
                SetStatus($"Zakończono: {finished.Reason}. Najlepszy koszt {finished.BestCost:F2} (wątek #{finished.LeaderWorkerId}), {finished.TotalEvaluated:N0} rozwiązań w {finished.ElapsedSeconds:F1} s.");
                AppendLog(StatusText.Text);
                break;
        }
    }

    private void ApplyNewBest(NewBestTourPayload best)
    {
        _bestTour = best.Tour;
        _leaderWorkerId = best.WorkerId;
        BestResultText.Text = best.Cost.ToString("N2", CultureInfo.CurrentCulture);
        LeaderText.Text = best.WorkerId == 0 ? "Faza 0" : $"Thread #{best.WorkerId}";
        RedrawMap();
        UpdateTourTable();
        AppendLog($"Nowy rekord {best.Cost:F2} – wątek #{best.WorkerId} ({best.Phase}, epoka {best.Epoch}).");
    }

    private void ApplyTelemetry(TelemetryTickPayload tick)
    {
        SolutionCountText.Text = tick.TotalEvaluated.ToString("N0", CultureInfo.CurrentCulture);
        double deltaSeconds = tick.ElapsedSeconds - _lastElapsedSeconds;
        double instantaneous = deltaSeconds > 0 ? (tick.TotalEvaluated - _lastTotalEvaluated) / deltaSeconds : 0;
        _lastElapsedSeconds = tick.ElapsedSeconds;
        _lastTotalEvaluated = tick.TotalEvaluated;
        ThroughputText.Text = $"{tick.SolutionsPerSecond:N0} | {instantaneous:N0}";
        EpochText.Text = $"{Math.Max(tick.Epoch, 1)} / {tick.MaxEpochs}";
        PhaseRemainingText.Text = tick.IsPaused ? $"{tick.PhaseRemainingSeconds:F1} s (pauza)" : $"{tick.PhaseRemainingSeconds:F1} s";
        ElapsedText.Text = $"{tick.ElapsedSeconds:F1} s";
        if (tick.LeaderWorkerId > 0)
        {
            _leaderWorkerId = tick.LeaderWorkerId;
            LeaderText.Text = $"Thread #{tick.LeaderWorkerId}";
        }

        UpdateEpochProgress(tick.CompletedEpochs, tick.MaxEpochs);
        WorkerGrid.ItemsSource = tick.Workers
            .Select(w => new WorkerRow(w.WorkerId, w.ManagedThreadId, w.Iterations, w.RecordsSet, w.BestCost, w.WorkerId == _leaderWorkerId))
            .ToList();
    }

    private void UpdateEpochProgress(int completedEpochs, int maxEpochs)
    {
        EpochProgress.Maximum = Math.Max(1, maxEpochs);
        EpochProgress.Value = Math.Min(completedEpochs, maxEpochs);
        ProgressText.Text = $"{completedEpochs} / {maxEpochs}";
    }

    private void UpdateTourTable()
    {
        if (_instance is null || _bestTour is null)
        {
            TourGrid.ItemsSource = null;
            return;
        }

        TourGrid.ItemsSource = _bestTour
            .Select((cityIndex, position) =>
            {
                var city = _instance.Cities[cityIndex];
                return new TourRow(position + 1, city.Id, city.X, city.Y);
            })
            .ToList();
    }

    private void RedrawMap()
    {
        MapCanvas.Children.Clear();
        if (_instance is null)
        {
            return;
        }

        double width = MapCanvas.ActualWidth;
        double height = MapCanvas.ActualHeight;
        if (width <= 2 * MapMargin || height <= 2 * MapMargin)
        {
            return;
        }

        var cities = _instance.Cities;
        double minX = cities.Min(c => c.X);
        double maxX = cities.Max(c => c.X);
        double minY = cities.Min(c => c.Y);
        double maxY = cities.Max(c => c.Y);
        double spanX = Math.Max(maxX - minX, 1e-9);
        double spanY = Math.Max(maxY - minY, 1e-9);
        double scale = Math.Min((width - 2 * MapMargin) / spanX, (height - 2 * MapMargin) / spanY);
        double offsetX = (width - spanX * scale) / 2;
        double offsetY = (height - spanY * scale) / 2;

        Point Project(CityPoint city) => new(
            offsetX + (city.X - minX) * scale,
            height - (offsetY + (city.Y - minY) * scale));

        if (_bestTour is { Length: > 0 } tour && tour.Length == cities.Length)
        {
            var polyline = new Polyline
            {
                Stroke = TourBrush,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            foreach (int cityIndex in tour)
            {
                polyline.Points.Add(Project(cities[cityIndex]));
            }

            polyline.Points.Add(Project(cities[tour[0]]));
            MapCanvas.Children.Add(polyline);
        }

        double radius = cities.Length > 120 ? 2.5 : 4;
        int startCity = _bestTour is { Length: > 0 } ? _bestTour[0] : -1;
        for (int index = 0; index < cities.Length; index++)
        {
            var point = Project(cities[index]);
            var dot = new Ellipse
            {
                Width = 2 * radius,
                Height = 2 * radius,
                Fill = index == startCity ? StartCityBrush : CityBrush,
                ToolTip = $"Miasto {cities[index].Id}: ({cities[index].X:F1}; {cities[index].Y:F1})"
            };
            Canvas.SetLeft(dot, point.X - radius);
            Canvas.SetTop(dot, point.Y - radius);
            MapCanvas.Children.Add(dot);
        }
    }

    private void ResetRunView(int epochs)
    {
        _bestTour = null;
        _leaderWorkerId = -1;
        _lastTotalEvaluated = 0;
        _lastElapsedSeconds = 0;
        BestResultText.Text = "–";
        LeaderText.Text = "–";
        SolutionCountText.Text = "0";
        ThroughputText.Text = "0";
        EpochText.Text = $"0 / {epochs}";
        PhaseRemainingText.Text = "–";
        ElapsedText.Text = "–";
        EngineText.Text = "–";
        PhaseHeadline.Text = "Faza 0 (Inicjalizacja)";
        WorkerGrid.ItemsSource = null;
        TourGrid.ItemsSource = null;
        LogList.Items.Clear();
        UpdateEpochProgress(0, epochs);
        RedrawMap();
    }

    private void UpdateControlState()
    {
        StartButton.IsEnabled = !_isRunning;
        BrowseButton.IsEnabled = !_isRunning;
        EngineCombo.IsEnabled = !_isRunning;
        WorkerCountBox.IsEnabled = !_isRunning;
        EpochsBox.IsEnabled = !_isRunning;
        UseScriptCheck.IsEnabled = !_isRunning;
        ScriptBox.IsReadOnly = _isRunning;
        CompileButton.IsEnabled = !_isRunning;
        PauseButton.IsEnabled = _isRunning && !_stopRequested;
        PauseButton.Content = _isPaused ? "Resume" : "Pause";
        StopButton.IsEnabled = _isRunning && !_stopRequested;
        ExitButton.IsEnabled = !_isRunning;
    }

    private bool TryReadConfiguration(out int workerCount, out int epochs, out double phase1, out double phase2, out string error)
    {
        phase1 = 0;
        phase2 = 0;
        epochs = 0;
        error = string.Empty;

        if (!int.TryParse(WorkerCountBox.Text, out workerCount) || workerCount < 1 || workerCount > 256)
        {
            error = "Liczba wątków N musi być liczbą całkowitą z zakresu 1–256.";
            return false;
        }

        if (!int.TryParse(EpochsBox.Text, out epochs) || epochs < 1)
        {
            error = "Liczba epok musi być dodatnią liczbą całkowitą.";
            return false;
        }

        if (!TryParsePositive(Phase1Box.Text, out phase1) || !TryParsePositive(Phase2Box.Text, out phase2))
        {
            error = "Czasy faz muszą być dodatnimi liczbami (sekundy).";
            return false;
        }

        return true;
    }

    private static bool TryParsePositive(string text, out double value)
    {
        string normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0 && value <= 3600;
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void AppendLog(string message)
    {
        LogList.Items.Add($"{DateTime.Now:HH:mm:ss.fff}  {message}");
        if (LogList.Items.Count > MaxLogEntries)
        {
            LogList.Items.RemoveAt(0);
        }

        LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
    }
}
