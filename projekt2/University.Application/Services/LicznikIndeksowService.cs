using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Domain.Entities;

namespace University.Application.Services;

public class LicznikIndeksowService
{
    private readonly IUniversityDbContext _context;

    public LicznikIndeksowService(IUniversityDbContext context)
    {
        _context = context;
    }

    public async Task<LicznikIndeksow> CreateAsync(string prefix, int wartoscPoczatkowa)
    {
        var znormalizowany = IndeksUczelnianyService.NormalizujPrefix(prefix);
        if (wartoscPoczatkowa < 0)
            throw new ArgumentOutOfRangeException(nameof(wartoscPoczatkowa), "Wartość początkowa nie może być ujemna.");

        if (await _context.LicznikiIndeksow.AnyAsync(l => l.Prefix == znormalizowany))
            throw new InvalidOperationException($"Licznik dla prefiksu '{znormalizowany}' już istnieje.");

        var licznik = new LicznikIndeksow { Prefix = znormalizowany, AktualnaWartosc = wartoscPoczatkowa };
        _context.LicznikiIndeksow.Add(licznik);
        await _context.SaveChangesAsync();
        return licznik;
    }

    public async Task<List<LicznikIndeksow>> GetAllAsync()
    {
        return await _context.LicznikiIndeksow
            .AsNoTracking()
            .OrderBy(l => l.Prefix)
            .ToListAsync();
    }

    public async Task<LicznikIndeksow?> GetAsync(string prefix)
    {
        var znormalizowany = IndeksUczelnianyService.NormalizujPrefix(prefix);
        return await _context.LicznikiIndeksow.AsNoTracking().FirstOrDefaultAsync(l => l.Prefix == znormalizowany);
    }

    public async Task UpdateAsync(string prefix, int nowaWartosc)
    {
        var znormalizowany = IndeksUczelnianyService.NormalizujPrefix(prefix);
        if (nowaWartosc < 0)
            throw new ArgumentOutOfRangeException(nameof(nowaWartosc), "Wartość licznika nie może być ujemna.");

        var licznik = await _context.LicznikiIndeksow.FirstOrDefaultAsync(l => l.Prefix == znormalizowany)
            ?? throw new KeyNotFoundException($"Nie znaleziono licznika dla prefiksu '{znormalizowany}'.");

        licznik.AktualnaWartosc = nowaWartosc;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string prefix)
    {
        var znormalizowany = IndeksUczelnianyService.NormalizujPrefix(prefix);
        var licznik = await _context.LicznikiIndeksow.FirstOrDefaultAsync(l => l.Prefix == znormalizowany)
            ?? throw new KeyNotFoundException($"Nie znaleziono licznika dla prefiksu '{znormalizowany}'.");

        _context.LicznikiIndeksow.Remove(licznik);
        await _context.SaveChangesAsync();
    }
}
