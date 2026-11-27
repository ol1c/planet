# Programowanie Aplikacji Lokalnych

**Rok akademicki:** 2026/27

## Instrukcja laboratoryjna: Mapowanie Obiektowo-Relacyjne (ORM) Entity Framework Core (.NET 8/9/10), DDD, Związki, Shadow Properties, Migracje i Optymalizacja Zapytań

**Prowadzący:** Tomasz Goluch  
**Wersja:** 1.1

------------------------------------------------------------------------

### 1. Wprowadzenie: ORM, Domain-Driven Design (DDD), Code-First i Czysta Architektura

**Cel:** Zapoznanie z mapowaniem obiektowo-relacyjnym (ORM), jego rolą we wspieraniu podejścia Domain-Driven Design (DDD) poprzez realizację podejścia Code-First oraz separacją warstw w Czystej Architekturze (*Clean Architecture*).

Zgodnie z zasadami **Domain-Driven Design (DDD)**, projektując aplikację o złożonej logice biznesowej, w pierwszej kolejności skupiamy się na modelu domeny – klasach POCO, encjach i obiektach wartości realizujących reguły biznesowe. Dopiero w kolejnym kroku, za pomocą mappera obiektowo-relacyjnego (**ORM – Object-Relational Mapper**), rozwiązujemy problem trwałego zapisu (*persystencji*) stanu tych obiektów.

