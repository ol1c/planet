using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Planet.Core.Model;

namespace Planet.App.ViewModels;

public sealed class RulesViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, string> _errors = [];
    private string _plantBirth = "";
    private string _plantSurvival = "";
    private string _animalBirth = "";
    private string _animalBirthPlants = "";
    private string _animalSurvival = "";
    private string _moveRange = "";
    private string _lifespan = "";
    private bool _isLoading;

    public RulesViewModel(RuleSet rules)
    {
        Rules = rules;
        Load(rules);
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public RuleSet Rules { get; private set; }

    public string RuleString => Rules.ToString();

    public string PlantBirth { get => _plantBirth; set => SetRule(ref _plantBirth, value); }
    public string PlantSurvival { get => _plantSurvival; set => SetRule(ref _plantSurvival, value); }
    public string AnimalBirth { get => _animalBirth; set => SetRule(ref _animalBirth, value); }
    public string AnimalBirthPlants { get => _animalBirthPlants; set => SetRule(ref _animalBirthPlants, value); }
    public string AnimalSurvival { get => _animalSurvival; set => SetRule(ref _animalSurvival, value); }
    public string MoveRange { get => _moveRange; set => SetRule(ref _moveRange, value); }
    public string Lifespan { get => _lifespan; set => SetRule(ref _lifespan, value); }

    public bool HasErrors => _errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out var error) ? new[] { error } : Array.Empty<string>();

    public void Load(RuleSet rules)
    {
        _isLoading = true;
        PlantBirth = rules.PlantBirth.ToString();
        PlantSurvival = rules.PlantSurvival.ToString();
        AnimalBirth = rules.AnimalBirth.ToString();
        AnimalBirthPlants = rules.AnimalBirthPlants.ToString();
        AnimalSurvival = rules.AnimalSurvival.ToString();
        MoveRange = rules.MoveRange.ToString();
        Lifespan = rules.Lifespan.ToString();
        _isLoading = false;
        Validate();
    }

    private void SetRule(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value ?? "", propertyName) && !_isLoading)
            Validate();
    }

    private void Validate()
    {
        var sets = new Dictionary<string, CountSet>();
        foreach (var (name, text) in new[]
                 {
                     (nameof(PlantBirth), PlantBirth), (nameof(PlantSurvival), PlantSurvival),
                     (nameof(AnimalBirth), AnimalBirth), (nameof(AnimalBirthPlants), AnimalBirthPlants),
                     (nameof(AnimalSurvival), AnimalSurvival),
                 })
        {
            SetError(name, CountSet.TryParse(text, out var set, out var error) ? null : error);
            sets[name] = set;
        }

        var move = ValidateNumber(nameof(MoveRange), MoveRange, RuleSet.MinMoveRange, RuleSet.MaxMoveRange);
        var life = ValidateNumber(nameof(Lifespan), Lifespan, RuleSet.MinLifespan, RuleSet.MaxLifespan);

        if (HasErrors)
            return;

        var rules = new RuleSet(sets[nameof(PlantBirth)], sets[nameof(PlantSurvival)], sets[nameof(AnimalBirth)],
            sets[nameof(AnimalBirthPlants)], sets[nameof(AnimalSurvival)], move, life);
        if (rules.Equals(Rules))
            return;

        Rules = rules;
        OnPropertyChanged(nameof(Rules));
        OnPropertyChanged(nameof(RuleString));
    }

    private int ValidateNumber(string propertyName, string text, int min, int max)
    {
        var ok = int.TryParse(text, out var value) && value >= min && value <= max;
        SetError(propertyName, ok ? null : $"Wpisz liczbę całkowitą z zakresu {min}-{max}.");
        return value;
    }

    private void SetError(string propertyName, string? error)
    {
        var changed = error is null
            ? _errors.Remove(propertyName)
            : !_errors.TryGetValue(propertyName, out var old) || old != error;
        if (error is not null)
            _errors[propertyName] = error;
        if (!changed)
            return;

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
    }
}
