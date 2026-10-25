using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using Planet.App.Services;
using Planet.Core.IO;
using Planet.Core.Simulation;

namespace Planet.App.ViewModels;

public sealed partial class MainViewModel
{
    public const int MaxBirthRecords = 200;

    private const int MaxRecordsPerStep = 30;

    private const int PhotoScale = 24;

    private BirthRecordViewModel? _selectedBirth;
    private bool _followBirths;
    private string _gotoText = "";
    private int _trackedAnimalId;
    private int _markerX = -1;
    private int _markerY = -1;
    private readonly List<BirthRecordViewModel> _pendingBirths = [];

    public ObservableCollection<BirthRecordViewModel> Births { get; } = [];

    public BirthRecordViewModel? SelectedBirth
    {
        get => _selectedBirth;
        set
        {
            if (SetProperty(ref _selectedBirth, value) && value is not null)
                GoToBirth(value);
        }
    }

    public bool FollowBirths { get => _followBirths; set => SetProperty(ref _followBirths, value); }

    public string GotoText { get => _gotoText; set => SetProperty(ref _gotoText, value); }

    public int TrackedAnimalId { get => _trackedAnimalId; private set => SetProperty(ref _trackedAnimalId, value); }

    public int MarkerX { get => _markerX; private set => SetProperty(ref _markerX, value); }
    public int MarkerY { get => _markerY; private set => SetProperty(ref _markerY, value); }

    public ICommand GotoCommand { get; private set; } = null!;
    public ICommand NewerBirthCommand { get; private set; } = null!;
    public ICommand OlderBirthCommand { get; private set; } = null!;
    public ICommand SaveBirthPhotoCommand { get; private set; } = null!;
    public ICommand StopTrackingCommand { get; private set; } = null!;

    private void InitializeBirths()
    {
        GotoCommand = new RelayCommand(Goto, () => !string.IsNullOrWhiteSpace(GotoText));
        NewerBirthCommand = new RelayCommand(() => SelectBirthAt(Births.IndexOf(SelectedBirth!) - 1),
            () => SelectedBirth is not null && Births.IndexOf(SelectedBirth) > 0);
        OlderBirthCommand = new RelayCommand(() => SelectBirthAt(SelectedBirth is null ? 0 : Births.IndexOf(SelectedBirth) + 1),
            () => Births.Count > 0 && (SelectedBirth is null || Births.IndexOf(SelectedBirth) < Births.Count - 1));
        SaveBirthPhotoCommand = new RelayCommand(SaveBirthPhoto, () => SelectedBirth is not null);
        StopTrackingCommand = new RelayCommand(StopTracking, () => TrackedAnimalId != 0);
    }

    private void RecordBirths(IReadOnlyList<AnimalBirth> births)
    {
        for (var i = Math.Max(0, births.Count - MaxRecordsPerStep); i < births.Count; i++)
        {
            var birth = births[i];
            var snapshot = WorldRasterizer.Snapshot(_world, birth.X, birth.Y, BirthRecordViewModel.SnapshotRadius);
            _pendingBirths.Add(new BirthRecordViewModel(birth.Id, _world.Generation, birth.X, birth.Y, snapshot));
        }
    }

    private void AfterSteps()
    {
        if (_pendingBirths.Count == 0) return;

        var newest = _pendingBirths[^1];
        foreach (var record in _pendingBirths.TakeLast(MaxBirthRecords))
            Births.Insert(0, record);
        _pendingBirths.Clear();
        while (Births.Count > MaxBirthRecords)
            Births.RemoveAt(Births.Count - 1);

        if (FollowBirths)
            SelectedBirth = newest;
    }

    private void SelectBirthAt(int index)
    {
        if (index >= 0 && index < Births.Count)
            SelectedBirth = Births[index];
    }

    private void GoToBirth(BirthRecordViewModel record)
    {
        TrackedAnimalId = record.Id;
        FocusZoom();
        if (_world.TryFindAnimal(record.Id, out var x, out var y, out var cell))
        {
            CenterOn(x, y);
            StatusMessage = $"Zwierzę #{record.Id}: pole ({x}, {y}), wiek {cell.AnimalAge}, głód {cell.Hunger}" +
                            (cell.Satiety > 0 ? $", szuka towarzystwa jeszcze {cell.Satiety} kr." : "") +
                            $" · urodzone w kroku {record.Generation} na ({record.X}, {record.Y}).";
        }
        else
        {
            CenterOn(record.X, record.Y);
            StatusMessage = $"Zwierzę #{record.Id} już nie żyje – pokazuję miejsce narodzin ({record.X}, {record.Y}).";
        }
        UpdateTrackedMarker();
        RefreshViewport();
    }

    private void Goto()
    {
        var text = GotoText.Trim();
        if (text.StartsWith('#') && int.TryParse(text[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            if (_world.TryFindAnimal(id, out var ax, out var ay, out var cell))
            {
                TrackedAnimalId = id;
                FocusZoom();
                CenterOn(ax, ay);
                UpdateTrackedMarker();
                RefreshViewport();
                StatusMessage = $"Zwierzę #{id}: pole ({ax}, {ay}), wiek {cell.AnimalAge}.";
            }
            else
            {
                StatusMessage = $"Nie ma na planszy żywego zwierzęcia #{id}.";
            }
            return;
        }

        var parts = text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) && _world.Contains(x, y))
        {
            FocusZoom();
            CenterOn(x, y);
            StatusMessage = $"Widok wyśrodkowany na polu ({x}, {y}).";
        }
        else
        {
            StatusMessage = $"Niezrozumiałe polecenie goto: \"{text}\" – wpisz \"x,y\" (0–{BoardSize - 1}) albo \"#id\".";
        }
    }

    private void FocusZoom()
    {
        if (ZoomSize > DetailThreshold)
            ZoomSize = Math.Min(FocusZoomSize, BoardSize);
    }

    private void SaveBirthPhoto()
    {
        if (SelectedBirth is not { } record) return;
        var path = _dialogs.ShowSaveFileDialog($"narodziny_{record.Id}_krok{record.Generation}.png", FileKind.Png);
        if (path is null) return;
        try
        {
            BoardImages.SavePng(record.Render(PhotoScale), path);
            StatusMessage = $"Zapisano zdjęcie otoczenia zwierzęcia #{record.Id}: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Nie udało się zapisać zdjęcia.\n{ex.Message}");
        }
    }

    private void StopTracking()
    {
        TrackedAnimalId = 0;
        _selectedBirth = null;
        OnPropertyChanged(nameof(SelectedBirth));
        UpdateTrackedMarker();
        RefreshViewport();
    }

    private void UpdateTrackedMarker()
    {
        if (TrackedAnimalId != 0 && _world.TryFindAnimal(TrackedAnimalId, out var x, out var y, out _))
            (MarkerX, MarkerY) = (x, y);
        else
            (MarkerX, MarkerY) = (-1, -1);
    }

    private void ClearBirths()
    {
        _pendingBirths.Clear();
        Births.Clear();
        _selectedBirth = null;
        OnPropertyChanged(nameof(SelectedBirth));
        TrackedAnimalId = 0;
    }
}
