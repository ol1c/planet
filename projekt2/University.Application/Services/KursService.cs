using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Dtos;
using University.Domain.Entities;

namespace University.Application.Services;

public class KursService
{
    private readonly IUniversityDbContext _context;

    public KursService(IUniversityDbContext context)
    {
        _context = context;
    }

    public async Task<Kurs> CreateAsync(
        string nazwa, string kodKursu, int punktyECTS, int wydzialId, int? prowadzacyId,
        IEnumerable<int>? wymaganiaWstepneIds = null)
    {
        await SprawdzPowiazaniaAsync(wydzialId, prowadzacyId);

        var kurs = new Kurs
        {
            Nazwa = StudentService.WymaganyTekst(nazwa, nameof(nazwa)),
            KodKursu = StudentService.WymaganyTekst(kodKursu, nameof(kodKursu)).ToUpperInvariant(),
            PunktyECTS = PoprawnePunkty(punktyECTS),
            WydzialId = wydzialId,
            ProwadzacyId = prowadzacyId
        };

        foreach (var wymaganie in await PobierzKursyAsync(wymaganiaWstepneIds))
            kurs.WymaganiaWstepne.Add(wymaganie);

        _context.Kursy.Add(kurs);
        await _context.SaveChangesAsync();
        return kurs;
    }

    public async Task<List<KursDto>> GetAllAsync()
    {
        return await _context.Kursy
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(k => k.KodKursu)
            .Select(k => new KursDto
            {
                Id = k.Id,
                KodKursu = k.KodKursu,
                Nazwa = k.Nazwa,
                PunktyECTS = k.PunktyECTS,
                WydzialId = k.WydzialId,
                Wydzial = k.Wydzial.Nazwa,
                ProwadzacyId = k.ProwadzacyId,
                Prowadzacy = k.Prowadzacy != null
                    ? k.Prowadzacy.TytulNaukowy + " " + k.Prowadzacy.Imie + " " + k.Prowadzacy.Nazwisko
                    : null,
                LiczbaZapisow = k.Zapisy.Count(),
                WymaganiaWstepne = k.WymaganiaWstepne
                    .OrderBy(w => w.KodKursu)
                    .Select(w => w.KodKursu + " (" + w.PunktyECTS + " ECTS)")
                    .ToList()
            })
            .ToListAsync();
    }

    public async Task<Kurs?> GetByIdAsync(int id)
    {
        return await _context.Kursy
            .AsNoTracking()
            .Include(k => k.WymaganiaWstepne)
            .FirstOrDefaultAsync(k => k.Id == id);
    }

    public async Task UpdateAsync(
        int id, string nazwa, string kodKursu, int punktyECTS, int wydzialId, int? prowadzacyId,
        IEnumerable<int> wymaganiaWstepneIds)
    {
        var kurs = await _context.Kursy
            .Include(k => k.WymaganiaWstepne)
            .FirstOrDefaultAsync(k => k.Id == id)
            ?? throw new KeyNotFoundException($"Nie znaleziono kursu o Id = {id}.");

        await SprawdzPowiazaniaAsync(wydzialId, prowadzacyId);

        var wymagania = await PobierzKursyAsync(wymaganiaWstepneIds);
        if (wymagania.Any(w => w.Id == id))
            throw new InvalidOperationException("Kurs nie może być wymaganiem wstępnym samego siebie.");

        kurs.Nazwa = StudentService.WymaganyTekst(nazwa, nameof(nazwa));
        kurs.KodKursu = StudentService.WymaganyTekst(kodKursu, nameof(kodKursu)).ToUpperInvariant();
        kurs.PunktyECTS = PoprawnePunkty(punktyECTS);
        kurs.WydzialId = wydzialId;
        kurs.ProwadzacyId = prowadzacyId;

        kurs.WymaganiaWstepne.Clear();
        foreach (var wymaganie in wymagania)
            kurs.WymaganiaWstepne.Add(wymaganie);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var kurs = await _context.Kursy
            .Include(k => k.WymaganiaWstepne)
            .Include(k => k.KursyZalezne)
            .FirstOrDefaultAsync(k => k.Id == id)
            ?? throw new KeyNotFoundException($"Nie znaleziono kursu o Id = {id}.");

        kurs.WymaganiaWstepne.Clear();
        kurs.KursyZalezne.Clear();
        _context.Kursy.Remove(kurs);
        await _context.SaveChangesAsync();
    }

    private async Task<List<Kurs>> PobierzKursyAsync(IEnumerable<int>? ids)
    {
        var lista = ids?.Distinct().ToList() ?? new List<int>();
        if (lista.Count == 0)
            return new List<Kurs>();

        var kursy = await _context.Kursy.Where(k => lista.Contains(k.Id)).ToListAsync();
        var brakujace = lista.Except(kursy.Select(k => k.Id)).ToList();
        if (brakujace.Count > 0)
            throw new KeyNotFoundException($"Nie znaleziono kursów o Id: {string.Join(", ", brakujace)}.");

        return kursy;
    }

    private async Task SprawdzPowiazaniaAsync(int wydzialId, int? prowadzacyId)
    {
        if (!await _context.Wydzialy.AnyAsync(w => w.Id == wydzialId))
            throw new KeyNotFoundException($"Nie znaleziono wydziału o Id = {wydzialId}.");

        if (prowadzacyId is not null && !await _context.Profesorowie.AnyAsync(p => p.Id == prowadzacyId))
            throw new KeyNotFoundException($"Nie znaleziono profesora o Id = {prowadzacyId}.");
    }

    private static int PoprawnePunkty(int punktyECTS)
    {
        if (punktyECTS is < 1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(punktyECTS), "Liczba punktów ECTS musi mieścić się w zakresie 1–30.");
        return punktyECTS;
    }
}
