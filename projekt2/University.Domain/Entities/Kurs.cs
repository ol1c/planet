namespace University.Domain.Entities;

public class Kurs
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = string.Empty;
    public string KodKursu { get; set; } = string.Empty;
    public int PunktyECTS { get; set; }

    public int WydzialId { get; set; }
    public Wydzial Wydzial { get; set; } = null!;

    public int? ProwadzacyId { get; set; }
    public Profesor? Prowadzacy { get; set; }

    public ICollection<Enrollment> Zapisy { get; set; } = new List<Enrollment>();

    public ICollection<Kurs> WymaganiaWstepne { get; set; } = new HashSet<Kurs>();
    public ICollection<Kurs> KursyZalezne { get; set; } = new HashSet<Kurs>();
}
