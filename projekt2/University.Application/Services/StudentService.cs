using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Dtos;
using University.Domain.Entities;
using University.Domain.ValueObjects;

namespace University.Application.Services;

public class StudentService
{
    private readonly IUniversityDbContext _context;
    private readonly IndeksUczelnianyService _indeksService;

    public StudentService(IUniversityDbContext context, IndeksUczelnianyService indeksService)
    {
        _context = context;
        _indeksService = indeksService;
    }

    public Task<Student> CreateStudentAsync(
        string imie, string nazwisko, int rokStudiow, Adres adres, string prefix = "S")
    {
        var student = new Student
        {
            Imie = WymaganyTekst(imie, nameof(imie)),
            Nazwisko = WymaganyTekst(nazwisko, nameof(nazwisko)),
            RokStudiow = PoprawnyRokStudiow(rokStudiow),
            AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres))
        };

        return DodajZIndeksemAsync(student, prefix);
    }

    public async Task<StudentStudiowMagisterskich> CreateStudentMagisterskiAsync(
        string imie, string nazwisko, int rokStudiow, Adres adres,
        string tematPracyDyplomowej, int? promotorId, string prefix = "S")
    {
        await SprawdzPromotoraAsync(promotorId);

        var magistrant = new StudentStudiowMagisterskich
        {
            Imie = WymaganyTekst(imie, nameof(imie)),
            Nazwisko = WymaganyTekst(nazwisko, nameof(nazwisko)),
            RokStudiow = PoprawnyRokStudiow(rokStudiow),
            AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres)),
            TematPracyDyplomowej = WymaganyTekst(tematPracyDyplomowej, nameof(tematPracyDyplomowej)),
            PromotorId = promotorId
        };

        return await DodajZIndeksemAsync(magistrant, prefix);
    }

    public async Task<List<StudentDto>> GetAllAsync(bool tylkoMagistranci = false)
    {
        IQueryable<Student> zrodlo = _context.Studenci.AsNoTracking();
        if (tylkoMagistranci)
            zrodlo = zrodlo.Where(s => s is StudentStudiowMagisterskich);

        return await zrodlo
            .OrderBy(s => s.IndeksUczelniany)
            .Select(s => new StudentDto
            {
                Id = s.Id,
                IndeksUczelniany = s.IndeksUczelniany,
                Imie = s.Imie,
                Nazwisko = s.Nazwisko,
                RokStudiow = s.RokStudiow,
                AdresZamieszkania = s.AdresZamieszkania,
                OstatniaModyfikacja = EF.Property<DateTime>(s, ShadowProperties.OstatniaModyfikacja),
                CzyMagistrant = s is StudentStudiowMagisterskich,
                TematPracyDyplomowej = s is StudentStudiowMagisterskich
                    ? ((StudentStudiowMagisterskich)s).TematPracyDyplomowej
                    : null,
                PromotorId = s is StudentStudiowMagisterskich
                    ? ((StudentStudiowMagisterskich)s).PromotorId
                    : null,
                Promotor = s is StudentStudiowMagisterskich && ((StudentStudiowMagisterskich)s).Promotor != null
                    ? ((StudentStudiowMagisterskich)s).Promotor!.TytulNaukowy + " "
                      + ((StudentStudiowMagisterskich)s).Promotor!.Imie + " "
                      + ((StudentStudiowMagisterskich)s).Promotor!.Nazwisko + " ("
                      + ((StudentStudiowMagisterskich)s).Promotor!.IndeksUczelniany + ")"
                    : null
            })
            .ToListAsync();
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        return await _context.Studenci.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Student?> GetByIndeksAsync(string indeksUczelniany)
    {
        var indeks = indeksUczelniany.Trim().ToUpperInvariant();
        return await _context.Studenci.AsNoTracking().FirstOrDefaultAsync(s => s.IndeksUczelniany == indeks);
    }

    public async Task UpdateStudentAsync(int studentId, string imie, string nazwisko, int rokStudiow, Adres adres)
    {
        var student = await PobierzSledzonegoAsync(studentId);

        student.Imie = WymaganyTekst(imie, nameof(imie));
        student.Nazwisko = WymaganyTekst(nazwisko, nameof(nazwisko));
        student.RokStudiow = PoprawnyRokStudiow(rokStudiow);
        student.AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres));

        _context.Entry(student).Property(ShadowProperties.OstatniaModyfikacja).CurrentValue = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStudentMagisterskiAsync(
        int studentId, string imie, string nazwisko, int rokStudiow, Adres adres,
        string tematPracyDyplomowej, int? promotorId)
    {
        var student = await PobierzSledzonegoAsync(studentId);
        if (student is not StudentStudiowMagisterskich magistrant)
            throw new InvalidOperationException($"Student {student.IndeksUczelniany} nie jest studentem studiów magisterskich.");

        await SprawdzPromotoraAsync(promotorId);

        magistrant.Imie = WymaganyTekst(imie, nameof(imie));
        magistrant.Nazwisko = WymaganyTekst(nazwisko, nameof(nazwisko));
        magistrant.RokStudiow = PoprawnyRokStudiow(rokStudiow);
        magistrant.AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres));
        magistrant.TematPracyDyplomowej = WymaganyTekst(tematPracyDyplomowej, nameof(tematPracyDyplomowej));
        magistrant.PromotorId = promotorId;

        _context.Entry(magistrant).Property(ShadowProperties.OstatniaModyfikacja).CurrentValue = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<WynikUsuniecia> DeleteStudentAsync(int studentId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var student = await PobierzSledzonegoAsync(studentId);
            var indeks = student.IndeksUczelniany;

            var licznikCofniety = await _indeksService.CofnijLicznikJesliOstatniAsync(indeks);

            _context.Studenci.Remove(student);
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

    public async Task<WynikUsuniecia> DeleteStudentByIndeksAsync(string indeksUczelniany)
    {
        var student = await GetByIndeksAsync(indeksUczelniany)
            ?? throw new KeyNotFoundException($"Nie znaleziono studenta o indeksie '{indeksUczelniany}'.");
        return await DeleteStudentAsync(student.Id);
    }

    private async Task<TStudent> DodajZIndeksemAsync<TStudent>(TStudent student, string prefix) where TStudent : Student
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            student.IndeksUczelniany = await _indeksService.PrzydzielKolejnyIndeksAsync(prefix);

            _context.Studenci.Add(student);
            _context.Entry(student).Property(ShadowProperties.OstatniaModyfikacja).CurrentValue = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return student;
        }
        catch
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<Student> PobierzSledzonegoAsync(int studentId)
    {
        return await _context.Studenci.FirstOrDefaultAsync(s => s.Id == studentId)
            ?? throw new KeyNotFoundException($"Nie znaleziono studenta o Id = {studentId}.");
    }

    private async Task SprawdzPromotoraAsync(int? promotorId)
    {
        if (promotorId is null)
            return;

        if (!await _context.Profesorowie.AnyAsync(p => p.Id == promotorId))
            throw new KeyNotFoundException($"Nie znaleziono profesora (promotora) o Id = {promotorId}.");
    }

    private static int PoprawnyRokStudiow(int rokStudiow)
    {
        if (rokStudiow is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(rokStudiow), "Rok studiów musi mieścić się w zakresie 1–7.");
        return rokStudiow;
    }

    internal static string WymaganyTekst(string wartosc, string nazwaParametru)
    {
        if (string.IsNullOrWhiteSpace(wartosc))
            throw new ArgumentException($"Pole '{nazwaParametru}' nie może być puste.", nazwaParametru);
        return wartosc.Trim();
    }
}
