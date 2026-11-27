using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using University.Application.Services;

namespace University.UI.Menu;

public class RaportyMenu : MenuSekcji
{
    public RaportyMenu(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public override async Task UruchomAsync()
    {
        while (true)
        {
            var wybor = Konsola.Wybor("Raporty analityczne",
                "1. Zapytanie 1 – profesor z największą łączną liczbą studentów",
                "2. Zapytanie 2 – średnia ocen (GPA) kursów wybranego wydziału",
                "3. Zapytanie 3 – student z najtrudniejszym planem (unikalne ECTS z prerekwizytami)",
                "4. Pokaż SQL wygenerowany dla zapytań 1–3 (ToQueryString)",
                "0. Powrót");

            switch (wybor)
            {
                case "1": await WykonajAsync<UniversityQueryService>(Zapytanie1Async); break;
                case "2": await WykonajAsync(Zapytanie2Async); break;
                case "3": await WykonajAsync<UniversityQueryService>(Zapytanie3Async); break;
                case "4": await WykonajAsync<UniversityQueryService>(PokazSqlAsync); break;
                case "0": return;
                default: Konsola.Blad("Nieznana opcja."); break;
            }
        }
    }

    private static async Task Zapytanie1Async(UniversityQueryService service)
    {
        var wynik = await service.GetTopProfesorAsync();
        Konsola.Naglowek("Zapytanie 1: Profesor z największą łączną liczbą studentów");
        if (wynik is null)
        {
            Konsola.Info("Brak profesorów w bazie.");
            return;
        }

        Console.WriteLine($"  Indeks:                     {wynik.Indeks}");
        Console.WriteLine($"  Profesor:                   {wynik.TytulNaukowy} {wynik.Imie} {wynik.Nazwisko}");
        Console.WriteLine($"  Liczba prowadzonych kursów: {wynik.LiczbaProwadzonychKursow}");
        Console.WriteLine($"  Łączna liczba zapisanych:   {wynik.LacznaLiczbaStudentow}");
    }

    private static async Task Zapytanie2Async(IServiceProvider provider)
    {
        var wydzialy = await provider.GetRequiredService<WydzialService>().GetAllAsync();
        if (wydzialy.Count == 0)
        {
            Konsola.Info("Brak wydziałów w bazie.");
            return;
        }

        Konsola.Naglowek("Wybierz wydział");
        for (var i = 0; i < wydzialy.Count; i++)
            Console.WriteLine($"  {i + 1}. {wydzialy[i].Nazwa} (kursów: {wydzialy[i].LiczbaKursow})");

        var numer = Konsola.Liczba("Numer wydziału", 1);
        if (numer < 1 || numer > wydzialy.Count)
            throw new ArgumentOutOfRangeException(nameof(numer), "Wybrano numer spoza listy.");
        var wydzial = wydzialy[numer - 1];

        var wynik = await provider.GetRequiredService<UniversityQueryService>().GetKursyGpaAsync(wydzial.Id);
        Konsola.Naglowek($"Zapytanie 2: GPA kursów – {wydzial.Nazwa}");
        if (wynik.Count == 0)
        {
            Konsola.Info("Wydział nie ma kursów.");
            return;
        }

        Console.WriteLine($"  {"Kod",-16}{"Kurs",-38}{"Ocenionych",11}{"GPA",8}");
        foreach (var k in wynik)
            Console.WriteLine($"  {k.KodKursu,-16}{Skroc(k.NazwaKursu, 36),-38}{k.LiczbaOcenionychStudentow,11}{k.SredniaOcen,8:F2}");
    }

    private static async Task Zapytanie3Async(UniversityQueryService service)
    {
        var wynik = await service.GetNajtrudniejszyPlanAsync();
        Konsola.Naglowek("Zapytanie 3: Student z najtrudniejszym planem zajęć");
        if (wynik is null)
        {
            Konsola.Info("Brak studentów w bazie.");
            return;
        }

        Console.WriteLine($"  Indeks:                                   {wynik.Indeks}");
        Console.WriteLine($"  Student:                                  {wynik.ImieNazwisko}");
        Console.WriteLine($"  Liczba zapisanych kursów:                 {wynik.LiczbaZapisanychKursow}");
        Console.WriteLine($"  ECTS samych zapisanych kursów:            {wynik.SumaECTSZapisanychKursow}");
        Console.WriteLine($"  Łączny koszt ECTS (kursy + prerekwizyty,");
        Console.WriteLine($"  każdy kurs liczony tylko raz):            {wynik.LacznyKosztECTS}");
    }

    private static Task PokazSqlAsync(UniversityQueryService service)
    {
        Konsola.Naglowek("SQL – Zapytanie 1");
        Console.WriteLine(service.TopProfesorQuery().Take(1).ToQueryString());
        Konsola.Naglowek("SQL – Zapytanie 2 (wydzialId = parametr)");
        Console.WriteLine(service.KursyGpaQuery(1).ToQueryString());
        Konsola.Naglowek("SQL – Zapytanie 3");
        Console.WriteLine(service.NajtrudniejszyPlanQuery().Take(1).ToQueryString());
        return Task.CompletedTask;
    }

    private static string Skroc(string tekst, int dlugosc) =>
        tekst.Length <= dlugosc ? tekst : tekst[..(dlugosc - 1)] + "…";
}
