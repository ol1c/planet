using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Dtos;

namespace University.Application.Services;

public class UniversityQueryService
{
    private readonly IUniversityDbContext _context;

    public UniversityQueryService(IUniversityDbContext context)
    {
        _context = context;
    }

    public IQueryable<TopProfesorDto> TopProfesorQuery() =>
        _context.Profesorowie
            .TagWith("Zapytanie 1: Profesor z najwieksza laczna liczba studentow")
            .AsNoTracking()
            .Select(p => new TopProfesorDto
            {
                Indeks = p.IndeksUczelniany,
                Imie = p.Imie,
                Nazwisko = p.Nazwisko,
                TytulNaukowy = p.TytulNaukowy,
                LiczbaProwadzonychKursow = p.ProwadzoneKursy.Count(),
                LacznaLiczbaStudentow = p.ProwadzoneKursy.SelectMany(k => k.Zapisy).Count()
            })
            .OrderByDescending(dto => dto.LacznaLiczbaStudentow)
            .ThenByDescending(dto => dto.LiczbaProwadzonychKursow)
            .ThenBy(dto => dto.Indeks);

    public Task<TopProfesorDto?> GetTopProfesorAsync() => TopProfesorQuery().FirstOrDefaultAsync();

    public IQueryable<KursGpaDto> KursyGpaQuery(int wydzialId) =>
        _context.Kursy
            .TagWith("Zapytanie 2: Srednia ocen (GPA) kursow wydzialu")
            .AsNoTracking()
            .Where(k => k.WydzialId == wydzialId)
            .OrderBy(k => k.KodKursu)
            .Select(k => new KursGpaDto
            {
                KodKursu = k.KodKursu,
                NazwaKursu = k.Nazwa,
                LiczbaOcenionychStudentow = k.Zapisy.Count(z => z.Ocena != null),
                SredniaOcen = k.Zapisy
                    .Where(z => z.Ocena != null)
                    .Average(z => z.Ocena) ?? 0.0
            });

    public Task<List<KursGpaDto>> GetKursyGpaAsync(int wydzialId) => KursyGpaQuery(wydzialId).ToListAsync();

    public IQueryable<StudentTrudnoscDto> NajtrudniejszyPlanQuery() =>
        _context.Studenci
            .TagWith("Zapytanie 3: Student z najtrudniejszym planem (unikalne ECTS z prerekwizytami)")
            .AsNoTracking()
            .Select(s => new StudentTrudnoscDto
            {
                Indeks = s.IndeksUczelniany,
                ImieNazwisko = s.Imie + " " + s.Nazwisko,
                LiczbaZapisanychKursow = s.Zapisy.Count(),
                SumaECTSZapisanychKursow = s.Zapisy.Sum(z => z.Kurs.PunktyECTS),
                LacznyKosztECTS = s.Zapisy.Select(z => z.Kurs)
                    .Union(s.Zapisy.SelectMany(z => z.Kurs.WymaganiaWstepne))
                    .Sum(k => (int?)k.PunktyECTS) ?? 0
            })
            .OrderByDescending(dto => dto.LacznyKosztECTS)
            .ThenBy(dto => dto.Indeks);

    public Task<StudentTrudnoscDto?> GetNajtrudniejszyPlanAsync() => NajtrudniejszyPlanQuery().FirstOrDefaultAsync();
}
