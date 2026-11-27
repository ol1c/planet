using Microsoft.Extensions.DependencyInjection;
using University.Application.Generators;
using University.Application.Services;

namespace University.UI.Menu;

public class GeneratorMenu : MenuSekcji
{
    public GeneratorMenu(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public override Task UruchomAsync() => WykonajAsync(async provider =>
    {
        Konsola.Naglowek("Generator danych testowych (Bogus)");
        var ustawienia = new UstawieniaGeneratora(
            LiczbaWydzialow: Konsola.Liczba("Liczba wydziałów (max 5)", 3),
            LiczbaProfesorow: Konsola.Liczba("Liczba profesorów", 8),
            LiczbaKursow: Konsola.Liczba("Liczba kursów (max 20)", 12),
            LiczbaStudentowInzynierskich: Konsola.Liczba("Liczba studentów inżynierskich", 10),
            LiczbaStudentowMagisterskich: Konsola.Liczba("Liczba studentów magisterskich", 5));

        var generator = provider.GetRequiredService<UniversityBogusGenerator>();
        var wynik = await generator.GenerujAsync(ustawienia);

        Konsola.Sukces("Wygenerowano dane:");
        Console.WriteLine($"  Wydziały: {wynik.Wydzialy}, profesorowie (z gabinetami): {wynik.Profesorowie}, kursy: {wynik.Kursy} " +
                          $"(powiązań wymagań wstępnych: {wynik.WymaganiaWstepne})");
        Console.WriteLine($"  Studenci inżynierscy: {wynik.StudenciInzynierscy}, magisterscy: {wynik.StudenciMagisterscy}, zapisy: {wynik.Zapisy}");
        Console.WriteLine($"  Nadane indeksy: {string.Join(", ", wynik.NadaneIndeksy)}");

        var liczniki = await provider.GetRequiredService<LicznikIndeksowService>().GetAllAsync();
        Console.WriteLine($"  Stan liczników: {string.Join(", ", liczniki.Select(l => $"{l.Prefix}={l.AktualnaWartosc}"))}");
    });
}
