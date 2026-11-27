using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Dtos;
using University.Domain.Entities;
using University.Domain.ValueObjects;

namespace University.Application.Services;

public class ProfesorService
{
    private readonly IUniversityDbContext _context;
    private readonly IndeksUczelnianyService _indeksService;

    public ProfesorService(IUniversityDbContext context, IndeksUczelnianyService indeksService)
    {
        _context = context;
        _indeksService = indeksService;
    }

    public async Task<Profesor> CreateProfesorAsync(
        string imie, string nazwisko, string tytulNaukowy, Adres adres,
        string? numerPokoju = null, string? budynek = null, string prefix = "P")
    {
        var profesor = new Profesor
        {
            Imie = StudentService.WymaganyTekst(imie, nameof(imie)),
            Nazwisko = StudentService.WymaganyTekst(nazwisko, nameof(nazwisko)),
            TytulNaukowy = StudentService.WymaganyTekst(tytulNaukowy, nameof(tytulNaukowy)),
            AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres)),
            Gabinet = UtworzGabinet(numerPokoju, budynek)
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            profesor.IndeksUczelniany = await _indeksService.PrzydzielKolejnyIndeksAsync(prefix);

            _context.Profesorowie.Add(profesor);
            _context.Entry(profesor).Property(ShadowProperties.OstatniaModyfikacja).CurrentValue = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return profesor;
        }
        catch
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<List<ProfesorDto>> GetAllAsync()
    {
        return await _context.Profesorowie
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(p => p.IndeksUczelniany)
            .Select(p => new ProfesorDto
            {
                Id = p.Id,
                IndeksUczelniany = p.IndeksUczelniany,
                TytulNaukowy = p.TytulNaukowy,
                Imie = p.Imie,
                Nazwisko = p.Nazwisko,
                AdresZamieszkania = p.AdresZamieszkania,
                OstatniaModyfikacja = EF.Property<DateTime>(p, ShadowProperties.OstatniaModyfikacja),
                Gabinet = p.Gabinet != null ? p.Gabinet.NumerPokoju + " (bud. " + p.Gabinet.Budynek + ")" : null,
                ProwadzoneKursy = p.ProwadzoneKursy
                    .OrderBy(k => k.KodKursu)
                    .Select(k => k.KodKursu + " " + k.Nazwa)
                    .ToList(),
                Dyplomanci = p.Dyplomanci
                    .OrderBy(d => d.IndeksUczelniany)
                    .Select(d => d.IndeksUczelniany + " " + d.Imie + " " + d.Nazwisko)
                    .ToList()
            })
            .ToListAsync();
    }

    public async Task<Profesor?> GetByIdAsync(int id)
    {
        return await _context.Profesorowie
            .AsNoTracking()
            .Include(p => p.Gabinet)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Profesor?> GetByIndeksAsync(string indeksUczelniany)
    {
        var indeks = indeksUczelniany.Trim().ToUpperInvariant();
        return await _context.Profesorowie
            .AsNoTracking()
            .Include(p => p.Gabinet)
            .FirstOrDefaultAsync(p => p.IndeksUczelniany == indeks);
    }

    public async Task UpdateProfesorAsync(
        int profesorId, string imie, string nazwisko, string tytulNaukowy, Adres adres,
        string? numerPokoju, string? budynek)
    {
        var profesor = await _context.Profesorowie
            .Include(p => p.Gabinet)
            .FirstOrDefaultAsync(p => p.Id == profesorId)
            ?? throw new KeyNotFoundException($"Nie znaleziono profesora o Id = {profesorId}.");

        profesor.Imie = StudentService.WymaganyTekst(imie, nameof(imie));
        profesor.Nazwisko = StudentService.WymaganyTekst(nazwisko, nameof(nazwisko));
        profesor.TytulNaukowy = StudentService.WymaganyTekst(tytulNaukowy, nameof(tytulNaukowy));
        profesor.AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres));

        var nowyGabinet = UtworzGabinet(numerPokoju, budynek);
        if (nowyGabinet is null)
        {
            if (profesor.Gabinet is not null)
                _context.Gabinety.Remove(profesor.Gabinet);
        }
        else if (profesor.Gabinet is null)
        {
            profesor.Gabinet = nowyGabinet;
        }
        else
        {
            profesor.Gabinet.NumerPokoju = nowyGabinet.NumerPokoju;
            profesor.Gabinet.Budynek = nowyGabinet.Budynek;
        }

        _context.Entry(profesor).Property(ShadowProperties.OstatniaModyfikacja).CurrentValue = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<WynikUsuniecia> DeleteProfesorAsync(int profesorId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var profesor = await _context.Profesorowie.FirstOrDefaultAsync(p => p.Id == profesorId)
                ?? throw new KeyNotFoundException($"Nie znaleziono profesora o Id = {profesorId}.");
            var indeks = profesor.IndeksUczelniany;

            var licznikCofniety = await _indeksService.CofnijLicznikJesliOstatniAsync(indeks);

            _context.Profesorowie.Remove(profesor);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new WynikUsuniecia(indeks, licznikCofniety);
        }
        catch
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<WynikUsuniecia> DeleteProfesorByIndeksAsync(string indeksUczelniany)
    {
        var profesor = await GetByIndeksAsync(indeksUczelniany)
            ?? throw new KeyNotFoundException($"Nie znaleziono profesora o indeksie '{indeksUczelniany}'.");
        return await DeleteProfesorAsync(profesor.Id);
    }

    private static Gabinet? UtworzGabinet(string? numerPokoju, string? budynek)
    {
        if (string.IsNullOrWhiteSpace(numerPokoju) && string.IsNullOrWhiteSpace(budynek))
            return null;

        return new Gabinet
        {
            NumerPokoju = StudentService.WymaganyTekst(numerPokoju ?? string.Empty, nameof(numerPokoju)),
            Budynek = StudentService.WymaganyTekst(budynek ?? string.Empty, nameof(budynek))
        };
    }
}
