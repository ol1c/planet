using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;

namespace University.Application.Services;

public class IndeksUczelnianyService
{
    private readonly IUniversityDbContext _context;

    public IndeksUczelnianyService(IUniversityDbContext context)
    {
        _context = context;
    }

    public async Task<string> PrzydzielKolejnyIndeksAsync(string prefix)
    {
        var znormalizowanyPrefix = NormalizujPrefix(prefix);

        var licznik = await _context.LicznikiIndeksow
            .FirstOrDefaultAsync(l => l.Prefix == znormalizowanyPrefix)
            ?? throw new InvalidOperationException($"Brak zdefiniowanego licznika dla prefiksu '{znormalizowanyPrefix}'.");

        licznik.AktualnaWartosc++;
        return $"{licznik.Prefix}{licznik.AktualnaWartosc}";
    }

    public async Task<bool> CofnijLicznikJesliOstatniAsync(string indeksUczelniany)
    {
        var prefix = new string(indeksUczelniany.TakeWhile(char.IsLetter).ToArray());
        var numerTekst = new string(indeksUczelniany.SkipWhile(char.IsLetter).ToArray());

        if (!int.TryParse(numerTekst, out var numer))
            return false;

        var licznik = await _context.LicznikiIndeksow.FirstOrDefaultAsync(l => l.Prefix == prefix);
        if (licznik is null || licznik.AktualnaWartosc != numer)
            return false;

        licznik.AktualnaWartosc--;
        return true;
    }

    public static string NormalizujPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefiks nie może być pusty.", nameof(prefix));

        var znormalizowany = prefix.Trim().ToUpperInvariant();
        if (!znormalizowany.All(char.IsLetter))
            throw new ArgumentException("Prefiks może składać się wyłącznie z liter.", nameof(prefix));

        return znormalizowany;
    }
}
