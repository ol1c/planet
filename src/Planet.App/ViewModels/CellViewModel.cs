using System.Windows.Input;
using Planet.Core.Model;

namespace Planet.App.ViewModels;

public sealed class CellViewModel(ICommand editCommand, ICommand eraseCommand) : ObservableObject
{
    private int _x;
    private int _y;
    private bool _hasPlant;
    private bool _hasAnimal;
    private int _animalAge;
    private bool _isNewborn;
    private bool _isTracked;

    public ICommand EditCommand { get; } = editCommand;

    public ICommand EraseCommand { get; } = eraseCommand;

    public int X { get => _x; private set => SetProperty(ref _x, value); }
    public int Y { get => _y; private set => SetProperty(ref _y, value); }
    public bool HasPlant { get => _hasPlant; private set => SetProperty(ref _hasPlant, value); }
    public bool HasAnimal { get => _hasAnimal; private set => SetProperty(ref _hasAnimal, value); }
    public int AnimalAge { get => _animalAge; private set => SetProperty(ref _animalAge, value); }

    public bool IsNewborn { get => _isNewborn; private set => SetProperty(ref _isNewborn, value); }

    public bool IsTracked { get => _isTracked; private set => SetProperty(ref _isTracked, value); }

    public string Description => (HasPlant, HasAnimal) switch
    {
        (true, true) => $"– roślina i zwierzę #{AnimalId} (wiek {AnimalAge})",
        (false, true) => $"– zwierzę #{AnimalId} (wiek {AnimalAge})",
        (true, false) => "– roślina",
        _ => "– puste",
    };

    public int AnimalId { get; private set; }

    public void Update(int x, int y, Cell cell, int trackedAnimalId = 0)
    {
        X = x;
        Y = y;
        var changed = HasPlant != cell.HasPlant || HasAnimal != cell.HasAnimal || AnimalAge != cell.AnimalAge || AnimalId != cell.AnimalId;
        AnimalId = cell.AnimalId;
        HasPlant = cell.HasPlant;
        HasAnimal = cell.HasAnimal;
        AnimalAge = cell.AnimalAge;
        IsNewborn = cell.HasAnimal && cell.AnimalAge == 0;
        IsTracked = cell.HasAnimal && trackedAnimalId != 0 && cell.AnimalId == trackedAnimalId;
        if (changed)
            OnPropertyChanged(nameof(Description));
    }
}
