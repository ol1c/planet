using Microsoft.Extensions.DependencyInjection;
using University.Application.Services;

namespace University.UI.Menu;

public class ProfesorowieMenu : MenuSekcji
{
    public ProfesorowieMenu(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public override async Task UruchomAsync()
    {
        while (true)
        {
            var wybor = Konsola.Wybor("Profesorowie",
                "1. Lista profesorów (gabinety, kursy, dyplomanci)",
                "2. Dodaj profesora",
                "3. Edytuj profesora",
                "4. Usuń profesora (Id lub indeks)",
                "0. Powrót");

            switch (wybor)
            {
                case "1": await WykonajAsync<ProfesorService>(WyswietlAsync); break;
                case "2": await WykonajAsync<ProfesorService>(DodajAsync); break;
                case "3": await WykonajAsync<ProfesorService>(EdytujAsync); break;
                case "4": await WykonajAsync(UsunAsync); break;
                case "0": return;
                default: Konsola.Blad("Nieznana opcja."); break;
            }
        }
    }

    private static async Task WyswietlAsync(ProfesorService service)
    {
        var profesorowie = await service.GetAllAsync();
        Konsola.Naglowek($"Profesorowie ({profesorowie.Count})");
        if (profesorowie.Count == 0)
        {
            Konsola.Info("Brak profesorów w bazie.");
            return;
        }

        foreach (var p in profesorowie)
        {
            Console.WriteLine($"[Id {p.Id}] {p.IndeksUczelniany}  {p.TytulNaukowy} {p.Imie} {p.Nazwisko}");
            Console.WriteLine($"    Adres: {p.AdresZamieszkania}");
            Console.WriteLine($"    Gabinet: {p.Gabinet ?? "brak"}");
            Console.WriteLine($"    Ostatnia modyfikacja (Shadow Property): {Konsola.Data(p.OstatniaModyfikacja)}");
            Console.WriteLine($"    Prowadzone kursy: {(p.ProwadzoneKursy.Count == 0 ? "brak" : string.Join("; ", p.ProwadzoneKursy))}");
            Console.WriteLine($"    Dyplomanci: {(p.Dyplomanci.Count == 0 ? "brak" : string.Join("; ", p.Dyplomanci))}");
        }
    }

    private static async Task DodajAsync(ProfesorService service)
    {
        Konsola.Naglowek("Nowy profesor");
        var imie = Konsola.Tekst("Imię");
        var nazwisko = Konsola.Tekst("Nazwisko");
        var tytul = Konsola.Tekst("Tytuł naukowy", "dr inż.");
        var adres = Konsola.Adres();
        var pokoj = Konsola.TekstOpcjonalny("Numer pokoju gabinetu (np. EA-312)");
        var budynek = pokoj is null ? null : Konsola.Tekst("Budynek", "EA");

        var profesor = await service.CreateProfesorAsync(imie, nazwisko, tytul, adres, pokoj, budynek);
        Konsola.Sukces($"Dodano profesora {profesor.IndeksUczelniany} (Id {profesor.Id}).");
    }

    private static async Task EdytujAsync(ProfesorService service)
    {
        var id = Konsola.Liczba("Id profesora do edycji");
        var profesor = await service.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Nie znaleziono profesora o Id = {id}.");

        Konsola.Naglowek($"Edycja {profesor.IndeksUczelniany} (Enter = bez zmian)");
        var imie = Konsola.Tekst("Imię", profesor.Imie);
        var nazwisko = Konsola.Tekst("Nazwisko", profesor.Nazwisko);
        var tytul = Konsola.Tekst("Tytuł naukowy", profesor.TytulNaukowy);
        var adres = Konsola.Adres(profesor.AdresZamieszkania);
        var pokoj = Konsola.TekstOpcjonalny("Numer pokoju gabinetu", profesor.Gabinet?.NumerPokoju);
        var budynek = pokoj is null ? null : Konsola.Tekst("Budynek", profesor.Gabinet?.Budynek);

        await service.UpdateProfesorAsync(id, imie, nazwisko, tytul, adres, pokoj, budynek);
        Konsola.Sukces($"Zaktualizowano profesora {profesor.IndeksUczelniany}; Shadow Property OstatniaModyfikacja ustawiona na bieżący czas.");
    }

    private static async Task UsunAsync(IServiceProvider provider)
    {
        var service = provider.GetRequiredService<ProfesorService>();
        var liczniki = provider.GetRequiredService<LicznikIndeksowService>();

        var klucz = Konsola.Tekst("Id lub indeks uczelniany profesora (np. 3 lub P103)").Trim();
        var wynik = int.TryParse(klucz, out var id)
            ? await service.DeleteProfesorAsync(id)
            : await service.DeleteProfesorByIndeksAsync(klucz);

        var prefix = new string(wynik.IndeksUczelniany.TakeWhile(char.IsLetter).ToArray());
        var licznik = await liczniki.GetAsync(prefix);

        Konsola.Sukces($"Usunięto profesora {wynik.IndeksUczelniany} (gabinet usunięty kaskadowo, " +
                       "u dyplomantów PromotorId = NULL, u kursów ProwadzacyId = NULL).");
        Konsola.Info(wynik.LicznikCofniety
            ? $"Był to ostatnio nadany numer – licznik '{prefix}' cofnięty do {licznik?.AktualnaWartosc}."
            : $"Numer nie był ostatnim nadanym – licznik '{prefix}' bez zmian ({licznik?.AktualnaWartosc}).");
    }
}
