namespace University.Application.Dtos;

public class TopProfesorDto
{
    public string Indeks { get; init; } = string.Empty;
    public string Imie { get; init; } = string.Empty;
    public string Nazwisko { get; init; } = string.Empty;
    public string TytulNaukowy { get; init; } = string.Empty;
    public int LiczbaProwadzonychKursow { get; init; }
    public int LacznaLiczbaStudentow { get; init; }
}

public class KursGpaDto
{
    public string KodKursu { get; init; } = string.Empty;
    public string NazwaKursu { get; init; } = string.Empty;
    public int LiczbaOcenionychStudentow { get; init; }
    public double SredniaOcen { get; init; }
}

public class StudentTrudnoscDto
{
    public string Indeks { get; init; } = string.Empty;
    public string ImieNazwisko { get; init; } = string.Empty;
    public int LiczbaZapisanychKursow { get; init; }
    public int SumaECTSZapisanychKursow { get; init; }
    public int LacznyKosztECTS { get; init; }
}
