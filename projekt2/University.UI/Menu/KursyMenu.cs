using Microsoft.Extensions.DependencyInjection;
using University.Application.Services;

namespace University.UI.Menu;

public class KursyMenu : MenuSekcji
{
    public KursyMenu(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public override async Task UruchomAsync()
    {
        while (true)
        {
            var wybor = Konsola.Wybor("Kursy",
                "1. Lista kursów (z wymaganiami wstępnymi)",
                "2. Dodaj kurs",
                "3. Edytuj kurs",
                "4. Usuń kurs",
                "0. Powrót");

            switch (wybor)
            {
                case "1": await WykonajAsync<KursService>(WyswietlAsync); break;
                case "2": await WykonajAsync<KursService>(DodajAsync); break;
                case "3": await WykonajAsync<KursService>(EdytujAsync); break;
                case "4": await WykonajAsync<KursService>(UsunAsync); break;
                case "0": return;
                default: Konsola.Blad("Nieznana opcja."); break;
            }
        }
    }

    private static async Task WyswietlAsync(KursService service)
    {
        var kursy = await service.GetAllAsync();
        Konsola.Naglowek($"Kursy ({kursy.Count})");
        if (kursy.Count == 0)
        {
            Konsola.Info("Brak kursów w bazie.");
            return;
        }

        foreach (var k in kursy)
        {
            Console.WriteLine($"[Id {k.Id}] {k.KodKursu}  {k.Nazwa}  ({k.PunktyECTS} ECTS)  zapisanych: {k.LiczbaZapisow}");
            Console.WriteLine($"    Wydział: {k.Wydzial}");
            Console.WriteLine($"    Prowadzący: {k.Prowadzacy ?? "brak (ProwadzacyId = NULL)"}");
            Console.WriteLine($"    Wymagania wstępne: {(k.WymaganiaWstepne.Count == 0 ? "brak" : string.Join(", ", k.WymaganiaWstepne))}");
        }
    }

    private static async Task DodajAsync(KursService service)
    {
        Konsola.Naglowek("Nowy kurs");
        var nazwa = Konsola.Tekst("Nazwa");
        var kod = Konsola.Tekst("Kod kursu (np. ETI-EF-01)");
        var ects = Konsola.Liczba("Punkty ECTS", 5);
        var wydzialId = Konsola.Liczba("Id wydziału");
        var prowadzacyId = Konsola.LiczbaOpcjonalna("Id profesora prowadzącego");
        var wymagania = Konsola.ListaLiczb("Wymagania wstępne");

        var kurs = await service.CreateAsync(nazwa, kod, ects, wydzialId, prowadzacyId, wymagania);
        Konsola.Sukces($"Dodano kurs {kurs.KodKursu} (Id {kurs.Id}).");
    }

    private static async Task EdytujAsync(KursService service)
    {
        var id = Konsola.Liczba("Id kursu do edycji");
        var kurs = await service.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Nie znaleziono kursu o Id = {id}.");

        Konsola.Naglowek($"Edycja {kurs.KodKursu} (Enter = bez zmian)");
        var nazwa = Konsola.Tekst("Nazwa", kurs.Nazwa);
        var kod = Konsola.Tekst("Kod kursu", kurs.KodKursu);
        var ects = Konsola.Liczba("Punkty ECTS", kurs.PunktyECTS);
        var wydzialId = Konsola.Liczba("Id wydziału", kurs.WydzialId);
        var prowadzacyId = Konsola.LiczbaOpcjonalna("Id profesora prowadzącego", kurs.ProwadzacyId);
        var wymagania = Konsola.ListaLiczb("Wymagania wstępne", kurs.WymaganiaWstepne.Select(w => w.Id));

        await service.UpdateAsync(id, nazwa, kod, ects, wydzialId, prowadzacyId, wymagania);
        Konsola.Sukces($"Zaktualizowano kurs {kod}.");
    }

    private static async Task UsunAsync(KursService service)
    {
        var id = Konsola.Liczba("Id kursu do usunięcia");
        await service.DeleteAsync(id);
        Konsola.Sukces($"Usunięto kurs o Id {id} (wraz z zapisami i powiązaniami wymagań wstępnych).");
    }
}
