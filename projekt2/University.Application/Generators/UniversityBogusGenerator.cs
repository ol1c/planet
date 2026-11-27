using Bogus;
using University.Application.Abstractions;
using University.Application.Services;
using University.Domain.Entities;
using University.Domain.ValueObjects;

namespace University.Application.Generators;

public record UstawieniaGeneratora(
    int LiczbaWydzialow = 3,
    int LiczbaProfesorow = 8,
    int LiczbaKursow = 12,
    int LiczbaStudentowInzynierskich = 10,
    int LiczbaStudentowMagisterskich = 5,
    int? Ziarno = null);

public record PodsumowanieGeneratora(
    int Wydzialy,
    int Profesorowie,
    int Kursy,
    int WymaganiaWstepne,
    int StudenciInzynierscy,
    int StudenciMagisterscy,
    int Zapisy,
    IReadOnlyList<string> NadaneIndeksy);

public class UniversityBogusGenerator
{
    private static readonly string[] TytulyNaukowe =
        { "dr", "dr inż.", "dr hab.", "dr hab. inż.", "prof. dr hab.", "prof. dr hab. inż." };

    private static readonly (string Nazwa, string Skrot)[] Wydzialy =
    {
        ("Wydział Elektroniki, Telekomunikacji i Informatyki", "ETI"),
        ("Wydział Zarządzania i Ekonomii", "ZIE"),
        ("Wydział Fizyki Technicznej i Matematyki Stosowanej", "FTMS"),
        ("Wydział Mechaniczny i Okrętownictwa", "WIMIO"),
        ("Wydział Chemiczny", "CHEM")
    };

    private static readonly string[] NazwyKursow =
    {
        "Analiza matematyczna", "Algebra liniowa", "Podstawy programowania", "Programowanie obiektowe",
        "Algorytmy i struktury danych", "Bazy danych", "Systemy operacyjne", "Sieci komputerowe",
        "Inżynieria oprogramowania", "Programowanie aplikacji lokalnych", "Fizyka", "Statystyka",
        "Metody numeryczne", "Grafika komputerowa", "Sztuczna inteligencja", "Bezpieczeństwo systemów",
        "Ekonomia", "Zarządzanie projektami", "Technologie internetowe", "Systemy rozproszone"
    };

    private static readonly string[] RodzajePrac =
        { "Analiza", "Projekt i implementacja", "Optymalizacja", "Zastosowanie", "Badanie wydajności", "Porównanie metod" };

    private static readonly string[] ObszaryPrac =
    {
        "systemów rozproszonych", "sieci neuronowych w rozpoznawaniu obrazów", "baz danych NoSQL",
        "algorytmów genetycznych", "mikroserwisów w chmurze obliczeniowej", "protokołów Internetu Rzeczy",
        "mapowania obiektowo-relacyjnego", "metod kompresji danych", "systemów rekomendacyjnych"
    };

    private static readonly double[] Oceny = { 2.0, 3.0, 3.5, 4.0, 4.5, 5.0 };
    private static readonly string[] Semestry = { "2025/26-L", "2026/27-Z" };

    private readonly IUniversityDbContext _context;
    private readonly StudentService _studentService;
    private readonly ProfesorService _profesorService;
    private readonly WydzialService _wydzialService;
    private readonly KursService _kursService;

    public UniversityBogusGenerator(
        IUniversityDbContext context,
        StudentService studentService,
        ProfesorService profesorService,
        WydzialService wydzialService,
        KursService kursService)
    {
        _context = context;
        _studentService = studentService;
        _profesorService = profesorService;
        _wydzialService = wydzialService;
        _kursService = kursService;
    }

