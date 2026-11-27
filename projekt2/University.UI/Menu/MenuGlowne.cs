using Microsoft.Extensions.DependencyInjection;

namespace University.UI.Menu;

public class MenuGlowne
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MenuGlowne(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task UruchomAsync()
    {
        while (true)
        {
            var wybor = Konsola.Wybor("UniversityCleanArch – menu główne",
                "1. Generator danych testowych (Bogus)",
                "2. Studenci",
                "3. Profesorowie",
                "4. Kursy",
                "5. Wydziały",
                "6. Liczniki indeksów (LicznikiIndeksow)",
                "7. Raporty analityczne (zapytania 1–3)",
                "0. Wyjście");

            switch (wybor)
            {
                case "1": await new GeneratorMenu(_scopeFactory).UruchomAsync(); break;
                case "2": await new StudenciMenu(_scopeFactory).UruchomAsync(); break;
                case "3": await new ProfesorowieMenu(_scopeFactory).UruchomAsync(); break;
                case "4": await new KursyMenu(_scopeFactory).UruchomAsync(); break;
                case "5": await new WydzialyMenu(_scopeFactory).UruchomAsync(); break;
                case "6": await new LicznikiMenu(_scopeFactory).UruchomAsync(); break;
                case "7": await new RaportyMenu(_scopeFactory).UruchomAsync(); break;
                case "0": return;
                default: Konsola.Blad("Nieznana opcja."); break;
            }
        }
    }
}