ORM niweluje tzw. **niezgodność impedancji obiektowo-relacyjnej** (*Object-Relational Impedance Mismatch*) – fundamentalne różnice między paradygmatem obiektowym (grafy obiektów, dziedziczenie, hermetyzacja, polimorfizm) a modelem relacyjnym E. F. Codda (relacje/tabele, krotki, klucze obce, brak dziedziczenia). Pozwala to modelowi domeny zachować właściwość **Persistence Ignorance** (klasy domenowe są zwykłymi klasami C# – *POCO*, które nie wiedzą, w jaki sposób i w jakiej bazie danych są utrwalane).

- **Podejście Code-First:** Naturalna konsekwencja DDD – schemat relacyjnej bazy danych (tabele, kolumny, indeksy, więzy integralności) jest generowany i aktualizowany automatycznie na podstawie kodu klas domenowych C# oraz konfiguracji mapowania za pomocą mechanizmu **Migracji**.
- **Podejście Database-First (Reverse Engineering):** Odwrotny kierunek pracy, w którym narzędzia ORM generują klasy C# oraz kontekst danych na podstawie istniejącej już bazy danych (komenda `dotnet ef dbcontext scaffold`).

#### Podział odpowiedzialności w Czystej Architekturze (Clean Architecture)

W nowoczesnych systemach .NET opartych o EF Core separujemy model domenowy od szczegółów infrastruktury bazodanowej poprzez podział solucji na cztery współpracujące warstwy (projekty):

``` text
┌─────────────────────────────────────────────────────────────────────────────┐
│                   WARSTWA PREZENTACJI (UI / Web / Console)                  │
│   Widoki (WPF / Terminal.Gui / ASP.NET Core), Punkt startowy (Program.cs),  │
│   Konfiguracja kontenera Dependency Injection (AddDbContext)                │
└──────────────────────┬──────────────────────────────────┬───────────────────┘
                       │ wywołuje                         │ rejestruje DI
                       ▼                                  ▼
┌────────────────────────────────────────┐  ┌─────────────────────────────────┐
│      WARSTWA APLIKACJI (Application)   │  │ WARSTWA INFRASTRUKTURY          │
│  Przypadki użycia (Use Cases), Serwisy │  │ (Infrastructure)                │
│  biznesowe, DTO, Generatory danych     │◄─┤ Implementacja DbContext,        │
│  (Bogus), Obsługa transakcji i zapis   │  │ Konfiguracje Fluent API, Shadow │
│  Shadow Properties w ChangeTrackerze   │  │ Properties, Migracje, Provider  │
└──────────────────────┬─────────────────┘  └─────────────┬───────────────────┘
                       │ zależy od                        │ zależy od
                       ▼                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        WARSTWA DOMENY (Domain / Core)                       │
│   Czyste encje biznesowe POCO (Student, Kurs, Profesor), Obiekty Wartości   │
│   (record Adres, Money), Wyjątki domenowe (Brak zależności od bazy SQL!)    │
└─────────────────────────────────────────────────────────────────────────────┘Kopiuj
```

> **🧠 Złota zasada zależności (Dependency Rule):**  
> Projekt **`Domain`** znajduje się w samym centrum architektury i **nie posiada referencji do żadnych innych projektów ani dostawców baz danych** (takich jak `Microsoft.EntityFrameworkCore.SqlServer`). Cała konfiguracja specyficzna dla relacyjnej bazy danych (`DbContext`, `OnModelCreating`, *Shadow Properties*, `Migrations`) trafia do projektu **`Infrastructure`**.

------------------------------------------------------------------------

### 2. Instalacja biblioteki, dostawców i narzędzi CLI / PMC

**Cel:** Instalacja pakietów NuGet dla Entity Framework Core, wybór odpowiedniego dostawcy bazy danych (*Database Provider*) oraz opanowanie narzędzi wiersza poleceń (.NET CLI oraz Package Manager Console).

#### 1. Wersje EF Core i kompatybilność z platformą .NET

Wersja pakietów `Microsoft.EntityFrameworkCore.*` powinna odpowiadać docelowej wersji SDK `.NET` używanej w projekcie. Aktualne zestawienie wydań publikowane jest w oficjalnej dokumentacji: [What's New in EF Core](https://learn.microsoft.com/en-us/ef/core/what-is-new/).

| Wersja EF Core   | Wymagany Target Framework | Typ wydania                 | Wsparcie techniczne do |
|:-----------------|:--------------------------|:----------------------------|:-----------------------|
| **EF Core 10.0** | **.NET 10.0**             | **LTS (Long Term Support)** | **Listopad 2028**      |
| **EF Core 9.0**  | **.NET 8.0 / .NET 9.0**   | STS (Standard Term Support) | Listopad 2026          |
| **EF Core 8.0**  | **.NET 8.0**              | **LTS (Long Term Support)** | **Listopad 2026**      |

*(Na zajęciach laboratoryjnych wspierane są projekty celujące w **.NET 8.0 LTS**, **.NET 9.0** oraz **.NET 10.0 LTS**).*

#### 2. Wymagane pakiety NuGet

Decydując się na wykorzystanie Entity Framework Core w projekcie, instalujemy co najmniej dwa podstawowe pakiety oraz pakiety narzędziowe czasu projektowania (*design-time*):

1.  **`Microsoft.EntityFrameworkCore`** – rdzeń ORM (śledzenie zmian, budowanie modelu, LINQ translator).
2.  **Pakiet dostawcy bazy danych (Provider)** – tłumaczy abstrakcyjne drzewa wyrażeń EF Core na dialekt SQL konkretnego silnika bazodanowego:
    - `Microsoft.EntityFrameworkCore.SqlServer` (Microsoft SQL Server / LocalDB),
    - `Microsoft.EntityFrameworkCore.Sqlite` (lekka baza plikowa SQLite),
    - `Npgsql.EntityFrameworkCore.PostgreSQL` (PostgreSQL),
    - `Pomelo.EntityFrameworkCore.MySql` (MySQL / MariaDB).
3.  **Narzędzia generowania migracji:**
    - `Microsoft.EntityFrameworkCore.Tools` (dla konsoli Menedżera Pakietów w Visual Studio),
    - `Microsoft.EntityFrameworkCore.Design` (wymagany w projekcie startowym podczas korzystania z narzędzia `.NET CLI` – `dotnet ef`).

#### 3. Instalacja pakietów i narzędzi (Wieloplatformowe .NET CLI vs Visual Studio PMC)

**Wariant A: Wieloplatformowy wiersz poleceń (.NET CLI – VS Code, Rider, Terminal)**

``` bash
# Instalacja pakietów w projekcie Infrastructure
dotnet add MyProject.Infrastructure package Microsoft.EntityFrameworkCore
dotnet add MyProject.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer

# Instalacja pakietu Design w projekcie startowym (UI)
dotnet add MyProject.UI package Microsoft.EntityFrameworkCore.Design

# Jednorazowa globalna instalacja narzędzia dotnet-ef (lub jego aktualizacja)
dotnet tool install --global dotnet-ef
# Aktualizacja do najnowszej wersji:
dotnet tool update --global dotnet-efKopiuj
```

**Wariant B: Konsola Menedżera Pakietów w Visual Studio (Package Manager Console – PMC)**

``` powershell
PM> Install-Package Microsoft.EntityFrameworkCore -Project MyProject.Infrastructure
PM> Install-Package Microsoft.EntityFrameworkCore.SqlServer -Project MyProject.Infrastructure
PM> Install-Package Microsoft.EntityFrameworkCore.Tools -Project MyProject.UIKopiuj
```

**Wariant C: Bezpośredni wpis w pliku projektu (`.csproj`)** Zamiast uruchamiać komendy instalacyjne, odpowiednie referencje można zadeklarować bezpośrednio w sekcji `<ItemGroup>` pliku `.csproj` (zastępując `8.0.*` lub `9.0.*` / `10.0.*` wybraną wersją SDK):

``` xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.*">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>Kopiuj
```

#### 4. Zestawienie komend zarządzania schematem i migracjami

| Operacja                | Komenda .NET CLI (Terminal / Bash / PS) | Komenda PMC (Visual Studio) | Opis działania                                                                                                  |
|:------------------------|:----------------------------------------|:----------------------------|:----------------------------------------------------------------------------------------------------------------|
| **Dodanie migracji**    | `dotnet ef migrations add <Nazwa>`      | `Add-Migration <Nazwa>`     | Analizuje zmiany w modelu względem ostatniej migawki i generuje pliki migracji w katalogu `Migrations/`.        |
| **Usunięcie migracji**  | `dotnet ef migrations remove`           | `Remove-Migration`          | Usuwa ostatnią (jeszcze niezaaplikowaną w bazie) migrację i przywraca stan migawki `ModelSnapshot`.             |
| **Aktualizacja bazy**   | `dotnet ef database update`             | `Update-Database`           | Tworzy bazę (jeśli nie istnieje) i wykonuje wszystkie oczekujące migracje. Podanie `0` cofa wszystkie migracje. |
| **Usunięcie bazy**      | `dotnet ef database drop`               | `Drop-Database`             | Całkowicie usuwa docelową bazę danych.                                                                          |
| **Podgląd skryptu SQL** | `dotnet ef migrations script`           | `Script-Migration`          | Generuje czysty skrypt DDL SQL odpowiadający migracjom (przydatne do audytu i wdrożeń produkcyjnych).           |

**💡 Praca w solucji wieloprojektowej (Clean Architecture): Flagi `--project` oraz `--startup-project` (Kliknij, aby rozwinąć/zwinąć)**

Gdy klasa `DbContext` znajduje się w bibliotece **`Infrastructure`**, a plik `Program.cs` wraz z `appsettings.json` i rejestracją `AddDbContext` w projekcie **`UI`**, narzędzia EF Core muszą wiedzieć, gdzie zapisać pliki migracji, a skąd pobrać konfigurację połączenia:

- **W terminalu (.NET CLI – z poziomu głównego folderu solucji):**

  ``` bash
  dotnet ef migrations add InitialCreate --project University.Infrastructure --startup-project University.UI
  dotnet ef database update --project University.Infrastructure --startup-project University.UIKopiuj
  ```

- **W konsoli Package Manager Console (Visual Studio):**
  1.  Na górnym pasku okna *Package Manager Console* ustaw listę rozwijaną **Default project** na `University.Infrastructure`.
  2.  W oknie *Solution Explorer* kliknij prawym przyciskiem myszy projekt `University.UI` i wybierz **Set as Startup Project**.
  3.  Wykonaj polecenie `Add-Migration InitialCreate` oraz `Update-Database`.

------------------------------------------------------------------------

### 3. Encje (Entities) oraz Obiekty Wartości (Value Objects)

**Cel:** Zrozumienie fundamentalnej różnicy między encjami POCO a obiektami wartości w podejściu DDD oraz wykorzystanie rekordów (`record`) języka C# do implementacji niemodyfikowalnych obiektów wartości pilnujących własnych inwariantów.

#### 1. Encje vs Obiekty Wartości

- **Encje (Entities):** Najważniejsze byty z punktu widzenia modelowanego biznesu (np. `Student`, `Profesor`, `Kurs`, `Zamówienie`).
  - **Posiadają ciągłą tożsamość (Identity):** Dwie encje o identycznych wartościach wszystkich właściwości (np. dwóch studentów o imieniu i nazwisku *„Jan Kowalski”*) **nie są tym samym obiektem**, jeśli posiadają różny identyfikator (`Id` / `IndeksUczelniany`).
  - **Są mutowalne w czasie:** Ich stan ewoluuje w cyklu życia systemu i jest śledzony przez `ChangeTracker` w EF Core.
- **Obiekty Wartości (Value Objects):** Obiekty opisujące cechy, miary, kwoty lub adresy (np. `Money`, `Adres`, `ZakresDat`, `Email`).
  - **Brak tożsamości (Równość strukturalna):** Nie posiadają własnego klucza głównego `Id`. Dwa obiekty wartości o identycznym stanie (np. dwie instancje `Adres("Narutowicza 11/12", "Gdańsk", "80-233")`) są sobie **zawsze równe**.
  - **Wzorzec Whole Value Pattern (Ward Cunningham):** Zamiast rozpraszać prymitywne typy proste (`string`, `decimal`) po dziesiątkach klas encji (*antywzorzec Primitive Obsession*) i duplikować ich walidację (co łamie zasadę *Single Responsibility Principle* – pierwszą regułę SOLID), zamykamy wartość wraz z jej regułami integralności w dedykowanym typie.
  - **Niemodyfikowalność (Immutability):** Obiekt wartości podlega walidacji w konstruktorze w momencie tworzenia i po utworzeniu nie może zmienić swojego stanu (brak publicznych setterów `set;`, wyłącznie akcesory `init;`). Aby zmienić adres studenta, przypisujemy do właściwości zupełnie nową, zwalidowaną instancję rekordu `Adres` (lub korzystamy z wyrażenia `with`).

#### 2. Implementacja Obiektów Wartości za pomocą `record` w C#

Od wersji C# 9 dedykowanym i preferowanym sposobem implementacji obiektów wartości są **rekordy (`record` / `record class` oraz `readonly record struct`)**. Deklaracja podstawowego rekordu może zostać zapisana w jednej linii:

``` csharp
public record Money(decimal Amount, string Currency);Kopiuj
```

Słowo kluczowe `record` stanowi instrukcję dla kompilatora Roslyn, aby automatycznie wygenerował kod pośredni (**MSIL / CIL**) realizujący pełną semantykę obiektu wartości (tzw. *boilerplate code*), który dawniej należało pisać ręcznie w klasie bazowej `ValueObject`:

1.  **Niemodyfikowalność (`init`):** Kompilator generuje właściwości z akcesorem `init` (`modreq(IsExternalInit)`), co gwarantuje, że wartości można nadać wyłącznie podczas konstrukcji obiektu:

    ``` csharp
    public class Money : IEquatable<Money>
    {
        public decimal Amount { get; init; }
        public string Currency { get; init; }

        public Money(decimal Amount, string Currency)
        {
            this.Amount = Amount;
            this.Currency = Currency;
        }
    }Kopiuj
    ```

2.  **Porównywanie przez wartość (`Equals` oraz `GetHashCode`):** Dwie osobne referencje `Money` są równe, jeśli ich pola `Amount` i `Currency` przechowują te same wartości. Metoda `GetHashCode()` bezpiecznie łączy skróty wszystkich składowych:

    ``` csharp
    public override bool Equals(object? obj) => this.Equals(obj as Money);

    public virtual bool Equals(Money? other) =>
        other is not null &&
        EqualityComparer<decimal>.Default.Equals(this.Amount, other.Amount) &&
        EqualityComparer<string>.Default.Equals(this.Currency, other.Currency);

    public override int GetHashCode() =>
        System.HashCode.Combine(this.Amount, this.Currency);Kopiuj
    ```

3.  **Przeciążone operatory równości (`==` oraz `!=`):** Działają w oparciu o równość wartościową, a nie referencyjną:

    ``` csharp
    public static bool operator ==(Money? left, Money? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Money? left, Money? right) => !(left == right);Kopiuj
    ```

4.  **Czytelna reprezentacja `ToString()`:** Zamiast domyślnej nazwy typu w logach, kompilator generuje metodę `PrintMembers` oraz `ToString()` zwracającą sformatowany stan obiektu: `"Money { Amount = 100.50, Currency = PLN }"`.

5.  **Niedestrukcyjna mutacja (wyrażenie `with`) oraz Dekonstruktor:** Umożliwia utworzenie zmodyfikowanej kopii (`var m2 = m1 with { Amount = 200 };`) przy użyciu wygenerowanego chronionego konstruktora kopiującego `protected Money(Money original)` oraz rozpakowanie krotki (`var (amount, currency) = myMoney;`).

#### 3. Pełny Obiekt Wartości z ochroną inwariantów i walidacją w konstruktorze (`Adres` oraz `Money`)

Aby rekord nie był jedynie pasywnym kontenerem na dane (DTO), rozbudowujemy go o walidację inwariantów w konstruktorze, normalizację (`.Trim()`) oraz prywatny konstruktor bezparametrowy na potrzeby materializacji przez EF Core.

**Przykład A: Obiekt Wartości `Adres` (wymagany w zadaniu projektowym):**

``` csharp
using System.Text.RegularExpressions;

namespace University.Domain.ValueObjects;

public record Adres
{
    public string Ulica { get; init; } = string.Empty;
    public string Miasto { get; init; } = string.Empty;
    public string KodPocztowy { get; init; } = string.Empty;

    // Prywatny konstruktor bezparametrowy dla bezpiecznej materializacji przez ORM (EF Core)
    private Adres() { }

    public Adres(string ulica, string miasto, string kodPocztowy)
    {
        // === 1. WALIDACJA WEJŚCIOWA (Ochrona inwariantów) ===
        if (string.IsNullOrWhiteSpace(ulica))
            throw new ArgumentException("Nazwa ulicy nie może być pusta.", nameof(ulica));

        if (string.IsNullOrWhiteSpace(miasto))
            throw new ArgumentException("Nazwa miasta nie może być pusta.", nameof(miasto));

        if (string.IsNullOrWhiteSpace(kodPocztowy))
            throw new ArgumentException("Kod pocztowy nie może być pusty.", nameof(kodPocztowy));

        // === 2. NORMALIZACJA I WALIDACJA FORMATU ===
        var znormalizowanyKod = kodPocztowy.Trim();
        if (!Regex.IsMatch(znormalizowanyKod, @"^\d{2}-\d{3}$"))
        {
            throw new ArgumentException(
                "Kod pocztowy musi mieć format XX-XXX (np. 80-233).", nameof(kodPocztowy));
        }

        Ulica = ulica.Trim();
        Miasto = miasto.Trim();
        KodPocztowy = znormalizowanyKod;
    }

    public override string ToString() => $"{Ulica}, {KodPocztowy} {Miasto}";
}Kopiuj
```

**Przykład B: Obiekt Wartości `Money` z operacjami biznesowymi:**

``` csharp
namespace MyProject.Domain.ValueObjects;

public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    private static readonly HashSet<string> ValidCurrencies =
        new() { "PLN", "USD", "EUR", "GBP", "CHF" };

    private Money()
    {
        Currency = "PLN";
    }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Kwota pieniężna nie może być ujemna.");

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Kod waluty nie może być pusty.", nameof(currency));

        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3 || !ValidCurrencies.Contains(normalizedCurrency))
            throw new ArgumentException($"Waluta '{normalizedCurrency}' nie jest wspierana.", nameof(currency));

        Amount = amount;
        Currency = normalizedCurrency;
    }

    public Money Add(Money other)
    {
        if (this.Currency != other.Currency)
            throw new InvalidOperationException("Nie można dodawać kwot w różnych walutach.");
        return new Money(this.Amount + other.Amount, this.Currency);
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}Kopiuj
```

> **⚠️ Konstruktory w rekordach i klasach a EF Core (Constructor Binding):**  
> EF Core potrafi tworzyć instancje encji i obiektów wartości wywołując konstruktor z parametrami, **pod warunkiem**, że nazwy parametrów konstruktora dokładnie odpowiadają nazwom właściwości (z dokładnością do wielkości pierwszej litery, np. `ulica` → `Ulica`). Dobrą praktyką w złożonych klasach i rekordach domenowych jest dodanie prywatnego lub chronionego konstruktora bezparametrowego (`private Adres() { }`), z którego EF Core skorzysta podczas materializacji danych z bazy SQL.

------------------------------------------------------------------------

### 4. Konfiguracja Modelu Danych: Fluent API, *Shadow Properties*, Indeksy i Obiekty Wartości (`OwnsOne` vs `ComplexProperty`)

**Cel:** Opanowanie technik mapowania klas POCO na schemat relacyjnej bazy danych za pomocą atrybutów `DataAnnotations`, płynnego interfejsu `Fluent API`, właściwości ukrytych (*Shadow Properties*) oraz encji posiadanych (`OwnsOne`).

#### 1. Adnotacje danych (`DataAnnotations Attributes`)

Podstawowe reguły mapowania można zadeklarować bezpośrednio nad właściwościami klas za pomocą atrybutów z przestrzeni `System.ComponentModel.DataAnnotations` oraz `System.ComponentModel.DataAnnotations.Schema`:

- `[Key]` – jawnie wskazuje klucz główny encji (`Primary Key`). Domyślnie EF Core stosuje konwencję nazewniczą i automatycznie traktuje jako klucz główny właściwość o nazwie `Id` lub `<NazwaKlasy>Id` (np. `StudentId`).
- `[Required]` – wymusza ograniczenie `NOT NULL` na odpowiadającej kolumnie w bazie danych (przy włączonych *Nullable Reference Types* w C# właściwości typu `string` bez znaku `?` są domyślnie traktowane jako `NOT NULL`).
- `[MaxLength(100)]` / `[StringLength(100, MinimumLength = 3)]` – określa maksymalny rozmiar kolumny tekstowej w bazie danych (np. `nvarchar(100)` zamiast domyślnego `nvarchar(max)`).
- `[NotMapped]` – wyklucza właściwość lub klasę z mapowania na kolumnę/tabelę w bazie danych (przydatne dla właściwości wyliczanych w locie).

#### 2. Konfiguracja przez `Fluent API` w `OnModelCreating`

Aby nie zanieczyszczać czystego modelu domenowego (`Domain`) atrybutami bazodanowymi, w architekturze warstwowej preferujemy konfigurację poprzez **Fluent API** wewnątrz metody `OnModelCreating(ModelBuilder modelBuilder)` klasy `DbContext` (w warstwie `Infrastructure`). `Fluent API` ma zawsze **wyższy priorytet** niż adnotacje danych i konwencje domyślne:

- **Klucze główne (pojedyncze i złożone – Composite Keys):**

  ``` csharp
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
      // Klucz główny na niestandardowej właściwości (np. Prefix w LicznikIndeksow)
      modelBuilder.Entity<LicznikIndeksow>()
          .HasKey(l => l.Prefix);

      // Klucz złożony z dwóch kolumn
      modelBuilder.Entity<Enrollment>()
          .HasKey(e => new { e.StudentId, e.KursId });
  }Kopiuj
  ```

- **Wymagalność, długość kolumn i Unikalne Indeksy (`HasIndex` + `IsUnique`):** Indeksy B-Tree drastycznie przyspieszają wyszukiwanie (operacje `WHERE` po kolumnie) oraz gwarantują na poziomie silnika SQL unikalność wartości biznesowych (np. numeru indeksu uczelnianego):

  ``` csharp
  modelBuilder.Entity<Student>(builder =>
  {
      builder.Property(s => s.IndeksUczelniany)
          .IsRequired()
          .HasMaxLength(20);

      builder.HasIndex(s => s.IndeksUczelniany)
          .IsUnique();
  });Kopiuj
  ```

- **Konwertery wartości (`HasConversion` / `ValueConverter`):** Pozwalają na transformację typu w locie podczas zapisu i odczytu z bazy danych (np. zapis `enum` jako czytelnego tekstu `string` zamiast `int`):

  ``` csharp
  modelBuilder.Entity<Course>()
      .Property(c => c.Category)
      .HasConversion<string>(); // Zapisuje nazwę etykiety enum zamiast liczbyKopiuj
  ```

- **Inicjalizacja danych startowych (`HasData` – Data Seeding):** Pozwala wypełnić tabele danymi słownikowymi lub początkowymi bezpośrednio podczas wykonywania migracji:

  ``` csharp
  modelBuilder.Entity<LicznikIndeksow>().HasData(
      new LicznikIndeksow { Prefix = "S", AktualnaWartosc = 1000 },
      new LicznikIndeksow { Prefix = "P", AktualnaWartosc = 100 }
  );Kopiuj
  ```

#### 3. Właściwości Ukryte (*Shadow Properties*) i metoda `EF.Property<T>`

Często w bazie danych potrzebujemy przechowywać kolumny techniczne lub audytowe (np. `OstatniaModyfikacja`, `UtworzonoPrzez`, `TenantId`), które nie stanowią części logiki domenowej i nie powinny zaśmiecać czystych klas POCO w warstwie `Domain`.

**Właściwość ukryta (*Shadow Property*)** to kolumna zdefiniowana wyłącznie w modelu metadanych EF Core, której wartość przechowywana jest wewnątrz `ChangeTrackera` (w obiekcie `InternalEntityEntry`), a nie we właściwości klasy C#:

1.  **Deklaracja *Shadow Property* we Fluent API (`OnModelCreating`):**

    ``` csharp
    modelBuilder.Entity<Student>()
        .Property<DateTime>("OstatniaModyfikacja");Kopiuj
    ```

2.  **Zapis i odczyt wartości dla śledzonej encji przez `ChangeTracker` (w serwisie CRUD):**

    ``` csharp
    // Ustawienie wartości podczas tworzenia (Create) lub edycji (Update) encji:
    _context.Entry(student).Property("OstatniaModyfikacja").CurrentValue = DateTime.UtcNow;

    // Odczyt wartości z pamięci ChangeTrackera:
    DateTime zmodyfikowano = _context.Entry(student).Property<DateTime>("OstatniaModyfikacja").CurrentValue;Kopiuj
    ```

3.  **Odwołanie do *Shadow Property* wewnątrz zapytań LINQ (`EF.Property<T>`):** Ponieważ klasa `Student` nie posiada w C# właściwości `.OstatniaModyfikacja`, w zapytaniach LINQ (`Where`, `OrderBy`, `Select`) odwołujemy się do niej za pomocą statycznej metody pomocniczej **`EF.Property<T>(encja, "NazwaKolumny")`**, którą translator EF Core zamienia na nazwę kolumny w SQL:

    ``` csharp
    var ostatnioZmodyfikowani = await _context.Studenci
        .AsNoTracking()
        .OrderByDescending(s => EF.Property<DateTime>(s, "OstatniaModyfikacja"))
        .Select(s => new
        {
            s.IndeksUczelniany,
            ImieNazwisko = $"{s.Imie} {s.Nazwisko}",
            OstatniaZmiana = EF.Property<DateTime>(s, "OstatniaModyfikacja")
        })
        .ToListAsync();Kopiuj
    ```

#### 4. Separacja konfiguracji: `IEntityTypeConfiguration<TEntity>`

W dużych projektach umieszczanie konfiguracji kilkudziesięciu encji w jednej metodzie `OnModelCreating` czyni klasę `DbContext` nieczytelną. Dobrą praktyką jest wydzielenie konfiguracji każdej encji do osobnej klasy implementującej interfejs `IEntityTypeConfiguration<TEntity>`:

``` csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.IndeksUczelniany)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(s => s.IndeksUczelniany)
            .IsUnique();

        // Deklaracja Shadow Property (kolumna audytowa w tabeli SQL):
        builder.Property<DateTime>("OstatniaModyfikacja");

        // Konfiguracja obiektu wartości jako Encji Posiadanej (Owned Entity):
        builder.OwnsOne(s => s.AdresZamieszkania, adresBuilder =>
        {
            adresBuilder.Property(a => a.Ulica).HasMaxLength(100).HasColumnName("Ulica").IsRequired();
            adresBuilder.Property(a => a.Miasto).HasMaxLength(60).HasColumnName("Miasto").IsRequired();
            adresBuilder.Property(a => a.KodPocztowy).HasMaxLength(10).HasColumnName("KodPocztowy").IsRequired();
        });
    }
}Kopiuj
```

Następnie w `DbContext` ładujemy wszystkie klasy konfiguracyjne z bieżącego zestawu (*assembly*) jedną linijką:

``` csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(UniversityDbContext).Assembly);
}Kopiuj
```

#### 5. Mapowanie Obiektów Wartości: `OwnsOne` (Owned Entities) vs `ComplexProperty` (Complex Types)

W EF Core istnieją dwa mechanizmy utrwalania Obiektów Wartości (takich jak nasz rekord `Adres`), różniące się sposobem reprezentacji w silniku śledzenia zmian:

##### Wariant A: Encje Posiadane (`Owned Entity Types` – `.OwnsOne` / `.OwnsMany`)

Klasyczny i najbardziej wszechstronny mechanizm w EF Core. Pod maską EF Core traktuje typ `Owned` jak **podrzędną encję zależną połączoną z właścicielem związkiem `1:1`**, automatycznie tworząc dla niej ukryty klucz główny (**Shadow Primary Key**) tożsamy z kluczem właściciela:

- **Domyślne spłaszczenie kolumn (Table Splitting):** Właściwości obiektu `Adres` trafiają jako dodatkowe kolumny bezpośrednio do tabeli właściciela (`Studenci`), o nazwach zgodnych z konwencją `AdresZamieszkania_Ulica` (lub nadanych przez `.HasColumnName("Ulica")`).
- **Automatyczne ładowanie bez `.Include()`:** Pobierając właściciela (`context.Studenci.ToList()`), jego obiekty `Owned` są zawsze dołączane automatycznie.
- **Unikalne możliwości `OwnsOne`:** Ponieważ posiada ukryty klucz główny, wspiera opcjonalne obiekty wartości (`Adres?` = `null`), mapowanie do osobnej tabeli (`.ToTable("AdresyStudentow")`), kolekcje obiektów wartości (`.OwnsMany()`) oraz kolumny dokumentowe JSON (`.ToJson()`).
- **Ograniczenie:** Ponieważ każda instancja `OwnsOne` otrzymuje w `StateManagerze` własny wpis `InternalEntityEntry` z kluczem właściciela, **nie wolno przypisać tej samej referencji obiektu `Adres` do dwóch różnych śledzonych encji** (spowoduje to wyjątek duplikacji klucza przy `SaveChanges()`).

##### Wariant B: Typy Złożone (`Complex Types` – `.ComplexProperty` od EF Core 8+)

Nowy mechanizm zaprojektowany specjalnie dla niemodyfikowalnych rekordów (`record`) i struktur wartościowych (`readonly record struct`):

- **Brak ukrytego klucza i brak `InternalEntityEntry`:** EF Core traktuje `ComplexProperty` wyłącznie jako grupę kolumn dopisaną bezpośrednio do tablicy migawki (`OriginalValues`) rodzica.
- **Współdzielenie referencji (Wzorzec *Flyweight*):** Tę samą instancję `var adresAkademika = new Adres(...)` można bezpiecznie przypisać do 500 studentów w pamięci RAM!

| Cecha w silniku EF Core                       | `.OwnsOne()` (*Owned Entity*)              | `.ComplexProperty()` (*Complex Type*, EF 8+) |
|:----------------------------------------------|:-------------------------------------------|:---------------------------------------------|
| **Ukryty klucz (Shadow PK)**                  | **Tak** (przejęty od encji właściciela)    | **Brak** (zero kluczy w modelu)              |
| **Osobny wpis w `ChangeTrackerze`**           | **Tak** (osobny `InternalEntityEntry`)     | **Brak** (spłaszczony w tablicy właściciela) |
| **Współdzielenie tej samej instancji**        | **Zabronione** (błąd śledzenia tożsamości) | **Dozwolone** (idealne dla `record`)         |
| **Wsparcie dla `.ToTable()` i `.OwnsMany()`** | **Pełne wsparcie**                         | **Brak** (wymaga klucza do `JOIN`)           |

------------------------------------------------------------------------

### 5. Związki Między Encjami (Relationships), Nawigacje, `DeleteBehavior` i Ładowanie Danych

**Cel:** Opanowanie modelowania związków `1:1`, `1:N`, `N:M` (w tym z tabelą pośrednią przechowującą dane oraz związków samoodwołujących się), konfiguracji kaskadowego usuwania oraz unikania pułapek wydajnościowych: Problemu N+1 zapytań i Eksplozji Kartezjańskiej.

#### 1. Relacja a Związek oraz Właściwości Nawigacyjne i *Relationship Fixup*

W algebrze relacyjnej E. F. Codda **relacją (*Relation*)** jest sama pojedyncza tabela, natomiast powiązanie między dwiema tabelami za pomocą klucza obcego (*Foreign Key* – FK) to **związek (*Relationship*)**, potocznie nazywany „relacją”. W modelu obiektowym C# związki reprezentujemy poprzez **właściwości nawigacyjne**:

- **Odniesienie nawigacyjne (Reference Navigation):** Pojedyncza referencja do powiązanej encji (strona „jeden” w związkach `1:1` oraz `1:N`).
  - Dla związku **opcjonalnego** (klucz obcy `int?` dopuszcza `NULL`) właściwość nawigacyjna również jest nullowalna (np. `public Profesor? Promotor { get; set; }`).
  - Dla związku **wymaganego** (klucz obcy `int` jest `NOT NULL`) stosujemy zapis `public Wydzial Wydzial { get; set; } = null!;` (operator `null!` wycisza ostrzeżenie kompilatora NRT przed załadowaniem nawigacji przez EF Core).
- **Kolekcja nawigacyjna (Collection Navigation):** Kolekcja `ICollection<T>` lub `List<T>` / `HashSet<T>` reprezentująca stronę „wiele” w związkach `1:N` oraz `N:M`. Zawsze inicjalizujemy ją w miejscu deklaracji (`= new List<T>();`), aby uniknąć wyjątku `NullReferenceException`.
  - *Uwaga:* Nigdy nie używaj właściwości wyliczanej `public ICollection<Kurs> Kursy => new List<Kurs>();`, ponieważ każde odwołanie do właściwości alokowałoby nową, pustą listę, niszcząc działanie `.Include()`!
- **Automatyczne spinanie referencji (*Relationship Fixup*):** Dla encji śledzonych przez `ChangeTracker` EF Core automatycznie synchronizuje obie strony związku w pamięci RAM (np. przypisanie `kurs.Wydzial = w` automatycznie dodaje `kurs` do kolekcji `w.Kursy`).

#### 2. Wzorce związków między encjami i ich konfiguracja we Fluent API

##### A. Związek Jeden-do-Jednego (`1:1` – One-to-One, np. `Profesor` <span class="math inline">\\\leftrightarrow\\</span> `Gabinet`)

W związku 1:1 jedna strona jest **encją główną (Principal – rodzicem)**, a druga **encją zależną (Dependent – dzieckiem)**. To w encji zależnej musi znajdować się właściwość klucza obcego (`ProfesorId`), na której EF Core automatycznie założy indeks unikalny:

``` csharp
// Encja główna (Principal)
public class Profesor
{
    public int Id { get; set; }
    public string Imie { get; set; } = string.Empty;
    public string Nazwisko { get; set; } = string.Empty;
    public Gabinet? Gabinet { get; set; } // Nawigacja do encji zależnej
}

// Encja zależna (Dependent - zawiera klucz obcy!)
public class Gabinet
{
    public int Id { get; set; }
    public string NumerPokoju { get; set; } = string.Empty;
    public string Budynek { get; set; } = string.Empty;

    public int ProfesorId { get; set; } // Klucz obcy (FK)
    public Profesor Profesor { get; set; } = null!;
}

// Konfiguracja Fluent API (w OnModelCreating):
// Uwaga: W związku 1:1 w HasForeignKey<TDependent> należy jawnie podać typ encji zależnej!
modelBuilder.Entity<Profesor>()
    .HasOne(p => p.Gabinet)
    .WithOne(g => g.Profesor)
    .HasForeignKey<Gabinet>(g => g.ProfesorId)
    .OnDelete(DeleteBehavior.Cascade);Kopiuj
```

##### B. Związek Jeden-do-Wielu (`1:N` – One-to-Many, np. `Wydział` <span class="math inline">\\\leftrightarrow\\</span> `Kurs`)

``` csharp
public class Wydzial
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = string.Empty;
    public ICollection<Kurs> Kursy { get; set; } = new List<Kurs>();
}

public class Kurs
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = string.Empty;
    public int PunktyECTS { get; set; }

    public int WydzialId { get; set; }
    public Wydzial Wydzial { get; set; } = null!;
}

// Konfiguracja Fluent API:
modelBuilder.Entity<Wydzial>()
    .HasMany(w => w.Kursy)
    .WithOne(k => k.Wydzial)
    .HasForeignKey(k => k.WydzialId)
    .OnDelete(DeleteBehavior.Restrict);Kopiuj
```

##### C. Związek Wiele-do-Wielu z dodatkowymi danymi (`N:M` z encją pośrednią / Payload, np. `Enrollment`)

Gdy tabela łącząca oprócz pary kluczy obcych przechowuje dodatkowe dane biznesowe (np. ocenę końcową oraz semestr zapisu na kurs), definiujemy jawną klasę encji łączącej (`Enrollment`) i konfigurujemy ją za pomocą dwóch związków `1:N`:

``` csharp
public class Enrollment
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int KursId { get; set; }
    public Kurs Kurs { get; set; } = null!;

    // Dodatkowe dane biznesowe (Payload) w tabeli łączącej:
    public double? Ocena { get; set; }
    public string Semestr { get; set; } = string.Empty; // np. "2026/27-Z"
}

// Konfiguracja Fluent API:
modelBuilder.Entity<Enrollment>(builder =>
{
    builder.HasKey(e => new { e.StudentId, e.KursId });

    builder.HasOne(e => e.Student)
        .WithMany(s => s.Zapisy)
        .HasForeignKey(e => e.StudentId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne(e => e.Kurs)
        .WithMany(k => k.Zapisy)
        .HasForeignKey(e => e.KursId)
        .OnDelete(DeleteBehavior.Cascade);
});Kopiuj
```

##### D. Związek Wiele-do-Wielu samoodwołujący się (*Self-Referencing Many-to-Many*, np. `Prerequisites` na encji `Kurs`)

Kurs może wymagać zaliczenia innych kursów (wymagania wstępne – `WymaganiaWstepne`), a jednocześnie sam może być wymaganiem wstępnym dla kolejnych przedmiotów (`KursyZalezne`). EF Core utworzy w tle tabelę pośrednią opartą o *Shadow Properties*:

``` csharp
public class Kurs
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = string.Empty;
    public string KodKursu { get; set; } = string.Empty;
    public int PunktyECTS { get; set; }

    // Kursy, które trzeba zaliczyć PRZED tym kursem (Prerequisites)
    public ICollection<Kurs> WymaganiaWstepne { get; set; } = new HashSet<Kurs>();

    // Kursy, dla których TEN kurs jest wymaganiem wstępnym
    public ICollection<Kurs> KursyZalezne { get; set; } = new HashSet<Kurs>();
}

// Konfiguracja Fluent API dla Self-Referencing N:M:
modelBuilder.Entity<Kurs>()
    .HasMany(k => k.WymaganiaWstepne)
    .WithMany(k => k.KursyZalezne)
    .UsingEntity(join => join.ToTable("WymaganiaWstepneKursow"));Kopiuj
```

#### 3. Więzy integralności przy usuwaniu encji (`OnDelete` / `DeleteBehavior`)

Podczas usuwania encji głównej (rodzica), relacyjna baza danych oraz kontekst EF Core muszą wiedzieć, co zrobić z przypisanymi do niej encjami zależnymi:

| Wartość `DeleteBehavior`        | Wymagalność FK        | Działanie w bazie danych i EF Core                                                                                                                                                        |
|:--------------------------------|:----------------------|:------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **`Cascade`**                   | Wymagany (`NOT NULL`) | **Domyślne dla kluczy wymaganych.** Usunięcie rodzica automatycznie usuwa wszystkie przypisane rekordy zależne (np. usunięcie `Profesora` usuwa jego `Gabinet`).                          |
| **`SetNull`**                   | Opcjonalny (`NULL`)   | Usunięcie rodzica **nie usuwa** encji zależnej, lecz silnik SQL ustawia jej kolumnę klucza obcego na `NULL` (np. usunięcie `Profesora`-promotora zeruje pole `PromotorId` u magistranta). |
| **`Restrict`** / **`NoAction`** | Dowolny               | Blokuje usunięcie rodzica (rzuca `DbUpdateException`), dopóki istnieją przypisane do niego encje zależne. Przerywa cykle kaskadowe (błąd 1785 w MS SQL Server).                           |
| **`ClientSetNull`**             | Opcjonalny (`NULL`)   | **Domyślne dla kluczy opcjonalnych.** EF Core zeruje klucz obcy wyłącznie dla wczytanych do RAM dzieci, ale w bazie SQL tworzy więz `NO ACTION`!                                          |

> **⚠️ Dlaczego dla promotora musimy jawnie skonfigurować `DeleteBehavior.SetNull`?**  
> Dla opcjonalnego klucza `int? PromotorId` EF Core domyślnie ustawia `ClientSetNull` (w bazie powstaje więz `ON DELETE NO ACTION`). Gdybyśmy usunęli profesora bez wcześniejszego wczytania wszystkich jego magistrantów do pamięci RAM przez `.Include()`, baza danych rzuciłaby wyjątek naruszenia klucza obcego! Jawne ustawienie `.OnDelete(DeleteBehavior.SetNull)` generuje w SQL regułę `ON DELETE SET NULL`, dzięki czemu baza danych sama bezpiecznie wyzeruje pole `PromotorId`:

``` csharp
modelBuilder.Entity<StudentStudiowMagisterskich>()
    .HasOne(m => m.Promotor)
    .WithMany(p => p.Dyplomanci)
    .HasForeignKey(m => m.PromotorId)
    .IsRequired(false) // Klucz obcy PromotorId (int?) musi dopuszczać NULL!
    .OnDelete(DeleteBehavior.SetNull);Kopiuj
```

#### 4. Strategie ładowania powiązanych danych: Problem N+1 oraz Eksplozja Kartezjańska (`AsSplitQuery`)

##### A. Pułapka opóźnionego ładowania (*Lazy Loading*) i Problem N+1

**Lazy Loading** polega na automatycznym wykonywaniu zapytania SQL w tle w momencie pierwszego odwołania się w kodzie C# do właściwości nawigacyjnej (wymaga pakietu `Microsoft.EntityFrameworkCore.Proxies` i słowa kluczowego `virtual`). Choć wygodny, Lazy Loading jest główną przyczyną katastrofalnego spadku wydajności znanego jako **Problem N+1 zapytań**:

``` csharp
// ANTYWZORZEC (Problem N+1 przy włączonym Lazy Loading):
var kursy = context.Kursy.Take(100).ToList(); // 1 zapytanie SQL

foreach (var kurs in kursy)
{
    // W każdej iteracji pętli odwołanie do kurs.Zapisy uruchamia OSOBNE zapytanie SQL!
    // Łącznie baza danych zostaje uderzona 1 + 100 = 101 zapytaniami!
    Console.WriteLine($"Kurs: {kurs.Nazwa}, zapisanych: {kurs.Zapisy.Count}");
}Kopiuj
```

##### B. Rozwiązanie: Ładowanie chciwe (*Eager Loading* – `Include` / `ThenInclude`) oraz Podział Zapytania (`AsSplitQuery`)

W profesjonalnych aplikacjach nie używamy `LazyLoadingProxies`, lecz jawnie wskazujemy za pomocą metod `.Include()` oraz `.ThenInclude()`, które tabele mają zostać pobrane:

``` csharp
// POPRAWNE ROZWIĄZANIE (Eager Loading - tylko 1 zapytanie SQL z JOIN):
var kursyZZapisami = await context.Kursy
    .AsNoTracking()
    .Include(k => k.Wydzial)
    .Include(k => k.Zapisy)
        .ThenInclude(z => z.Student)
    .Take(100)
    .ToListAsync();Kopiuj
```

> **🚀 Pułapka wielu kolekcji w `.Include()` (Eksplozja Kartezjańska) i metoda `.AsSplitQuery()`:**  
> Gdy pobierasz encję (np. `Profesor`) i dołączasz przez `.Include()` **dwie równoległe kolekcje `1:N`** (np. 10 `ProwadzoneKursy` oraz 20 `Dyplomanci`), pojedyncze zapytanie SQL z dwoma `LEFT JOIN` przemnoży wiersze przez siebie, przesyłając przez sieć <span class="math inline">\\10 \times 20 = 200\\</span> zduplikowanych wierszy dla jednego profesora!  
> Dodanie metody **`.AsSplitQuery()`** instruuje EF Core, aby pobrał każdą kolekcję w osobnym, płaskim zapytaniu SQL (<span class="math inline">\\1 + 10 + 20 = 31\\</span> wierszy bez duplikacji):
>
> ``` csharp
> var profesorowie = await context.Profesorowie
>     .AsNoTracking()
>     .AsSplitQuery() // Chroni przed Eksplozją Kartezjańską!
>     .Include(p => p.Gabinet)
>     .Include(p => p.ProwadzoneKursy)
>     .Include(p => p.Dyplomanci)
>     .ToListAsync();Kopiuj
> ```

------------------------------------------------------------------------

### 6. Strategie Mapowania Dziedziczenia w Bazie Danych (TPH, TPT, TPC)

**Cel:** Zaznajomienie z trzema sposobami odwzorowania obiektowej hierarchii dziedziczenia (np. `Student` <span class="math inline">\\\leftarrow\\</span> `StudentStudiowMagisterskich`) na struktury tabel relacyjnych.

Relacyjne bazy danych nie posiadają natywnego mechanizmu dziedziczenia tabel. EF Core udostępnia trzy strategie odwzorowania hierarchii klas:

``` text
Hierarchia w C#:   class Student { Id, Imie, Indeks }  <──  class StudentMagisterski : Student { TematPracy, PromotorId }

1. TPH (Table-Per-Hierarchy) [Domyślna]:
   ┌────────────────────────────────────────────────────────────────────────────┐
   │ Tabela: Studenci                                                           │
   │ Id | Discriminator (Shadow) | Imie | Indeks | TematPracy (NULL) | Promotor │
   └────────────────────────────────────────────────────────────────────────────┘

2. TPT (Table-Per-Type):
   ┌─────────────────────────────┐       ┌──────────────────────────────────────┐
   │ Tabela: Studenci            │ 1───1 │ Tabela: StudenciMagisterscy          │
   │ Id (PK) | Imie | Indeks     │◄──────┤ Id (PK, FK) | TematPracy | PromotorId│
   └─────────────────────────────┘       └──────────────────────────────────────┘

3. TPC (Table-Per-Concrete-Type):
   ┌─────────────────────────────┐       ┌──────────────────────────────────────┐
   │ Tabela: Studenci            │       │ Tabela: StudenciMagisterscy          │
   │ Id | Imie | Indeks          │       │ Id | Imie | Indeks | Temat | Promotor│
   └─────────────────────────────┘       └──────────────────────────────────────┘Kopiuj
```

#### Zestawienie porównawcze strategii dziedziczenia

| Cecha                      | 1\. TPH (Table-Per-Hierarchy)                                                                            | 2\. TPT (Table-Per-Type)                                                                  | 3\. TPC (Table-Per-Concrete-Type)                                                             |
|:---------------------------|:---------------------------------------------------------------------------------------------------------|:------------------------------------------------------------------------------------------|:----------------------------------------------------------------------------------------------|
| **Struktura tabel**        | **Jedna wspólna tabela** dla całej hierarchii klas + ukryta kolumna `Discriminator` (*Shadow Property*). | **Osobna tabela dla każdej klasy** (bazowej i potomnych) połączona związkiem 1:1 po `Id`. | **Osobna tabela dla każdej klasy nieabstrakcyjnej** (kolumny bazowe są powielone).            |
| **Wydajność odczytu**      | **Bardzo wysoka** (brak `JOIN` i `UNION`, odczyt z jednej tabeli).                                       | **Niska** (wymaga kosztownych złączeń `LEFT JOIN` przy każdym zapytaniu).                 | **Wysoka** przy odpytywaniu konkretnego podtypu; zapytania polimorficzne używają `UNION ALL`. |
| **Normalizacja i `NULL`**  | Kolumny klas potomnych **muszą dopuszczać `NULL`** w bazie danych.                                       | Pełna normalizacja (3NF) – kolumny klas potomnych mogą być `NOT NULL`.                    | Kolumny klas potomnych mogą być `NOT NULL` (brak pustych komórek).                            |
| **Konfiguracja w EF Core** | **Domyślna** (opcjonalnie `.UseTphMappingStrategy()` lub `.HasDiscriminator()`).                         | Wymaga `.UseTptMappingStrategy()` lub wywołania `.ToTable(...)` dla każdej klasy.         | Wymaga `.UseTpcMappingStrategy()` oraz sekwencji `SEQUENCE` dla unikalnych kluczy `Id`.       |

------------------------------------------------------------------------

### 7. Kontekst Danych (`DbContext`), `ChangeTracker` (`InternalEntityEntry`) i Operacje `Update`

**Cel:** Zrozumienie architektury klasy `DbContext`, wewnętrznej budowy `ChangeTrackera` (`StateManager`, `InternalEntityEntry`) oraz trzech scenariuszy modyfikacji danych.

#### 1. Rola `DbContext` jako wzorców Repository oraz Unit of Work

Klasa dziedzicząca po `DbContext` stanowi centralny punkt komunikacji z bazą danych i łączy w sobie dwa klasyczne wzorce projektowe Martina Fowlera:

- **Wzorzec Jednostki Pracy (*Unit of Work*):** Zamiast wysyłać pojedyncze zapytanie `INSERT`/`UPDATE` przy każdej zmianie właściwości w kodzie C#, `DbContext` śledzi wszystkie modyfikacje obiektów w pamięci za pomocą mechanizmu **`ChangeTracker`**. Dopiero wywołanie `SaveChanges()` / `SaveChangesAsync()` grupuje wszystkie wykryte zmiany i wysyła je do bazy w ramach jednej atomowej transakcji.
- **Wzorzec Repozytorium (*Repository*):** Właściwości typu `DbSet<TEntity>` reprezentują kolekcje encji głównych, na których można wykonywać zapytania LINQ (`IQueryable<T>`) oraz operacje dodawania (`Add`) i usuwania (`Remove`).

``` csharp
using Microsoft.EntityFrameworkCore;
using University.Domain.Entities;

namespace University.Infrastructure.Persistence;

public class UniversityDbContext : DbContext
{
    public DbSet<Student> Studenci => Set<Student>();
    public DbSet<StudentStudiowMagisterskich> StudenciMagisterscy => Set<StudentStudiowMagisterskich>();
    public DbSet<Profesor> Profesorowie => Set<Profesor>();
    public DbSet<Kurs> Kursy => Set<Kurs>();
    public DbSet<Wydzial> Wydzialy => Set<Wydzial>();
    public DbSet<Gabinet> Gabinety => Set<Gabinet>();
    public DbSet<Enrollment> Zapisy => Set<Enrollment>();
    public DbSet<LicznikIndeksow> LicznikiIndeksow => Set<LicznikIndeksow>();

    public UniversityDbContext(DbContextOptions<UniversityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UniversityDbContext).Assembly);
    }
}Kopiuj
```

#### 2. Jak działa `ChangeTracker` pod maską? (`StateManager`, `InternalEntityEntry` i 3 scenariusze `Update`)

Ponieważ nasze klasy domenowe to czyste **POCO**, nie posiadają w sobie żadnych ukrytych pól śledzących ORM. Zamiast tego wewnątrz `DbContext` działa komponent **`StateManager`**, który dla każdej pobranej encji alokuje na stercie pomocniczą strukturę kontrolną **`InternalEntityEntry`** zawierającą:

1.  **`EntityState`** – aktualny stan obiektu (`Detached`, `Unchanged`, `Added`, `Modified`, `Deleted`),
2.  **`OriginalValues` (Snapshot)** – kopię pierwotnych wartości kolumn w momencie odczytu z bazy, z którą metoda `DetectChanges()` porównuje bieżący stan obiektu POCO podczas `SaveChangesAsync()`,
3.  **`ShadowValues`** – bufor przechowujący wartości dla właściwości ukrytych (*Shadow Properties*, np. `"OstatniaModyfikacja"`).

W zależności od tego, czy encja znajduje się w `StateManagerze`, wyróżniamy **3 scenariusze modyfikacji danych (`Update`)**:

- **Scenariusz 1: Encja Śledzona (*Connected* – zalecany w serwisach aplikacyjnych):** Pobieramy encję z kontekstu, zmieniamy wybrane właściwości w C# (oraz ewentualnie *Shadow Property*) i wywołujemy `await _context.SaveChangesAsync()`. **Nie wywołujemy tutaj `_context.Update(encja)`!** `ChangeTracker` sam wykryje zmienione pola (*Dirty Checking*) i wygeneruje `UPDATE` wyłącznie dla zmodyfikowanych kolumn.
- **Scenariusz 2: Encja Odłączona (*Disconnected*):** Obiekt pochodzi spoza kontekstu (np. zdeserializowany z JSON). Wywołanie `_context.Studenci.Update(dto)` podpina obiekt i oznacza **wszystkie** jego kolumny jako `Modified` (nadpisując cały wiersz w SQL).
- **Scenariusz 3: Bezpośredni rozkaz SQL (`ExecuteUpdateAsync` od EF Core 7+):** Tłumaczy wyrażenie LINQ bezpośrednio na masowy rozkaz `UPDATE` w bazie danych z całkowitym pominięciem `ChangeTrackera` i bez ładowania encji do pamięci RAM.

> **⚠️ Krytyczna zasada współbieżności `DbContext`:**  
> Instancja `DbContext` **NIE jest bezpieczna wątkowo (not thread-safe)** i jest projektowana do krótkiego cyklu życia (`Scoped`). Nigdy nie wykonuj równoległych operacji (`Task.WhenAll`) na tej samej instancji `DbContext` i zawsze oczekuj (`await`) na zakończenie każdej metody asynchronicznej przed wywołaniem kolejnej!

#### 3. Konfiguracja Connection Stringa i rejestracja w `Program.cs`

Ciąg połączeniowy (*Connection String*) przechowujemy w pliku konfiguracyjnym `appsettings.json` (lub konfigurujemy w `Program.cs`):

``` json
{
  "ConnectionStrings": {
    "DefaultSqlServer": "Server=(localdb)\\mssqllocaldb;Database=UniversityCleanDb;Trusted_Connection=True;MultipleActiveResultSets=true",
    "SqliteConnection": "Data Source=university.db"
  }
}Kopiuj
```

Rejestracja kontekstu i serwisów w kontenerze Dependency Injection (w pliku `Program.cs`):

``` csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using University.Application.Services;
using University.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultSqlServer")
    ?? "Server=(localdb)\\mssqllocaldb;Database=UniversityCleanDb;Trusted_Connection=True;";

builder.Services.AddDbContext<UniversityDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<UniversityQueryService>();

using IHost host = builder.Build();Kopiuj
```

**💡 Fabryka czasu projektowania (`IDesignTimeDbContextFactory`) dla aplikacji konsolowych i WPF (Kliknij, aby rozwinąć/zwinąć)**

W prostych aplikacjach konsolowych lub WPF, które nie używają domyślnego `Host.CreateApplicationBuilder(args)`, narzędzie `Add-Migration` / `dotnet ef migrations add` może zgłosić błąd braku konfiguracji `DbContext`. Najczystszym rozwiązaniem w warstwie `Infrastructure` jest dodanie klasy implementującej `IDesignTimeDbContextFactory<TContext>`, z której korzystają wyłącznie narzędzia migracji:

``` csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace University.Infrastructure.Persistence;

public class UniversityDbContextFactory : IDesignTimeDbContextFactory<UniversityDbContext>
{
    public UniversityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UniversityDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=UniversityCleanDb;Trusted_Connection=True;");

        return new UniversityDbContext(optionsBuilder.Options);
    }
}Kopiuj
```

------------------------------------------------------------------------

### 8. Migracje Schematu Bazy Danych i Fasada `DatabaseFacade`

**Cel:** Opanowanie cyklu życia migracji oraz metod zarządzania bazą danych udostępnianych przez właściwość `context.Database`.

#### 1. Anatomia migracji w EF Core

Wykonanie polecenia `dotnet ef migrations add InitialCreate` (lub `Add-Migration InitialCreate`) nie modyfikuje jeszcze bazy danych, lecz generuje w katalogu `Migrations` pliki C#:

- `<YYYYMMDDHHMMSS>_InitialCreate.cs` – zawiera klasę dziedziczącą po `Migration` z dwiema metodami:
  - `Up(MigrationBuilder migrationBuilder)` – instrukcje tworzące lub modyfikujące tabele, kolumny (w tym *Shadow Properties* oraz spłaszczone kolumny `OwnsOne`) i indeksy przy aplikowaniu migracji,
  - `Down(MigrationBuilder migrationBuilder)` – instrukcje cofające zmiany w przypadku wycofywania migracji.
- `<NazwaKontekstu>ModelSnapshot.cs` – pełna migawka bieżącego stanu modelu C#. Stanowi „źródło prawdy”, z którym EF Core porównuje kod klas podczas generowania kolejnej migracji.

#### 2. Wzorzec Fasada (`context.Database`) i aplikowanie zmian w czasie uruchomienia

Właściwość `context.Database` (typu `DatabaseFacade`) realizuje wzorzec projektowy **Fasada**, wystawiając uproszczony interfejs do zarządzania fizyczną bazą danych:

- **`context.Database.Migrate()` / `MigrateAsync()` (Wymagane w zadaniu projektowym):** Tworzy bazę danych (jeśli nie istnieje), sprawdza tabelę historii `__EFMigrationsHistory` i wykonuje wszystkie oczekujące migracje.

  ``` csharp
  using (var scope = host.Services.CreateScope())
  {
      var dbContext = scope.ServiceProvider.GetRequiredService<UniversityDbContext>();
      await dbContext.Database.MigrateAsync();
  }Kopiuj
  ```

- **`context.Database.EnsureCreated()`:** Tworzy bazę i schemat bezpośrednio z modelu, ale **nie tworzy tabeli `__EFMigrationsHistory`** i nie używa migracji. Baza utworzona przez `EnsureCreated()` nie może być później aktualizowana migracjami (**metody `Migrate()` oraz `EnsureCreated()` wykluczają się wzajemnie!**).

------------------------------------------------------------------------

### 9. Operacje CRUD, *Shadow Property*, Transakcje Sekwencji i Generator (`Bogus`)

**Cel:** Realizacja pełnego cyklu CRUD (w tym aktualizacji niemodyfikowalnych obiektów wartości i *Shadow Properties*) w ramach jawnych transakcji bazodanowych oraz generowanie realistycznych danych testowych za pomocą biblioteki `Bogus`.

#### 1. Transakcje jawne (`BeginTransactionAsync`) vs automatyczne (`SaveChanges`)

Domyślnie każde pojedyncze wywołanie `context.SaveChangesAsync()` jest automatycznie opakowywane w transakcję bazodanową. Jawnej transakcji (`context.Database.BeginTransactionAsync()`) używamy wtedy, gdy jedna atomowa operacja biznesowa wymaga:

1.  Odczytu stanu z bazy i podjęcia decyzji w kodzie C# (np. pobrania i inkrementacji aktualnego stanu licznika z tabeli `LicznikiIndeksow` lub sprawdzenia przy usuwaniu, czy usuwany obiekt ma najwyższy numer),
2.  Wykonania zależnych operacji zapisu,
3.  Gwarancji pełnej izolacji ACID, aby w przypadku błędu zarówno dodanie/usunięcie studenta, jak i zmiana licznika zostały w całości wycofane (*Rollback*).

#### 2. Kompletny wzorzec serwisu aplikacyjnego: `Create`, `Update` (z podmianą `Adres` i *Shadow Property*) oraz `Delete`

Poniższy przykład pokazuje kompletną realizację wymagań Części 2 zadania projektowego w klasie `StudentService`:

``` csharp
using Microsoft.EntityFrameworkCore;
using University.Domain.Entities;
using University.Domain.ValueObjects;
using University.Infrastructure.Persistence;

namespace University.Application.Services;

public class StudentService
{
    private readonly UniversityDbContext _context;

    public StudentService(UniversityDbContext context)
    {
        _context = context;
    }

    // 1. CREATE: Transakcyjne nadanie indeksu + zapis Shadow Property "OstatniaModyfikacja"
    public async Task<Student> CreateStudentAsync(
        string imie, string nazwisko, int rokStudiow, Adres adres, string prefix = "S")
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var licznik = await _context.LicznikiIndeksow
                .FirstOrDefaultAsync(l => l.Prefix == prefix)
                ?? throw new InvalidOperationException($"Brak zdefiniowanego licznika dla prefiksu '{prefix}'.");

            // Inkrementacja licznika (Wariant A: AktualnaWartosc przechowuje ostatnio nadany numer)
            licznik.AktualnaWartosc++;
            string nadanyIndeks = $"{licznik.Prefix}{licznik.AktualnaWartosc}";

            var student = new Student
            {
                Imie = imie,
                Nazwisko = nazwisko,
                RokStudiow = rokStudiow,
                IndeksUczelniany = nadanyIndeks,
                AdresZamieszkania = adres
            };

            _context.Studenci.Add(student);

            // Ustawienie wartości właściwości ukrytej (Shadow Property) w ChangeTrackerze:
            _context.Entry(student).Property("OstatniaModyfikacja").CurrentValue = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return student;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // 2. UPDATE: Modyfikacja encji śledzonej (Connected), podmiana Value Objectu Adres i Shadow Property
    public async Task UpdateStudentAsync(int studentId, string noweNazwisko, int nowyRok, Adres nowyAdres)
    {
        var student = await _context.Studenci
            .FirstOrDefaultAsync(s => s.Id == studentId)
            ?? throw new KeyNotFoundException($"Nie znaleziono studenta o Id = {studentId}.");

        // Modyfikacja właściwości encji POCO
        student.Nazwisko = noweNazwisko;
        student.RokStudiow = nowyRok;

        // Ponieważ Adres jest niemodyfikowalnym Obiektem Wartości (record z akcesorami init),
        // nie modyfikujemy jego pojedynczych pól, lecz przypisujemy nową, zwalidowaną instancję:
        student.AdresZamieszkania = nowyAdres;

        // Aktualizacja Shadow Property "OstatniaModyfikacja" w ChangeTrackerze:
        _context.Entry(student).Property("OstatniaModyfikacja").CurrentValue = DateTime.UtcNow;

        // ChangeTracker sam wykryje zmiany (Dirty Checking) i wyśle UPDATE
        await _context.SaveChangesAsync();
    }

    // 3. DELETE: Warunkowe cofnięcie licznika w transakcji przy usunięciu ostatniego numeru
    public async Task DeleteStudentAsync(int studentId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var student = await _context.Studenci.FirstOrDefaultAsync(s => s.Id == studentId)
                ?? throw new KeyNotFoundException($"Nie znaleziono studenta o Id = {studentId}.");

            // Wyodrębniamy prefiks (np. "S") oraz numer (np. 1002) z IndeksUczelniany ("S1002")
            string indeks = student.IndeksUczelniany;
            string prefix = new string(indeks.TakeWhile(char.IsLetter).ToArray());
            string numerStr = new string(indeks.SkipWhile(char.IsLetter).ToArray());

            if (int.TryParse(numerStr, out int numerStudenta))
            {
                var licznik = await _context.LicznikiIndeksow
                    .FirstOrDefaultAsync(l => l.Prefix == prefix);

                // Jeśli usuwany student posiada dokładnie najwyższy aktualnie nadany numer,
                // cofamy licznik o 1, aby uniknąć luki na końcu sekwencji:
                if (licznik is not null && licznik.AktualnaWartosc == numerStudenta)
                {
                    licznik.AktualnaWartosc--;
                }
            }

            _context.Studenci.Remove(student);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}Kopiuj
```

#### 3. Generowanie realistycznych danych testowych z biblioteką `Bogus`

Biblioteka **Bogus** (`dotnet add package Bogus`) pozwala generować realistyczne polskie dane osobowe, adresowe i akademickie (`new Faker("pl")`). W czystej architekturze generator **musi tworzyć adresy przez walidujący konstruktor `new Adres(...)` oraz wywoływać metodę serwisową przydzielającą indeksy z `LicznikIndeksow`**:

``` csharp
using Bogus;
using University.Domain.ValueObjects;

// Definicja reguł generowania obiektu wartości Adres w polskiej lokalizacji ("pl")
// Format "##-###" gwarantuje przejście walidacji Regex w konstruktorze Adres!
var adresFaker = new Faker<Adres>("pl")
    .CustomInstantiator(f => new Adres(
        ulica: f.Address.StreetAddress(),
        miasto: f.Address.City(),
        kodPocztowy: f.Address.ZipCode("##-###")
    ));

var personFaker = new Faker("pl");
for (int i = 0; i < 15; i++)
{
    await studentService.CreateStudentAsync(
        imie: personFaker.Name.FirstName(),
        nazwisko: personFaker.Name.LastName(),
        rokStudiow: personFaker.Random.Int(1, 5),
        adres: adresFaker.Generate(),
        prefix: "S"
    );
}Kopiuj
```

------------------------------------------------------------------------

### 10. Wydajne Zapytania LINQ to Entities, Deduplikacja (`.Union()`) i Diagnostyka SQL

**Cel:** Opanowanie pisania złożonych zapytań analitycznych w LINQ, eliminacja duplikatów w podzapytaniach oraz weryfikacja wygenerowanego kodu SQL za pomocą `.ToQueryString()`.

Właściwości `DbSet<TEntity>` implementują interfejs **`IQueryable<TEntity>`**. Oznacza to, że zapytania LINQ nie są kompilowane przez kompilator Roslyn do zwykłych delegatów CIL (`Func<T, bool>`), lecz do struktur danych **Drzew Wyrażeń (`Expression<Func<T, bool>>` – AST)**, które dostawca EF Core tłumaczy na dialekt SQL w momencie materializacji (np. przy wywołaniu `.ToListAsync()`, `.FirstOrDefaultAsync()`).

#### Cztery filary optymalizacji zapytań w EF Core

##### 1. Zapytania tylko do odczytu: `.AsNoTracking()`

W przypadku zapytań raportowych, analitycznych i widoków list (gdzie tylko wyświetlamy dane) dodanie `.AsNoTracking()` pomija alokację struktur `InternalEntityEntry` oraz snapshotów w `ChangeTrackerze`, przyspieszając materializację o 20–40%:

``` csharp
var studenci = await _context.Studenci
    .AsNoTracking()
    .Where(s => s.RokStudiow >= 2)
    .ToListAsync();Kopiuj
```

##### 2. Projekcja danych do DTO: `.Select(x => new WynikDto { ... })`

Zamiast pobierać z bazy wszystkie kolumny całych encji (`SELECT *`), używaj operatora `.Select()`, aby pobrać wyłącznie potrzebne pola lub wyliczyć agregacje (`COUNT`, `AVG`, `SUM`) bezpośrednio w silniku SQL:

``` csharp
// Przykład (Zapytanie 2): Średnia ocen (GPA) i liczba ocenionych studentów dla kursów na wydziale
var statystykiKursow = await _context.Kursy
    .AsNoTracking()
    .Where(k => k.WydzialId == wybranyWydzialId)
    .Select(k => new KursGpaDto
    {
        KodKursu = k.KodKursu,
        NazwaKursu = k.Nazwa,
        LiczbaOcenionychStudentow = k.Zapisy.Count(z => z.Ocena != null),
        SredniaOcen = k.Zapisy
            .Where(z => z.Ocena != null)
            .Average(z => (double?)z.Ocena) ?? 0.0
    })
    .ToListAsync();Kopiuj
```

*(Uwaga: Gdy stosujesz projekcję `.Select(...)` do obiektu DTO, nie musisz wywoływać `.Include()` – EF Core sam wygeneruje odpowiednie podzapytania SQL na podstawie ścieżek nawigacji użytych wewnątrz `.Select()`!).*

##### 3. Ewaluacja po stronie serwera (*Server-Side*) vs w pamięci (*Client-Side Evaluation*)

Wszystkie operacje filtrowania (`Where`), sortowania (`OrderBy`), grupowania (`GroupBy`) i agregacji (`Sum`, `Average`, `Count`) **muszą znajdować się PRZED wywołaniem `.ToList()` / `.ToListAsync()`**.

> **⚠️ PUŁAPKA TRANSLACJI LINQ: `StringComparison` oraz własne metody C# wewnątrz `.Where()`**  
> Użycie wewnątrz zapytania `IQueryable` przeciążenia `s.Nazwisko.StartsWith(filtr, StringComparison.InvariantCultureIgnoreCase)` lub własnej metody pomocniczej C# spowoduje rzucenie wyjątku:  
> *`System.InvalidOperationException: The LINQ expression ... could not be translated.`*  
> Serwer SQL nie zna enumeracji `StringComparison` z platformy .NET! Zawsze używaj podstawowego `s.Nazwisko.StartsWith(filtr)` lub funkcji `EF.Functions.Like(s.Nazwisko, $"{filtr}%")`.

##### 4. Eliminacja duplikatów przy złączeniach wielopoziomowych (`.Union()` przed `.Sum()`)

Gdy obliczamy sumę wartości (np. punktów ECTS w Zapytaniu 3 zadania projektowego) po złączeniu dwóch różnych kolekcji: kursów, na które zapisany jest student (`s.Zapisy.Select(z => z.Kurs)`), oraz kursów będących ich wymaganiami wstępnymi (`s.Zapisy.SelectMany(z => z.Kurs.WymaganiaWstepne)`), ten sam kurs może pojawić się wielokrotnie.

Aby serwer SQL wyeliminował duplikaty kursów przed zsumowaniem punktów ECTS, łączymy oba zbiory operatorem **`.Union()`** (tłumaczonym na relacyjny operator `UNION` usuwający powtórzenia):

``` csharp
// Przykład (Zapytanie 3): Student z najtrudniejszym planem zajęć (deduplikacja przez Union)
var najtrudniejszyPlan = await _context.Studenci
    .AsNoTracking()
    .Select(s => new StudentTrudnoscDto
    {
        Indeks = s.IndeksUczelniany,
        ImieNazwisko = $"{s.Imie} {s.Nazwisko}",
        LiczbaZapisanychKursow = s.Zapisy.Count(),
        LacznyKosztECTS = s.Zapisy.Select(z => z.Kurs)
            .Union(s.Zapisy.SelectMany(z => z.Kurs.WymaganiaWstepne))
            .Sum(k => (int?)k.PunktyECTS) ?? 0
    })
    .OrderByDescending(dto => dto.LacznyKosztECTS)
    .FirstOrDefaultAsync();Kopiuj
```

#### 5. Diagnostyka i podgląd wygenerowanego SQL (`.ToQueryString()` oraz `.TagWith()`)

Przed oddaniem zadania warto zweryfikować, jaki kod SQL generują napisane zapytania LINQ. EF Core udostępnia metodę rozszerzającą **`.ToQueryString()`**, którą można wywołać na dowolnym obiekcie `IQueryable<T>` (bez wykonywania zapytania w bazie!):

``` csharp
var query = _context.Profesorowie
    .TagWith("Zapytanie 1: Top Profesor wg liczby studentow")
    .AsNoTracking()
    .Select(p => new TopProfesorDto
    {
        Indeks = p.IndeksUczelniany,
        PelneImieNazwisko = $"{p.TytulNaukowy} {p.Imie} {p.Nazwisko}",
        LiczbaProwadzonychKursow = p.ProwadzoneKursy.Count(),
        LacznaLiczbaStudentow = p.ProwadzoneKursy.SelectMany(k => k.Zapisy).Count()
    })
    .OrderByDescending(dto => dto.LacznaLiczbaStudentow);

// Podgląd wygenerowanego zapytania SQL w konsoli lub w oknie Watch debuggera:
string wygenerowanySql = query.ToQueryString();
Console.WriteLine(wygenerowanySql);

var wynik = await query.FirstOrDefaultAsync();Kopiuj
```

------------------------------------------------------------------------

### 11. Diagnostyka i Rozwiązywanie Najczęstszych Błędów (Troubleshooting)

**Cel:** Szybka identyfikacja i naprawa najczęstszych wyjątków napotykanych podczas pracy z Entity Framework Core.

#### 1. `System.InvalidOperationException: No database provider has been configured for this DbContext`

- **Przyczyna:** Instancja `DbContext` nie wie, z jakiego silnika bazy danych ma korzystać.
- **Rozwiązanie:**
  1.  Sprawdź, czy w `Program.cs` wywołano `builder.Services.AddDbContext<MojDbContext>(options => options.UseSqlServer(...))`.
  2.  Upewnij się, że klasa `MojDbContext` posiada konstruktor przyjmujący `DbContextOptions<MojDbContext>` i przekazuje go do konstruktora bazowego `: base(options)`.

#### 2. `System.InvalidOperationException: The entity type '<NazwaKlasy>' requires a primary key to be defined`

- **Przyczyna:** EF Core nie odnalazł klucza głównego dla encji, albo rekord będący Obiektem Wartości (`Adres`) został omyłkowo zarejestrowany jako zwykła encja.
- **Rozwiązanie:**
  1.  Jeśli to zwykła encja (np. `LicznikIndeksow` z kluczem `Prefix`), skonfiguruj klucz główny we Fluent API: `.HasKey(l => l.Prefix)`.
  2.  Jeśli to Obiekt Wartości (`Adres`), skonfiguruj go przez `.OwnsOne(s => s.AdresZamieszkania)` (lub `.ComplexProperty(s => s.AdresZamieszkania)`).

#### 3. `System.InvalidOperationException: The property 'OstatniaModyfikacja' on entity type 'Student' could not be found`

- **Przyczyna:** Próba zapisu lub odczytu właściwości ukrytej (*Shadow Property*) przez `_context.Entry(s).Property("OstatniaModyfikacja")` lub `EF.Property<DateTime>(s, "OstatniaModyfikacja")`, która nie została wcześniej zarejestrowana w `OnModelCreating`.
- **Rozwiązanie:** Upewnij się, że w konfiguracji Fluent API encji `Student` oraz `Profesor` znajduje się deklaracja `builder.Property<DateTime>("OstatniaModyfikacja");` i wygenerowano dla niej migrację.

#### 4. `SqlException: Invalid object name '<NazwaTabeli>'` (lub `SQLite Error 1: 'no such table'`)

- **Przyczyna:** Aplikacja próbuje wykonać zapytanie do tabeli, która nie została jeszcze utworzona w bazie danych.
- **Rozwiązanie:** Wygeneruj migrację (`dotnet ef migrations add ...` / `Add-Migration ...`) i zaktualizuj bazę danych (`dotnet ef database update` / `Update-Database`) lub wywołaj `await context.Database.MigrateAsync()` przy starcie aplikacji.

#### 5. `SqlException: Introducing FOREIGN KEY constraint ... may cause cycles or multiple cascade paths` (Błąd 1785)

- **Przyczyna:** Silnik Microsoft SQL Server blokuje tworzenie związków typu „diament”, w których do jednej tabeli prowadzą dwie różne ścieżki kaskadowego usuwania (`ON DELETE CASCADE` lub `SET NULL`).
- **Rozwiązanie:** W metodzie `OnModelCreating` zmień zachowanie przy usuwaniu dla jednej ze ścieżek związku (np. `Wydzial` <span class="math inline">\\\rightarrow\\</span> `Kurs`) na `.OnDelete(DeleteBehavior.Restrict)`.

#### 6. `SqlException: Cannot attach the file '...mdf' as database '...' because this file is already in use`

- **Przyczyna:** Zewnętrzny proces (np. otwarte połączenie w oknie *SQL Server Object Explorer* w Visual Studio lub poprzednia zawieszona instancja aplikacji) blokuje plik bazy danych `.mdf`.
- **Rozwiązanie:** Zamknij aktywne połączenie w oknie eksploratora baz danych (PPM na bazie → *Close Connection* / *Detach*) lub korzystaj ze standardowego połączenia do serwera LocalDB (`Server=(localdb)\\mssqllocaldb;Database=NazwaBazy;Trusted_Connection=True;`) bez ręcznego wskazywania ścieżki `AttachDbFilename`.