    public async Task<PodsumowanieGeneratora> GenerujAsync(UstawieniaGeneratora? ustawienia = null)
    {
        ustawienia ??= new UstawieniaGeneratora();

        var faker = new Faker("pl");
        if (ustawienia.Ziarno is int ziarno)
            faker.Random = new Randomizer(ziarno);

        var adresFaker = new Faker<Adres>("pl")
            .UseSeed(ustawienia.Ziarno ?? Environment.TickCount)
            .CustomInstantiator(f => new Adres(
                ulica: f.Address.StreetAddress(),
                miasto: f.Address.City(),
                kodPocztowy: f.Address.ZipCode("##-###")));

        var nadaneIndeksy = new List<string>();

        var wydzialy = new List<(Wydzial Wydzial, string Skrot)>();
        foreach (var (nazwa, skrot) in faker.PickRandom(Wydzialy, Math.Min(ustawienia.LiczbaWydzialow, Wydzialy.Length)))
            wydzialy.Add((await _wydzialService.CreateAsync(nazwa), skrot));

        var profesorowie = new List<Profesor>();
        for (var i = 0; i < ustawienia.LiczbaProfesorow; i++)
        {
            var budynek = faker.PickRandom("EA", "WETI-A", "WETI-B", "GG", "NE");
            var profesor = await _profesorService.CreateProfesorAsync(
                imie: faker.Name.FirstName(),
                nazwisko: faker.Name.LastName(),
                tytulNaukowy: faker.PickRandom(TytulyNaukowe),
                adres: adresFaker.Generate(),
                numerPokoju: $"{budynek}-{faker.Random.Int(100, 699)}",
                budynek: budynek);
            profesorowie.Add(profesor);
            nadaneIndeksy.Add(profesor.IndeksUczelniany);
        }

        var kursy = new List<Kurs>();
        var liczbaWymagan = 0;
        var licznikKodow = new Dictionary<string, int>();
        foreach (var nazwaKursu in faker.PickRandom(NazwyKursow, Math.Min(ustawienia.LiczbaKursow, NazwyKursow.Length)))
        {
            var (wydzial, skrot) = faker.PickRandom(wydzialy);
            licznikKodow[skrot] = licznikKodow.GetValueOrDefault(skrot) + 1;

            var wymagania = kursy.Count == 0
                ? new List<int>()
                : faker.PickRandom(kursy, faker.Random.Int(0, Math.Min(2, kursy.Count))).Select(k => k.Id).ToList();
            liczbaWymagan += wymagania.Count;

            var kurs = await _kursService.CreateAsync(
                nazwa: nazwaKursu,
                kodKursu: $"{skrot}-{faker.Random.Int(10, 99)}-{licznikKodow[skrot]:D2}",
                punktyECTS: faker.Random.Int(2, 8),
                wydzialId: wydzial.Id,
                prowadzacyId: profesorowie.Count > 0 ? faker.PickRandom(profesorowie).Id : null,
                wymaganiaWstepneIds: wymagania);
            kursy.Add(kurs);
        }

        var studenci = new List<Student>();
        for (var i = 0; i < ustawienia.LiczbaStudentowInzynierskich; i++)
        {
            var student = await _studentService.CreateStudentAsync(
                imie: faker.Name.FirstName(),
                nazwisko: faker.Name.LastName(),
                rokStudiow: faker.Random.Int(1, 4),
                adres: adresFaker.Generate());
            studenci.Add(student);
            nadaneIndeksy.Add(student.IndeksUczelniany);
        }

        for (var i = 0; i < ustawienia.LiczbaStudentowMagisterskich; i++)
        {
            var magistrant = await _studentService.CreateStudentMagisterskiAsync(
                imie: faker.Name.FirstName(),
                nazwisko: faker.Name.LastName(),
                rokStudiow: faker.Random.Int(1, 2),
                adres: adresFaker.Generate(),
                tematPracyDyplomowej: $"{faker.PickRandom(RodzajePrac)} {faker.PickRandom(ObszaryPrac)}",
                promotorId: profesorowie.Count > 0 ? faker.PickRandom(profesorowie).Id : null);
            studenci.Add(magistrant);
            nadaneIndeksy.Add(magistrant.IndeksUczelniany);
        }

        var liczbaZapisow = 0;
        if (kursy.Count > 0)
        {
            foreach (var student in studenci)
            {
                var wybraneKursy = faker.PickRandom(kursy, faker.Random.Int(Math.Min(3, kursy.Count), Math.Min(5, kursy.Count)));
                foreach (var kurs in wybraneKursy)
                {
                    _context.Zapisy.Add(new Enrollment
                    {
                        StudentId = student.Id,
                        KursId = kurs.Id,
                        Semestr = faker.PickRandom(Semestry),
                        Ocena = faker.Random.Bool(0.7f) ? faker.PickRandom(Oceny) : null
                    });
                    liczbaZapisow++;
                }
            }
            await _context.SaveChangesAsync();
        }

        _context.ChangeTracker.Clear();

        return new PodsumowanieGeneratora(
            wydzialy.Count,
            profesorowie.Count,
            kursy.Count,
            liczbaWymagan,
            ustawienia.LiczbaStudentowInzynierskich,
            ustawienia.LiczbaStudentowMagisterskich,
            liczbaZapisow,
            nadaneIndeksy);
    }
}
