using System.Globalization;
using Microsoft.EntityFrameworkCore;
using University.Domain.ValueObjects;

namespace University.UI.Menu;

public static class Konsola
{
    public static void Naglowek(string tekst)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== {tekst} ===");
        Console.ResetColor();
    }

    public static void Sukces(string tekst) => Kolorowo(tekst, ConsoleColor.Green);

    public static void Blad(string tekst) => Kolorowo(tekst, ConsoleColor.Red);

    public static void Info(string tekst) => Kolorowo(tekst, ConsoleColor.Yellow);

    private static void Kolorowo(string tekst, ConsoleColor kolor)
    {
        Console.ForegroundColor = kolor;
        Console.WriteLine(tekst);
        Console.ResetColor();
    }

    public static string Wybor(string tytul, params string[] opcje)
    {
        Naglowek(tytul);
        foreach (var opcja in opcje)
            Console.WriteLine($"  {opcja}");
        Console.Write("Wybór: ");
        return (Console.ReadLine() ?? "0").Trim();
    }

    public static string Tekst(string etykieta, string? domyslna = null)
    {
        Console.Write(domyslna is null ? $"{etykieta}: " : $"{etykieta} [{domyslna}]: ");
        var wartosc = Console.ReadLine() ?? string.Empty;
        return string.IsNullOrWhiteSpace(wartosc) && domyslna is not null ? domyslna : wartosc;
    }

    public static string? TekstOpcjonalny(string etykieta, string? domyslna = null)
    {
        Console.Write(domyslna is null ? $"{etykieta} (Enter = brak): " : $"{etykieta} [{domyslna}] ('-' = brak): ");
        var wartosc = (Console.ReadLine() ?? string.Empty).Trim();
        if (wartosc == "-")
            return null;
        return string.IsNullOrEmpty(wartosc) ? domyslna : wartosc;
    }

    public static int Liczba(string etykieta, int? domyslna = null)
    {
        while (true)
        {
            var tekst = Tekst(etykieta, domyslna?.ToString(CultureInfo.InvariantCulture));
            if (int.TryParse(tekst, out var wynik))
                return wynik;
            Blad("Podaj poprawną liczbę całkowitą.");
        }
    }

    public static int? LiczbaOpcjonalna(string etykieta, int? domyslna = null)
    {
        while (true)
        {
            var tekst = TekstOpcjonalny(etykieta, domyslna?.ToString(CultureInfo.InvariantCulture));
            if (tekst is null)
                return null;
            if (int.TryParse(tekst, out var wynik))
                return wynik;
            Blad("Podaj poprawną liczbę całkowitą.");
        }
    }

    public static List<int> ListaLiczb(string etykieta, IEnumerable<int>? domyslne = null)
    {
        var domyslnyTekst = domyslne is null ? null : string.Join(",", domyslne);
        while (true)
        {
            var tekst = Tekst($"{etykieta} (Id rozdzielone przecinkami)", domyslnyTekst ?? string.Empty);
            var czesci = tekst.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var wynik = new List<int>();
            var poprawne = true;
            foreach (var czesc in czesci)
            {
                if (int.TryParse(czesc, out var liczba))
                    wynik.Add(liczba);
                else
                    poprawne = false;
            }
            if (poprawne)
                return wynik;
            Blad("Lista może zawierać wyłącznie liczby całkowite.");
        }
    }

    public static Adres Adres(Adres? obecny = null)
    {
        Console.WriteLine("Adres zamieszkania:");
        var ulica = Tekst("  Ulica", obecny?.Ulica);
        var miasto = Tekst("  Miasto", obecny?.Miasto);
        var kod = Tekst("  Kod pocztowy (XX-XXX)", obecny?.KodPocztowy);
        return new Adres(ulica, miasto, kod);
    }

    public static string Data(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static async Task Bezpiecznie(Func<Task> akcja)
    {
        try
        {
            await akcja();
        }
        catch (ArgumentException ex)
        {
            Blad($"Błąd walidacji ({ex.GetType().Name}): {ex.Message}");
        }
        catch (KeyNotFoundException ex)
        {
            Blad($"Nie znaleziono: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Blad($"Operacja niedozwolona: {ex.Message}");
        }
        catch (DbUpdateException ex)
        {
            Blad($"Błąd zapisu w bazie danych: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public static void CzekajNaEnter()
    {
        Console.Write("Naciśnij Enter, aby kontynuować...");
        Console.ReadLine();
    }
}
