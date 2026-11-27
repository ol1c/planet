using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Dtos;
using University.Domain.Entities;

namespace University.Application.Services;

public class WydzialService
{
    private readonly IUniversityDbContext _context;

    public WydzialService(IUniversityDbContext context)
    {
        _context = context;
    }

    public async Task<Wydzial> CreateAsync(string nazwa)
    {
        var wydzial = new Wydzial { Nazwa = StudentService.WymaganyTekst(nazwa, nameof(nazwa)) };
        _context.Wydzialy.Add(wydzial);
        await _context.SaveChangesAsync();
        return wydzial;
    }

    public async Task<List<WydzialDto>> GetAllAsync()
    {
        return await _context.Wydzialy
            .AsNoTracking()
            .OrderBy(w => w.Nazwa)
            .Select(w => new WydzialDto
            {
                Id = w.Id,
                Nazwa = w.Nazwa,
                LiczbaKursow = w.Kursy.Count()
            })
            .ToListAsync();
    }

    public async Task<Wydzial?> GetByIdAsync(int id)
    {
        return await _context.Wydzialy.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task UpdateAsync(int id, string nazwa)
    {
        var wydzial = await _context.Wydzialy.FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new KeyNotFoundException($"Nie znaleziono wydziału o Id = {id}.");
        wydzial.Nazwa = StudentService.WymaganyTekst(nazwa, nameof(nazwa));
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var wydzial = await _context.Wydzialy.FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new KeyNotFoundException($"Nie znaleziono wydziału o Id = {id}.");

        if (await _context.Kursy.AnyAsync(k => k.WydzialId == id))
            throw new InvalidOperationException("Nie można usunąć wydziału, do którego przypisane są kursy.");

        _context.Wydzialy.Remove(wydzial);
        await _context.SaveChangesAsync();
    }
}
