using University.Domain.ValueObjects;

namespace University.Domain.Entities;

public class Student
{
    public int Id { get; set; }
    public string Imie { get; set; } = string.Empty;
    public string Nazwisko { get; set; } = string.Empty;
    public string IndeksUczelniany { get; set; } = string.Empty;
    public int RokStudiow { get; set; }
    public Adres AdresZamieszkania { get; set; } = null!;

    public ICollection<Enrollment> Zapisy { get; set; } = new List<Enrollment>();
}
