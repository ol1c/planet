using University.Domain.ValueObjects;

namespace University.Domain.Entities;

public class Profesor
{
    public int Id { get; set; }
    public string Imie { get; set; } = string.Empty;
    public string Nazwisko { get; set; } = string.Empty;
    public string IndeksUczelniany { get; set; } = string.Empty;
    public string TytulNaukowy { get; set; } = string.Empty;
    public Adres AdresZamieszkania { get; set; } = null!;

    public Gabinet? Gabinet { get; set; }
    public ICollection<Kurs> ProwadzoneKursy { get; set; } = new List<Kurs>();
    public ICollection<StudentStudiowMagisterskich> Dyplomanci { get; set; } = new List<StudentStudiowMagisterskich>();
}
