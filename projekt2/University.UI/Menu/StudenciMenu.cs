using Microsoft.Extensions.DependencyInjection;
using University.Application.Dtos;
using University.Application.Services;
using University.Domain.Entities;

namespace University.UI.Menu;

public class StudenciMenu : MenuSekcji
{
    public StudenciMenu(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public override async Task UruchomAsync()
    {
        while (true)
        {
            var wybor = Konsola.Wybor("Studenci",
                "1. Lista wszystkich studentów",
                "2. Lista studentów studiów magisterskich (promotorzy)",
                "3. Dodaj studenta",
                "4. Dodaj studenta studiów magisterskich",
                "5. Edytuj studenta",
                "6. Usuń studenta (Id lub indeks)",
                "0. Powrót");

            switch (wybor)
            {
                case "1": await WykonajAsync<StudentService>(s => WyswietlAsync(s, false)); break;
                case "2": await WykonajAsync<StudentService>(s => WyswietlAsync(s, true)); break;
                case "3": await WykonajAsync<StudentService>(DodajAsync); break;
                case "4": await WykonajAsync<StudentService>(DodajMagistrantaAsync); break;
                case "5": await WykonajAsync<StudentService>(EdytujAsync); break;
                case "6": await WykonajAsync(UsunAsync); break;
                case "0": return;
                default: Konsola.Blad("Nieznana opcja."); break;
            }
        }
    }

    private static async Task WyswietlAsync(StudentService service, bool tylkoMagistranci)
    {
        var studenci = await service.GetAllAsync(tylkoMagistranci);
        Konsola.Naglowek(tylkoMagistranci ? $"Studenci magisterscy ({studenci.Count})" : $"Studenci ({studenci.Count})");
        if (studenci.Count == 0)
        {
            Konsola.Info("Brak studentów w bazie.");
            return;
        }

        foreach (var s in studenci)
            WypiszStudenta(s);
    }

    private static void WypiszStudenta(StudentDto s)
    {
        Console.WriteLine($"[Id {s.Id}] {s.IndeksUczelniany}  {s.Imie} {s.Nazwisko}  rok {s.RokStudiow}  " +
                          $"{(s.CzyMagistrant ? "(magisterski)" : "(inżynierski)")}");
        Console.WriteLine($"    Adres: {s.AdresZamieszkania}");
        Console.WriteLine($"    Ostatnia modyfikacja (Shadow Property): {Konsola.Data(s.OstatniaModyfikacja)}");
        if (s.CzyMagistrant)
        {
            Console.WriteLine($"    Temat pracy: {s.TematPracyDyplomowej}");
            Console.WriteLine($"    Promotor: {s.Promotor ?? "Brak promotora (PromotorId = NULL)"}");
        }
    }

    private static async Task DodajAsync(StudentService service)
    {
        Konsola.Naglowek("Nowy student");
        var imie = Konsola.Tekst("Imię");
        var nazwisko = Konsola.Tekst("Nazwisko");
        var rok = Konsola.Liczba("Rok studiów", 1);
        var adres = Konsola.Adres();
        var prefix = Konsola.Tekst("Prefiks licznika indeksów", "S");

        var student = await service.CreateStudentAsync(imie, nazwisko, rok, adres, prefix);
        Konsola.Sukces($"Dodano studenta {student.IndeksUczelniany} (Id {student.Id}).");
    }

    private static async Task DodajMagistrantaAsync(StudentService service)
    {
        Konsola.Naglowek("Nowy student studiów magisterskich");
        var imie = Konsola.Tekst("Imię");
        var nazwisko = Konsola.Tekst("Nazwisko");
        var rok = Konsola.Liczba("Rok studiów", 1);
        var adres = Konsola.Adres();
        var temat = Konsola.Tekst("Temat pracy dyplomowej");
        var promotorId = Konsola.LiczbaOpcjonalna("Id promotora (profesora)");

        var student = await service.CreateStudentMagisterskiAsync(imie, nazwisko, rok, adres, temat, promotorId);
        Konsola.Sukces($"Dodano magistranta {student.IndeksUczelniany} (Id {student.Id}).");
    }

    private static async Task EdytujAsync(StudentService service)
    {
        var id = Konsola.Liczba("Id studenta do edycji");
        var student = await service.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Nie znaleziono studenta o Id = {id}.");

        Konsola.Naglowek($"Edycja {student.IndeksUczelniany} (Enter = bez zmian)");
        var imie = Konsola.Tekst("Imię", student.Imie);
        var nazwisko = Konsola.Tekst("Nazwisko", student.Nazwisko);
        var rok = Konsola.Liczba("Rok studiów", student.RokStudiow);
        var adres = Konsola.Adres(student.AdresZamieszkania);

        if (student is StudentStudiowMagisterskich magistrant)
        {
            var temat = Konsola.Tekst("Temat pracy dyplomowej", magistrant.TematPracyDyplomowej);
            var promotorId = Konsola.LiczbaOpcjonalna("Id promotora", magistrant.PromotorId);
            await service.UpdateStudentMagisterskiAsync(id, imie, nazwisko, rok, adres, temat, promotorId);
        }
        else
        {
            await service.UpdateStudentAsync(id, imie, nazwisko, rok, adres);
        }

        Konsola.Sukces($"Zaktualizowano studenta {student.IndeksUczelniany}; Shadow Property OstatniaModyfikacja ustawiona na bieżący czas.");
    }

    private static async Task UsunAsync(IServiceProvider provider)
    {
        var service = provider.GetRequiredService<StudentService>();
        var liczniki = provider.GetRequiredService<LicznikIndeksowService>();

        var klucz = Konsola.Tekst("Id lub indeks uczelniany studenta (np. 7 lub S1005)").Trim();
        var wynik = int.TryParse(klucz, out var id)
            ? await service.DeleteStudentAsync(id)
            : await service.DeleteStudentByIndeksAsync(klucz);

        var prefix = new string(wynik.IndeksUczelniany.TakeWhile(char.IsLetter).ToArray());
        var licznik = await liczniki.GetAsync(prefix);

        Konsola.Sukces($"Usunięto studenta {wynik.IndeksUczelniany}.");
        Konsola.Info(wynik.LicznikCofniety
            ? $"Był to ostatnio nadany numer – licznik '{prefix}' cofnięty do {licznik?.AktualnaWartosc}."
            : $"Numer nie był ostatnim nadanym – licznik '{prefix}' bez zmian ({licznik?.AktualnaWartosc}).");
    }
}
