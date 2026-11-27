using System.Text.RegularExpressions;

namespace University.Domain.ValueObjects;

public record Adres
{
    public string Ulica { get; init; } = string.Empty;
    public string Miasto { get; init; } = string.Empty;
    public string KodPocztowy { get; init; } = string.Empty;

    private Adres() { }

    public Adres(string ulica, string miasto, string kodPocztowy)
    {
        if (string.IsNullOrWhiteSpace(ulica))
            throw new ArgumentException("Ulica nie może być pusta.", nameof(ulica));

        if (string.IsNullOrWhiteSpace(miasto))
            throw new ArgumentException("Miasto nie może być puste.", nameof(miasto));

        if (string.IsNullOrWhiteSpace(kodPocztowy))
            throw new ArgumentException("Kod pocztowy nie może być pusty.", nameof(kodPocztowy));

        var znormalizowanyKod = kodPocztowy.Trim();
        if (!Regex.IsMatch(znormalizowanyKod, @"^\d{2}-\d{3}$"))
            throw new ArgumentException("Kod pocztowy musi mieć format XX-XXX (np. 80-233).", nameof(kodPocztowy));

        Ulica = ulica.Trim();
        Miasto = miasto.Trim();
        KodPocztowy = znormalizowanyKod;
    }

    public override string ToString() => $"{Ulica}, {KodPocztowy} {Miasto}";
}
