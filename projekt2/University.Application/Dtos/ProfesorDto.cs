using University.Domain.ValueObjects;

namespace University.Application.Dtos;

public class ProfesorDto
{
    public int Id { get; init; }
    public string IndeksUczelniany { get; init; } = string.Empty;
    public string TytulNaukowy { get; init; } = string.Empty;
    public string Imie { get; init; } = string.Empty;
    public string Nazwisko { get; init; } = string.Empty;
    public Adres? AdresZamieszkania { get; init; }
    public DateTime OstatniaModyfikacja { get; init; }
    public string? Gabinet { get; init; }
    public List<string> ProwadzoneKursy { get; init; } = new();
    public List<string> Dyplomanci { get; init; } = new();
}
