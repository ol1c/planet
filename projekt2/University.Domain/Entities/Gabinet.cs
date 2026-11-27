namespace University.Domain.Entities;

public class Gabinet
{
    public int Id { get; set; }
    public string NumerPokoju { get; set; } = string.Empty;
    public string Budynek { get; set; } = string.Empty;

    public int ProfesorId { get; set; }
    public Profesor Profesor { get; set; } = null!;
}
