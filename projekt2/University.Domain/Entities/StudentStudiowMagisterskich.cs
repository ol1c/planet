namespace University.Domain.Entities;

public class StudentStudiowMagisterskich : Student
{
    public string TematPracyDyplomowej { get; set; } = string.Empty;

    public int? PromotorId { get; set; }
    public Profesor? Promotor { get; set; }
}
