namespace University.Domain.Entities;

public class Wydzial
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = string.Empty;

    public ICollection<Kurs> Kursy { get; set; } = new List<Kurs>();
}
