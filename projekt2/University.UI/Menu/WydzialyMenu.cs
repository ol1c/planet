using Microsoft.Extensions.DependencyInjection;
using University.Application.Services;

namespace University.UI.Menu;

public class WydzialyMenu : MenuSekcji
{
    public WydzialyMenu(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public override async Task UruchomAsync()
    {
        while (true)
        {
            var wybor = Konsola.Wybor("Wydziały",
                "1. Lista wydziałów",
                "2. Dodaj wydział",
                "3. Zmień nazwę wydziału",
                "4. Usuń wydział",
                "0. Powrót");

            switch (wybor)
            {
                case "1": await WykonajAsync<WydzialService>(WyswietlAsync); break;
                case "2": await WykonajAsync<WydzialService>(DodajAsync); break;
                case "3": await WykonajAsync<WydzialService>(EdytujAsync); break;
                case "4": await WykonajAsync<WydzialService>(UsunAsync); break;
                case "0": return;
                default: Konsola.Blad("Nieznana opcja."); break;
            }
        }
    }

    private static async Task WyswietlAsync(WydzialService service)
    {
        var wydzialy = await service.GetAllAsync();
        Konsola.Naglowek($"Wydziały ({wydzialy.Count})");
        foreach (var w in wydzialy)
            Console.WriteLine($"[Id {w.Id}] {w.Nazwa}  (kursów: {w.LiczbaKursow})");
        if (wydzialy.Count == 0)
            Konsola.Info("Brak wydziałów w bazie.");
    }

    private static async Task DodajAsync(WydzialService service)
    {
        var wydzial = await service.CreateAsync(Konsola.Tekst("Nazwa wydziału"));
        Konsola.Sukces($"Dodano wydział (Id {wydzial.Id}).");
    }

    private static async Task EdytujAsync(WydzialService service)
    {
        var id = Konsola.Liczba("Id wydziału");
        var wydzial = await service.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Nie znaleziono wydziału o Id = {id}.");
        await service.UpdateAsync(id, Konsola.Tekst("Nowa nazwa", wydzial.Nazwa));
        Konsola.Sukces("Zaktualizowano wydział.");
    }

    private static async Task UsunAsync(WydzialService service)
    {
        var id = Konsola.Liczba("Id wydziału do usunięcia");
        await service.DeleteAsync(id);
        Konsola.Sukces($"Usunięto wydział o Id {id}.");
    }
}
