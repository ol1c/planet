using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using Planet.App.Models;
using Planet.App.Services;
using Planet.Core.IO;
using Planet.Core.Model;
using Planet.Core.Simulation;

namespace Planet.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public const int StandardMaxBoardSize = 300;

    private readonly IDialogService _dialogs;
    private readonly Simulator _simulator = new();
    private readonly SimulationStatistics _totals = new();
    private readonly DispatcherTimer _timer;

    private World _world;
    private bool _isRunning;
    private double _stepsPerSecond = 10;
    private int _stepsPerTick = 1;
    private EditTool _tool = EditTool.Plant;
    private int _newBoardSize = 60;
    private bool _isLargeMode;
    private double _plantDensity = 0.3;
    private double _animalDensity = 0.05;
    private int _renderVersion;
    private string _statusMessage = "Tryb edycji – kliknij pole, aby postawić roślinę lub zwierzę.";
    private string? _currentFile;

    public MainViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        _world = new World(_newBoardSize);
        Rules = new RulesViewModel(RuleSet.Default);
        Rules.RulesChanged += (_, _) => StatusMessage = $"Reguły: {Rules.RuleString}";

        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += (_, _) => DoSteps(StepsPerTick);
        UpdateTimerInterval();

        ToggleRunCommand = new RelayCommand(ToggleRun);
        StepCommand = new RelayCommand(() => DoSteps(1), () => !IsRunning);
        ClearCommand = new RelayCommand(Clear, () => !IsRunning);
        RandomizeCommand = new RelayCommand(Randomize, () => !IsRunning);
        NewBoardCommand = new RelayCommand(NewBoard, () => !IsRunning && NewBoardSize >= World.MinSize && NewBoardSize <= MaxAllowedBoardSize);
        SaveCommand = new RelayCommand(Save);
        OpenCommand = new RelayCommand(Open, () => !IsRunning);
        ResetRulesCommand = new RelayCommand(() => Rules.Load(RuleSet.Default));
        PaintCellCommand = new RelayCommand(p => Paint((CellPaintRequest)p!), _ => IsEditing);
        EditCellCommand = new RelayCommand(p => PaintCell((CellViewModel)p!, erase: false), _ => IsEditing);
        EraseCellCommand = new RelayCommand(p => PaintCell((CellViewModel)p!, erase: true), _ => IsEditing);

        InitializeView();
        InitializeExport();
        InitializeBirths();

        RebuildViewportCells();
        Refresh();
    }

    #region Właściwości

    public RulesViewModel Rules { get; }

    public StatisticsViewModel Statistics { get; } = new();

    public World World
    {
        get => _world;
        private set
        {
            if (!SetProperty(ref _world, value)) return;
            OnPropertyChanged(nameof(BoardSize));
            OnPropertyChanged(nameof(Wraps));
        }
    }

    public int BoardSize => _world.Size;

    public bool Wraps
    {
        get => _world.Wraps;
        set
        {
            if (_world.Wraps == value) return;
            _world.Wraps = value;
            OnPropertyChanged();
        }
    }

    public bool IsLargeMode
    {
        get => _isLargeMode;
        set
        {
            if (!SetProperty(ref _isLargeMode, value)) return;
            OnPropertyChanged(nameof(MaxAllowedBoardSize));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public int MaxAllowedBoardSize => IsLargeMode ? World.MaxSize : StandardMaxBoardSize;

    public int RenderVersion { get => _renderVersion; private set => SetProperty(ref _renderVersion, value); }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value)) return;
            OnPropertyChanged(nameof(IsEditing));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsEditing => !IsRunning;

    public double StepsPerSecond
    {
        get => _stepsPerSecond;
        set
        {
            if (SetProperty(ref _stepsPerSecond, Math.Clamp(value, 1, 60)))
                UpdateTimerInterval();
        }
    }

    public int StepsPerTick { get => _stepsPerTick; set => SetProperty(ref _stepsPerTick, Math.Clamp(value, 1, 50)); }

    public EditTool Tool { get => _tool; set => SetProperty(ref _tool, value); }

    public int NewBoardSize
    {
        get => _newBoardSize;
        set
        {
            if (SetProperty(ref _newBoardSize, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public double PlantDensity { get => _plantDensity; set => SetProperty(ref _plantDensity, Math.Clamp(value, 0, 1)); }
    public double AnimalDensity { get => _animalDensity; set => SetProperty(ref _animalDensity, Math.Clamp(value, 0, 1)); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    #endregion

    #region Komendy

    public ICommand ToggleRunCommand { get; }
    public ICommand StepCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RandomizeCommand { get; }
    public ICommand NewBoardCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand ResetRulesCommand { get; }

    public ICommand PaintCellCommand { get; }

    public ICommand EditCellCommand { get; }
    public ICommand EraseCellCommand { get; }

    #endregion

    private void ToggleRun()
    {
        if (IsRunning)
        {
            _timer.Stop();
            IsRunning = false;
            StatusMessage = "Zatrzymano – tryb edycji.";
        }
        else
        {
            IsRunning = true;
            _timer.Start();
            StatusMessage = "Symulacja trwa…";
        }
    }

    private void DoSteps(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var step = _simulator.Step(_world, Rules.Rules);
            _totals.Add(step);
            RecordBirths(step.Births);
        }
        Refresh();
        AfterSteps();
        RecordFrame();
    }

    private void Clear()
    {
        _world.Clear();
        ResetHistory();
        Refresh();
        StatusMessage = "Plansza wyczyszczona.";
    }

    private void Randomize()
    {
        WorldGenerator.Randomize(_world, PlantDensity, AnimalDensity, Rules.Rules.Lifespan);
        ResetHistory();
        Refresh();
        StatusMessage = "Wylosowano nowy stan planszy.";
    }

    private void NewBoard()
    {
        ReplaceWorld(new World(NewBoardSize, Wraps));
        StatusMessage = $"Nowa plansza {BoardSize}×{BoardSize}.";
    }

    private void Save()
    {
        var path = _dialogs.ShowSaveFileDialog(Path.GetFileName(_currentFile) ?? "plansza.txt");
        if (path is null) return;
        try
        {
            WorldFile.Save(path, _world, Rules.Rules);
            _currentFile = path;
            StatusMessage = $"Zapisano: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Nie udało się zapisać pliku.\n{ex.Message}");
        }
    }

    private void Open()
    {
        var path = _dialogs.ShowOpenFileDialog();
        if (path is null) return;
        try
        {
            var document = WorldFile.Load(path);
            if (document.World.Size > StandardMaxBoardSize)
                IsLargeMode = true;
            Rules.Load(document.Rules);
            ReplaceWorld(document.World);
            _currentFile = path;
            StatusMessage = $"Wczytano: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            _dialogs.ShowError($"Nie udało się wczytać pliku.\n{ex.Message}");
        }
    }

    private void ReplaceWorld(World world)
    {
        StopRecording();
        var wasWhole = ZoomSize >= BoardSize;
        World = world;
        NewBoardSize = world.Size;
        ResetHistory();
        OnBoardSizeChanged(wasWhole);
        Refresh();
    }

    private void ResetHistory()
    {
        _totals.Reset();
        ClearBirths();
    }

    private void Paint(CellPaintRequest request)
    {
        if (!_world.Contains(request.X, request.Y)) return;
        ApplyTool(request.X, request.Y, request.Erase);
    }

    private void PaintCell(CellViewModel cell, bool erase) => ApplyTool(cell.X, cell.Y, erase);

    private void ApplyTool(int x, int y, bool erase)
    {
        var cell = _world[x, y];
        var updated = erase ? Cell.Empty : Tool switch
        {
            EditTool.Plant => cell.WithPlant(true),
            EditTool.Animal when !cell.HasAnimal => cell.WithAnimal(0, _world.AllocateAnimalId()),
            EditTool.Animal => cell,
            _ => Cell.Empty,
        };
        if (updated == cell) return;
        _world[x, y] = updated;
        Refresh();
    }

    private void UpdateTimerInterval() => _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / StepsPerSecond);

    private void Refresh()
    {
        RenderVersion++;
        UpdateTrackedMarker();
        RefreshViewport();
        Statistics.Update(_world, _totals);
    }
}
