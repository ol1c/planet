using University.Domain.ValueObjects;

namespace University.Application.Dtos;

public class StudentDto
{
    public int Id { get; init; }
    public string IndeksUczelniany { get; init; } = string.Empty;
    public string Imie { get; init; } = string.Empty;
    public string Nazwisko { get; init; } = string.Empty;
    public int RokStudiow { get; init; }
    public Adres? AdresZamieszkania { get; init; }
    public DateTime OstatniaModyfikacja { get; init; }
    public bool CzyMagistrant { get; init; }
    public string? TematPracyDyplomowej { get; init; }
    public int? PromotorId { get; init; }
    public string? Promotor { get; init; }
}
