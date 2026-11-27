using Microsoft.Extensions.DependencyInjection;
using University.Application.Services;

namespace University.UI.Menu;

public class LicznikiMenu : MenuSekcji
{
    public LicznikiMenu(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public override async Task UruchomAsync()
    {
        while (true)
        {
            var wybor = Konsola.Wybor("Liczniki indeksów",
                "1. Tabela LicznikiIndeksow",
                "2. Dodaj prefiks (np. D z wartością 0)",
                "3. Zmień wartość licznika",
                "4. Usuń prefiks",
                "0. Powrót");

            switch (wybor)
            {
                case "1": await WykonajAsync<LicznikIndeksowService>(WyswietlAsync); break;
                case "2": await WykonajAsync<LicznikIndeksowService>(DodajAsync); break;
                case "3": await WykonajAsync<LicznikIndeksowService>(EdytujAsync); break;
                case "4": await WykonajAsync<LicznikIndeksowService>(UsunAsync); break;
                case "0": return;
                default: Konsola.Blad("Nieznana opcja."); break;
            }
        }
    }

    private static async Task WyswietlAsync(LicznikIndeksowService service)
    {
        var liczniki = await service.GetAllAsync();
        Konsola.Naglowek("LicznikiIndeksow");
        Console.WriteLine($"  {"Prefix",-8}{"AktualnaWartosc",16}  Ostatnio nadany");
        foreach (var l in liczniki)
            Console.WriteLine($"  {l.Prefix,-8}{l.AktualnaWartosc,16}  {l.Prefix}{l.AktualnaWartosc}");
    }

    private static async Task DodajAsync(LicznikIndeksowService service)
    {
        var prefix = Konsola.Tekst("Prefiks", "D");
        var wartosc = Konsola.Liczba("Wartość początkowa", 0);
        var licznik = await service.CreateAsync(prefix, wartosc);
        Konsola.Sukces($"Dodano licznik '{licznik.Prefix}' = {licznik.AktualnaWartosc}.");
    }

    private static async Task EdytujAsync(LicznikIndeksowService service)
    {
        var prefix = Konsola.Tekst("Prefiks");
        var licznik = await service.GetAsync(prefix)
            ?? throw new KeyNotFoundException($"Nie znaleziono licznika dla prefiksu '{prefix}'.");
        var wartosc = Konsola.Liczba("Nowa wartość", licznik.AktualnaWartosc);
        await service.UpdateAsync(prefix, wartosc);
        Konsola.Sukces($"Licznik '{licznik.Prefix}' = {wartosc}.");
    }

    private static async Task UsunAsync(LicznikIndeksowService service)
    {
        var prefix = Konsola.Tekst("Prefiks do usunięcia");
        await service.DeleteAsync(prefix);
        Konsola.Sukces($"Usunięto licznik '{prefix.Trim().ToUpperInvariant()}'.");
    }
}
