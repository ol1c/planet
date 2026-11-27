namespace University.Domain.Entities;

public class Enrollment
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int KursId { get; set; }
    public Kurs Kurs { get; set; } = null!;

    public double? Ocena { get; set; }
    public string Semestr { get; set; } = string.Empty;
}
