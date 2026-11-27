# Programowanie Aplikacji Lokalnych

**Rok akademicki:** 2026/27

## Zadanie laboratoryjne: System Zarządzania Uczelnią "UniversityCleanArch" (Entity Framework Core, Clean Architecture, DDD, Transakcje i Optymalizacja LINQ)

**Prowadzący:** Tomasz Goluch  
**Wersja:** 1.1  
**Wymagany standard:** C# 12+ / .NET 8 LTS, .NET 9 lub .NET 10 LTS  
**Maksymalna ocena zadania:** 120 punktów (120%)  
**Forma realizacji:** Projekt tworzony **od zera (Greenfield)** w oparciu o **Czystą Architekturę (Clean Architecture)**

------------------------------------------------------------------------

### 1. Kontekst Biznesowy i Architektoniczny Projektu

Celem zadania jest zaprojektowanie i zaimplementowanie wielowarstwowej aplikacji obsługującej procesy dziekanatowe uczelni wyższej – **`UniversityCleanArch`**. System umożliwia zarządzanie studentami (w tym dyplomantami studiów magisterskich), kadrą profesorską, wydziałami, siatką kursów wraz z wymaganiami wstępnymi (*prerequisites*) oraz zapisami semestralnymi.

W systemach klasy Enterprise opartych o relacyjne bazy danych kluczowe wyzwania obejmują:

- Zachowanie czystości modelu domenowego (**Persistence Ignorance** w podejściu **DDD**), tak aby encje biznesowe POCO i niemodyfikowalne obiekty wartości (*Value Objects*) pilnowały własnych inwariantów i nie były sprzężone z bibliotekami dostawcy bazy danych ani technicznymi polami audytowymi (**Shadow Properties**),
- Zapewnienie atomowości i spójności niestandardowych sekwencji numeracyjnych (np. generowanie unikalnych indeksów uczelnianych `"S1001"`, `"P101"` oraz cofanie licznika przy kasowaniu ostatniego rekordu w transakcji),
- Poprawne odwzorowanie zaawansowanych związków relacyjnych (`1:1`, `1:N`, `N:M` z dodatkowym ładunkiem danych, związków samoodwołujących się *self-referencing*, encji posiadanych *Owned Entities* oraz dziedziczenia) wraz z bezpieczną polityką usuwania (`DeleteBehavior.SetNull`),
- Unikanie pułapek wydajnościowych mappera ORM (problemu <span class="math inline">\\N+1\\</span> zapytań, eksplozji kartezjańskiej, śledzenia encji w zapytaniach raportowych oraz ewaluacji po stronie klienta – *Client-Side Evaluation*).

Realizując wszystkie wymagania zadania, można zdobyć **120 punktów (120%)**. Aplikacja musi zostać podzielona na cztery ściśle odseparowane warstwy (projekty): **`Domain`**, **`Application`**, **`Infrastructure`** oraz **`UI`**.

------------------------------------------------------------------------

### 2. Inicjalizacja Środowiska (Krok 0: Przygotowanie Solucji Clean Architecture od Zera)

Otwórz terminal w pustym katalogu roboczym i wykonaj poniższy skrypt `.NET CLI`, aby utworzyć kompletną strukturę solucji wraz z powiązaniami między projektami i wymaganymi pakietami NuGet (możesz dostosować flagę `-f net8.0` do `net9.0` lub `net10.0`):

``` bash
# 1. Utworzenie pliku rozwiązania (Solution)
dotnet new sln -n UniversityCleanArch

# 2. Utworzenie 4 projektów odpowiadających warstwom Clean Architecture
dotnet new classlib -n University.Domain -f net8.0
dotnet new classlib -n University.Application -f net8.0
dotnet new classlib -n University.Infrastructure -f net8.0
dotnet new console -n University.UI -f net8.0

# 3. Dodanie projektów do solucji
dotnet sln add University.Domain/University.Domain.csproj
dotnet sln add University.Application/University.Application.csproj
dotnet sln add University.Infrastructure/University.Infrastructure.csproj
dotnet sln add University.UI/University.UI.csproj

# 4. Konfiguracja referencji zgodnie z Regułą Zależności (Dependency Rule)
# Application zależy wyłącznie od Domain:
dotnet add University.Application/University.Application.csproj reference University.Domain/University.Domain.csproj

# Infrastructure zależy od Domain oraz Application:
dotnet add University.Infrastructure/University.Infrastructure.csproj reference University.Domain/University.Domain.csproj
dotnet add University.Infrastructure/University.Infrastructure.csproj reference University.Application/University.Application.csproj

# UI (projekt startowy) spina warstwy Application oraz Infrastructure:
dotnet add University.UI/University.UI.csproj reference University.Domain/University.Domain.csproj
dotnet add University.UI/University.UI.csproj reference University.Application/University.Application.csproj
dotnet add University.UI/University.UI.csproj reference University.Infrastructure/University.Infrastructure.csproj

# 5. Instalacja pakietów NuGet w odpowiednich warstwach
dotnet add University.Application/University.Application.csproj package Microsoft.EntityFrameworkCore
dotnet add University.Application/University.Application.csproj package Bogus

dotnet add University.Infrastructure/University.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add University.Infrastructure/University.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools

dotnet add University.UI/University.UI.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add University.UI/University.UI.csproj package Microsoft.Extensions.HostingKopiuj
```

> **💡 Alternatywa dla interfejsu użytkownika i bazy danych:**
>
> - Zamiast aplikacji konsolowej (`console` / `Terminal.Gui` / `Spectre.Console`), projekt **`University.UI`** może zostać utworzony jako aplikacja okienkowa **WPF** (`dotnet new wpf -n University.UI`).
> - Domyślnym dostawcą bazy danych jest **Microsoft SQL Server (LocalDB)** (`Microsoft.EntityFrameworkCore.SqlServer`), jednak dopuszczalne jest również zastosowanie bazy **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`) lub **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`).

------------------------------------------------------------------------

### 3. Szczegółowe Wymagania i Podział Punktów (Łącznie: 120 pkt / 120%)

``` text
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                 ARCHITEKTURA SYSTEMU UNIVERSITYCLEANARCH (EF CORE)                     │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  [Część 1: 25 pkt] Biblioteka Domain         ──► Encje POCO, VO Adres, Związki, Dziedz.│
│  [Część 2: 35 pkt] Biblioteka Application    ──► CRUD, Shadow Prop, Transakcje, Bogus  │
│  [Część 3: 20 pkt] Biblioteka Infrastructure ──► DbContext, OwnsOne, Shadow, SetNull   │
│  [Część 4: 20 pkt] Zapytania (LINQ / SQL)    ──► Projekcja DTO, AsNoTracking, 3 Kwer.  │
│  [Część 5: 20 pkt] Interfejs Użytkownika UI  ──► Generator, Przegląd, Kasowanie, Raport│
└────────────────────────────────────────────────────────────────────────────────────────┘Kopiuj
```

------------------------------------------------------------------------

#### Część 1: Biblioteka `Domain` – Klasy POCO, Obiekt Wartości (`Adres`), Związki i Dziedziczenie (25 pkt)

W projekcie `University.Domain` zaimplementuj czysty model obiektowy reprezentujący strukturę uczelni. Warstwa ta nie może zawierać zależności od konkretnego silnika bazy danych ani technicznych kolumn infrastrukturalnych.

##### Wymagania szczegółowe:

1.  **\[20 pkt\] Główne encje uczelniane (POCO), Obiekt Wartości i Związki:**
    - **Osoby:**
      - **`Student`:** `Id` (int), `Imie` (string), `Nazwisko` (string), `IndeksUczelniany` (string – unikalny identyfikator biznesowy, np. `"S1001"`), `RokStudiow` (int), `AdresZamieszkania` (`Adres`).
      - **`Profesor`:** `Id` (int), `Imie` (string), `Nazwisko` (string), `IndeksUczelniany` (string – unikalny identyfikator biznesowy, np. `"P101"`), `TytulNaukowy` (string, np. `"dr inż."`, `"prof. dr hab."`), `AdresZamieszkania` (`Adres`).
    - **Struktura dydaktyczna:**
      - **`Wydzial`:** `Id` (int), `Nazwa` (string, np. `"Wydział Elektroniki, Telekomunikacji i Informatyki"`).
      - **`Kurs`:** `Id` (int), `Nazwa` (string), `KodKursu` (string, np. `"ETI-EF-01"`), `PunktyECTS` (int).
      - **`Gabinet` (Office):** `Id` (int), `NumerPokoju` (string, np. `"EA-312"`), `Budynek` (string).
    - **Obiekt Wartości (Value Object – wzorzec *Whole Value Pattern*):**
      - **`Adres`:** Niemodyfikowalny rekord (`public record Adres`) posiadający właściwości `Ulica`, `Miasto`, `KodPocztowy` i spełniający reguły taktycznego DDD:
        1.  **Niemodyfikowalność (*Immutability*):** Właściwości mogą posiadać wyłącznie akcesory `init;` (brak publicznych setterów `set;`).
        2.  **Ochrona inwariantów i normalizacja w konstruktorze:** Konstruktor publiczny `Adres(string ulica, string miasto, string kodPocztowy)` musi walidować dane wejściowe (rzucać `ArgumentException`, gdy którekolwiek pole jest puste lub składa się z białych znaków, oraz weryfikować, czy `kodPocztowy` po przycięciu ma poprawny format/długość, np. `XX-XXX` lub min. 5 znaków) i normalizować ciągi znaków metodą `.Trim()`.
        3.  **Wsparcie dla ORM i reprezentacja tekstowa:** Rekord musi posiadać prywatny konstruktor bezparametrowy `private Adres() { }` na potrzeby materializacji EF Core oraz nadpisaną metodę `ToString()`.
      - Encje `Student` oraz `Profesor` muszą zawierać ten rekord jako swoją właściwość (`AdresZamieszkania`).
    - **Encja Sekwencji (Numeracja):**
      - **`LicznikIndeksow`:** Dedykowana encja przechowująca aktualny stan liczników dla poszczególnych prefiksów:
        - `Prefix` (`string`, **Klucz główny**, np. `"S"`, `"P"`, `"D"`),
        - `AktualnaWartosc` (`int`, np. `1001`).
    - **Wymagane związki między encjami i właściwości nawigacyjne:**
      - **Zapisy (`Enrollment`):** Związek **wiele-do-wielu z dodatkowymi danymi** (`Student` <span class="math inline">\\\leftrightarrow\\</span> `Kurs`). Jawna encja łącząca `Enrollment` przechowuje klucze `StudentId` i `KursId` oraz dodatkowe właściwości biznesowe: `Ocena` (`double?` lub `decimal?` – dopuszcza `null` przed wystawieniem oceny) oraz `Semestr` (`string`, np. `"2026/27-Z"`).
      - **Wymagania wstępne (`Prerequisites`):** Związek **wiele-do-wielu typu samoodwołującego się (*self-referencing*)** na encji `Kurs` – jeden kurs może posiadać wiele kursów wymaganych (`WymaganiaWstepne`) i jednocześnie być wymaganiem wstępnym dla wielu innych kursów (`KursyZalezne`).
      - **Gabinet (`Office`):** Związek **jeden-do-jednego** (`Profesor` <span class="math inline">\\\leftrightarrow\\</span> `Gabinet`).
      - **Powiązania wydziałowe i prowadzenie kursów:**
        - Związek **jeden-do-wielu** (`Wydzial` <span class="math inline">\\\leftrightarrow\\</span> `Kurs`) – każdy kurs jest przypisany do wydziału (`WydzialId`, `Wydzial`).
        - Związek **jeden-do-wielu** (`Profesor` <span class="math inline">\\\leftrightarrow\\</span> `Kurs`) – każdy kurs posiada przypisanego profesora prowadzącego (`ProwadzacyId`, `Prowadzacy`), a profesor posiada kolekcję `ProwadzoneKursy`.
2.  **\[5 pkt\] Podencja (Dziedziczenie):**
    - Zaimplementuj klasę pochodną **`StudentStudiowMagisterskich`** dziedziczącą po klasie `Student`.
    - Klasa musi posiadać dodatkowe właściwości:
      - `TematPracyDyplomowej` (`string`),
      - Opcjonalny związek do promotora: klucz obcy `PromotorId` (`int?`) oraz właściwość nawigacyjną `Promotor` (`Profesor?`). W klasie `Profesor` dodaj odpowiadającą kolekcję nawigacyjną `Dyplomanci` (`ICollection<StudentStudiowMagisterskich>`).

**💡 Wskazówki implementacyjne: Obiekt Wartości Adres z walidacją oraz encje Kurs i StudentStudiowMagisterskich (Rozwiń)**

``` csharp
using System.Text.RegularExpressions;

namespace University.Domain.ValueObjects;

public record Adres
{
    public string Ulica { get; init; } = string.Empty;
    public string Miasto { get; init; } = string.Empty;
    public string KodPocztowy { get; init; } = string.Empty;

    // Prywatny konstruktor bezparametrowy dla materializacji przez EF Core
    private Adres() { }

    public Adres(string ulica, string miasto, string kodPocztowy)
    {
        if (string.IsNullOrWhiteSpace(ulica))
            throw new ArgumentException("Ulica nie może być pusta.", nameof(ulica));

        if (string.IsNullOrWhiteSpace(miasto))
            throw new ArgumentException("Miasto nie może być puste.", nameof(miasto));

        if (string.IsNullOrWhiteSpace(kodPocztowy))
            throw new ArgumentException("Kod pocztowy nie może być pusty.", nameof(kodPocztowy));

        var znormalizowanyKod = kodPocztowy.Trim();
        if (!Regex.IsMatch(znormalizowanyKod, @"^\d{2}-\d{3}$"))
            throw new ArgumentException("Kod pocztowy musi mieć format XX-XXX (np. 80-233).", nameof(kodPocztowy));

        Ulica = ulica.Trim();
        Miasto = miasto.Trim();
        KodPocztowy = znormalizowanyKod;
    }

    public override string ToString() => $"{Ulica}, {KodPocztowy} {Miasto}";
}Kopiuj
```

``` csharp
namespace University.Domain.Entities;

public class StudentStudiowMagisterskich : Student
{
    public string TematPracyDyplomowej { get; set; } = string.Empty;

    // Klucz obcy musi być nullowalny (int?), aby umożliwić działanie DeleteBehavior.SetNull!
    public int? PromotorId { get; set; }
    public Profesor? Promotor { get; set; }
}

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

    // Związek Wiele-do-Wielu Self-Referencing:
    public ICollection<Kurs> WymaganiaWstepne { get; set; } = new HashSet<Kurs>();
    public ICollection<Kurs> KursyZalezne { get; set; } = new HashSet<Kurs>();
}Kopiuj
```

------------------------------------------------------------------------

#### Część 2: Biblioteka `Application` – CRUD, Obsługa *Shadow Property*, Transakcyjna Numeracja i `Bogus` (35 pkt)

W projekcie `University.Application` zaimplementuj serwisy aplikacyjne realizujące operacje CRUD, obsługę właściwości ukrytej (*Shadow Property*), transakcyjne przydzielanie i cofanie numerów indeksów oraz generator danych testowych.

##### Wymagania szczegółowe:

1.  **\[15 pkt\] Operacje CRUD, Zapis *Shadow Property* i Transakcyjna Logika Numeracji (`LicznikIndeksow`):**
    - **Operacje CRUD i obsługa *Shadow Property*:**
      - Zaimplementuj operacje tworzenia, odczytu, aktualizacji i usuwania dla głównych encji systemu (`Student`, `StudentStudiowMagisterskich`, `Profesor`, `Kurs`, `Wydzial`) oraz pełne zarządzanie encją `LicznikIndeksow` (umożliwiające m.in. dodanie nowego prefiksu, np. `"D"` dla doktorantów z wartością początkową `0`).
      - Podczas tworzenia (`Create`) oraz modyfikacji (`Update`) obiektów `Student` oraz `Profesor` zaktualizuj za pomocą `ChangeTrackera` wartość zdefiniowanej w warstwie `Infrastructure` właściwości ukrytej (**Shadow Property**) `"OstatniaModyfikacja"`: `_context.Entry(encja).Property("OstatniaModyfikacja").CurrentValue = DateTime.UtcNow;`
    - **Logika Tworzenia (Przydzielanie Indeksu w Transakcji):**
      - Operacje tworzenia `Studenta` (prefiks `"S"`) oraz `Profesora` (prefiks `"P"`) nie przyjmują gotowego numeru indeksu z zewnątrz, lecz automatycznie wyznaczają go na podstawie encji `LicznikIndeksow`.
      - Cała operacja (pobranie licznika dla prefiksu, zwiększenie wartości o `1`, przypisanie sformatowanego indeksu np. `"S1001"` nowemu obiektowi, ustawienie *Shadow Property* `"OstatniaModyfikacja"` oraz zapis zmian w bazie) **musi zostać wykonana w ramach jawnej transakcji bazodanowej (`BeginTransaction` / `BeginTransactionAsync`)**, aby zagwarantować spójność danych.
    - **Logika Usuwania (Warunkowe Cofanie Licznika w Transakcji):**
      - Operacja usuwania `Studenta` lub `Profesora` musi sprawdzać, czy usuwany obiekt posiada **najwyższy aktualnie nadany numer** dla swojego prefiksu.
      - Jeśli tak, operacja musi zdekrementować licznik o `1` w tabeli `LicznikIndeksow`, aby uniknąć powstawania dziur na końcu sekwencji. Operacja usunięcia encji i dekrementacji licznika również **musi być częścią jednej transakcji**.
2.  **\[20 pkt\] Generator Danych Testowych (`Bogus`):**
    - Zaimplementuj klasę generatora (np. `DataSeeder` / `UniversityBogusGenerator`) wykorzystującą bibliotekę **Bogus** (`Faker`) do wygenerowania spójnego zestawu danych uczelnianych (wydziały, profesorowie z gabinetami, studenci inżynierscy i magisterscy z przypisanymi promotorami, kursy z wymaganiami wstępnymi oraz zapisy `Enrollment` z ocenami).
    - **Kluczowy wymóg architektoniczny:** Podczas tworzenia fałszywych studentów i profesorów generator **musi korzystać z logiki biznesowej warstwy `Application`** do pozyskiwania kolejnych numerów `IndeksUczelniany` z tabeli `LicznikIndeksow` (oraz konstruktora walidującego rekord `Adres`), zamiast generować losowe ciągi znaków indeksów przez `Faker`.

**💡 Wskazówki implementacyjne: Spójność konwencji licznika (Pre-increment vs Post-increment) (Rozwiń)**

W zadaniu dopuszczalne są dwa równoważne warianty interpretacji pola `AktualnaWartosc` w `LicznikIndeksow` – wybierz jeden z nich i stosuj go konsekwentnie przy tworzeniu i usuwaniu:

- **Wariant A (`AktualnaWartosc` oznacza ostatnio nadany numer – zalecany):**
  - Wartość początkowa dla nowego prefiksu `"S"` wynosi `1000` (lub `0` dla `"D"`).
  - **Tworzenie:** Pobierz licznik (`1000`), zwiększ `AktualnaWartosc` na `1001` i przypisz `"S1001"`. Przy kolejnym studencie zwiększ z `1001` na `1002` i przypisz `"S1002"`.
  - **Usuwanie:** Jeśli usuwasz studenta `"S1002"`, a `AktualnaWartosc == 1002`, zmniejsz `AktualnaWartosc` z powrotem na `1001`.
- **Wariant B (`AktualnaWartosc` oznacza następny wolny numer do nadania):**
  - Wartość początkowa dla prefiksu `"S"` wynosi `1001`.
  - **Tworzenie:** Pobierz licznik, przypisz studentowi bieżącą wartość `"S1001"`, a następnie zwiększ `AktualnaWartosc` na `1002`.
  - **Usuwanie:** Jeśli usuwasz studenta `"S1001"`, sprawdź czy jego numer jest równy `AktualnaWartosc - 1` (`1002 - 1 == 1001`). Jeśli tak, zmniejsz `AktualnaWartosc` z `1002` na `1001`.

------------------------------------------------------------------------

#### Część 3: Biblioteka `Infrastructure` – `DbContext`, *Owned Entity*, *Shadow Property*, Indeksy i Migracje (20 pkt)

W projekcie `University.Infrastructure` zaimplementuj klasę `UniversityDbContext` dziedziczącą po `DbContext` oraz pełną konfigurację mapowania modelu we Fluent API.

##### Wymagania szczegółowe:

1.  **\[5 pkt\] Konfiguracja Obiektów Wartości jako Encji Posiadanych (`OwnsOne`) oraz Właściwości Ukrytych (*Shadow Property*):**
    - **Encje Posiadane (`Owned Entity Types` – `.OwnsOne`):**
      - Zmapuj właściwość `AdresZamieszkania` w encji `Student` (oraz `Profesor`) za pomocą metody `.OwnsOne(...)`, jawnie konfigurując w delegacie podrzędnym maksymalne długości kolumn (`HasMaxLength`) oraz ich nazwy (`HasColumnName`).
      - *(Uwaga: Dla jednej z encji, np. `Profesor`, możesz alternatywnie zastosować `.ComplexProperty(...)` z EF Core 8+, aby porównać w migracji zachowanie Encji Posiadanej z ukrytym kluczem Shadow PK względem Typu Złożonego, jednak przynajmniej jedna encja musi wykorzystywać pełną konfigurację `.OwnsOne(...)`).*
    - **Właściwość Ukryta (*Shadow Property*):**
      - Aby nie zanieczyszczać klas POCO w warstwie `Domain` kolumnami technicznymi, w konfiguracji Fluent API dla encji `Student` oraz `Profesor` zdefiniuj właściwość ukrytą typu `DateTime` o nazwie `"OstatniaModyfikacja"`: `builder.Property<DateTime>("OstatniaModyfikacja");`
2.  **\[5 pkt\] Konfiguracja wszystkich związków i kluczy:**
    - Skonfiguruj klucz główny `Prefix` dla encji `LicznikIndeksow`.
    - Skonfiguruj związki: `1:1` (`Profesor` <span class="math inline">\\\leftrightarrow\\</span> `Gabinet`), `1:N` (`Wydzial` <span class="math inline">\\\leftrightarrow\\</span> `Kurs`, `Profesor` <span class="math inline">\\\leftrightarrow\\</span> `Kurs`), `N:M` z encją pośrednią (`Enrollment`) oraz `N:M` samoodwołujący się (`Kurs.WymaganiaWstepne` <span class="math inline">\\\leftrightarrow\\</span> `Kurs.KursyZalezne`).
3.  **\[4 pkt\] Niedomyślna obsługa kasowania (`DeleteBehavior.SetNull`):**
    - Dla związku `StudentStudiowMagisterskich` <span class="math inline">\\\rightarrow\\</span> `Promotor` (`Profesor`) skonfiguruj zachowanie `.OnDelete(DeleteBehavior.SetNull)`.
    - Usunięcie profesora będącego promotorem **nie może usuwać studenta**, a jedynie automatycznie wyzerować (`NULL`) u niego pole `PromotorId`.
4.  **\[3 pkt\] Unikalne indeksy bazodanowe:**
    - Dodaj unikalny indeks (`.HasIndex(...).IsUnique()`) na kolumnie `IndeksUczelniany` zarówno dla encji `Student`, jak i `Profesor`, aby wymusić unikalność na poziomie silnika SQL i przyspieszyć wyszukiwanie po numerze indeksu.
5.  **\[3 pkt\] Wygenerowanie migracji i aktualizacja bazy:**
    - Wygeneruj migrację w projekcie `Infrastructure` i zapewnij automatyczną aktualizację schematu bazy danych (`Database.Migrate()` / `MigrateAsync()`).

**💡 Wskazówki implementacyjne: Konfiguracja OnModelCreating (OwnsOne, Shadow Property i Multiple Cascade Paths) (Rozwiń)**

W bazie Microsoft SQL Server związek między `Kurs` a `Profesor` oraz `Enrollment` może wygenerować ostrzeżenie o wielu ścieżkach kaskadowych (*multiple cascade paths* – błąd 1785). Dla związków `Profesor` (Prowadzący) <span class="math inline">\\\rightarrow\\</span> `Kurs` oraz `Wydział` <span class="math inline">\\\rightarrow\\</span> `Kurs` należy jawnie ustawić `.OnDelete(DeleteBehavior.SetNull)` lub `.OnDelete(DeleteBehavior.Restrict)`:

``` csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 1. Klucz główny LicznikIndeksow
    modelBuilder.Entity<LicznikIndeksow>()
        .HasKey(l => l.Prefix);

    // 2. Student: Indeks unikalny + Shadow Property + Owned Entity (OwnsOne)
    modelBuilder.Entity<Student>(builder =>
    {
        builder.HasIndex(s => s.IndeksUczelniany).IsUnique();
        builder.Property(s => s.IndeksUczelniany).HasMaxLength(20).IsRequired();

        // Deklaracja Shadow Property (kolumna audytowa niewidoczna w klasie POCO):
        builder.Property<DateTime>("OstatniaModyfikacja");

        // Konfiguracja Obiektu Wartości jako Owned Entity (tworzy w tle Shadow PK):
        builder.OwnsOne(s => s.AdresZamieszkania, a =>
        {
            a.Property(p => p.Ulica).HasMaxLength(100).HasColumnName("Ulica").IsRequired();
            a.Property(p => p.Miasto).HasMaxLength(60).HasColumnName("Miasto").IsRequired();
            a.Property(p => p.KodPocztowy).HasMaxLength(10).HasColumnName("KodPocztowy").IsRequired();
        });
    });

    // 3. Profesor: Indeks unikalny + Shadow Property + Adres + Związek 1:1 z Gabinetem
    modelBuilder.Entity<Profesor>(builder =>
    {
        builder.HasIndex(p => p.IndeksUczelniany).IsUnique();
        builder.Property(p => p.IndeksUczelniany).HasMaxLength(20).IsRequired();

        // Deklaracja Shadow Property:
        builder.Property<DateTime>("OstatniaModyfikacja");

        // Konfiguracja OwnsOne (lub alternatywnie ComplexProperty w EF Core 8+):
        builder.OwnsOne(p => p.AdresZamieszkania, a =>
        {
            a.Property(x => x.Ulica).HasMaxLength(100).HasColumnName("Ulica").IsRequired();
            a.Property(x => x.Miasto).HasMaxLength(60).HasColumnName("Miasto").IsRequired();
            a.Property(x => x.KodPocztowy).HasMaxLength(10).HasColumnName("KodPocztowy").IsRequired();
        });

        builder.HasOne(p => p.Gabinet)
            .WithOne(g => g.Profesor)
            .HasForeignKey<Gabinet>(g => g.ProfesorId)
            .OnDelete(DeleteBehavior.Cascade);
    });

    // 4. Związek Magistrant -> Promotor z DeleteBehavior.SetNull
    modelBuilder.Entity<StudentStudiowMagisterskich>()
        .HasOne(m => m.Promotor)
        .WithMany(p => p.Dyplomanci)
        .HasForeignKey(m => m.PromotorId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.SetNull);

    // 5. Związek Kurs -> Profesor prowadzący oraz Wydział
    modelBuilder.Entity<Kurs>(builder =>
    {
        builder.HasOne(k => k.Wydzial)
            .WithMany(w => w.Kursy)
            .HasForeignKey(k => k.WydzialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(k => k.Prowadzacy)
            .WithMany(p => p.ProwadzoneKursy)
            .HasForeignKey(k => k.ProwadzacyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Self-referencing N:M (Prerequisites - tabela pośrednia oparta o Shadow Properties)
        builder.HasMany(k => k.WymaganiaWstepne)
            .WithMany(k => k.KursyZalezne)
            .UsingEntity(j => j.ToTable("WymaganiaWstepneKursow"));
    });

    // 6. Związek N:M z ładunkiem (Enrollment)
    modelBuilder.Entity<Enrollment>(builder =>
    {
        builder.HasKey(e => new { e.StudentId, e.KursId });

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Zapisy)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Kurs)
            .WithMany(k => k.Zapisy)
            .HasForeignKey(e => k.KursId)
            .OnDelete(DeleteBehavior.Cascade);
    });
}Kopiuj
```

------------------------------------------------------------------------

#### Część 4: Zaawansowane Zapytania Analityczne (LINQ / SQL) i Optymalizacja Wydajności (20 pkt)

Zaimplementuj serwis zapytań analitycznych (np. `UniversityQueryService`). Zapytania powinny zostać zrealizowane za pomocą **LINQ to Entities** (preferowane) lub sparametryzowanego SQL (jeśli wykażesz, że LINQ generuje nieoptymalny plan wykonania).

> **⚠️ Kryteria oceny wydajności zapytań:**  
> Ocenie podlega nie tylko poprawność zwracanego wyniku, ale również **jakość i wydajność wygenerowanego zapytania SQL**:
>
> 1.  **Zapytania Read-Only (`.AsNoTracking()`):** Wszystkie poniższe zapytania służą wyłącznie do odczytu i muszą wyłączać śledzenie zmian.
> 2.  **Projekcja danych (`.Select(x => new WynikDto { ... })`):** Pobieraj z bazy wyłącznie kolumny i agregacje potrzebne do prezentacji wyniku (zakaz ładowania pełnych grafów encji do pamięci RAM w celu ich ręcznego przeliczenia w pętli `foreach`).
> 3.  **Brak problemu <span class="math inline">\\N+1\\</span> oraz ewaluacja Server-Side:** Operacje `Where`, `OrderByDescending`, `Count`, `Sum`, `Average` muszą być tłumaczone na SQL przed wywołaniem `.ToListAsync()` / `.FirstOrDefaultAsync()`.

##### Wymagane zapytania:

1.  **\[5 pkt\] Zapytanie 1 – Profesor z największą łączną liczbą studentów:**
    - Znajdź profesora, który prowadzi kursy z największą łączną liczbą zapisanych studentów (suma zapisów studenckich ze wszystkich kursów prowadzonych przez danego profesora).
    - Zwróć obiekt DTO zawierający: imię, nazwisko, tytuł naukowy i indeks profesora, liczbę prowadzonych przez niego kursów oraz łączną liczbę zapisanych na nie studentów.
2.  **\[5 pkt\] Zapytanie 2 – Średnia ocen (GPA) dla kursów na wybranym wydziale:**
    - Dla wydziału wybranego przez użytkownika (przekazanego przez `wydzialId` lub nazwę wydziału) zwróć zestawienie wszystkich kursów tego wydziału wraz z:
      - wyliczoną **średnią ocen (GPA)** ze wszystkich zapisów posiadających wystawioną ocenę (`Ocena != null`),
      - **liczbą studentów**, którzy otrzymali ocenę na danym kursie.
3.  **\[10 pkt\] Zapytanie 3 – Student z „najtrudniejszym” planem zajęć (Unikalne ECTS z prerekwizytami):**
    - Znajdź studenta posiadającego najbardziej wymagający plan zajęć.
    - **Definicja „trudności” planu:** Suma punktów ECTS wszystkich kursów, na które student jest aktualnie zapisany, **ORAZ** suma punktów ECTS wszystkich bezpośrednich wymagań wstępnych (*prerekwizytów*) dla tych kursów.
    - **Kluczowy warunek:** Należy wyeliminować duplikaty kursów! Jeżeli dwa kursy, na które zapisany jest student, mają ten sam prerekwizyt (lub jeśli student jest jednocześnie zapisany na kurs będący prerekwizytem innego jego kursu), punkty ECTS tego kursu mogą zostać policzone dla danego studenta **tylko jeden raz**.

**💡 Wskazówki implementacyjne: Projekcja LINQ dla wszystkich 3 zapytań (Rozwiń)**

``` csharp
// ZAPYTANIE 1: Profesor z największą łączną liczbą studentów na prowadzonych kursach
var topProfesor = await _context.Profesorowie
    .AsNoTracking()
    .Select(p => new TopProfesorDto
    {
        Indeks = p.IndeksUczelniany,
        PelneImieNazwisko = $"{p.TytulNaukowy} {p.Imie} {p.Nazwisko}",
        LiczbaProwadzonychKursow = p.ProwadzoneKursy.Count(),
        LacznaLiczbaStudentow = p.ProwadzoneKursy.SelectMany(k => k.Zapisy).Count()
    })
    .OrderByDescending(dto => dto.LacznaLiczbaStudentow)
    .FirstOrDefaultAsync();

// ZAPYTANIE 2: Średnia ocen (GPA) dla każdego kursu na wybranym wydziale
var kursyGpa = await _context.Kursy
    .AsNoTracking()
    .Where(k => k.WydzialId == wybranyWydzialId)
    .Select(k => new KursGpaDto
    {
        KodKursu = k.KodKursu,
        NazwaKursu = k.Nazwa,
        LiczbaOcenionychStudentow = k.Zapisy.Count(z => z.Ocena.HasValue),
        SredniaOcen = k.Zapisy
            .Where(z => z.Ocena.HasValue)
            .Average(z => z.Ocena) ?? 0.0
    })
    .ToListAsync();

// ZAPYTANIE 3: Student z najtrudniejszym planem zajęć (bez powtórzeń prerekwizytów)
// Łączymy za pomocą Union (który w SQL działa jak UNION eliminujący duplikaty)
// kursy bezpośrednie studenta oraz kursy będące ich bezpośrednimi wymaganiami wstępnymi:
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

*(Jeżeli w wybranej wersji dostawcy SQL operator `.Union()` wewnątrz podzapytania `.Sum()` zgłosi ograniczenie translacji, możesz pobrać projekcję `.Select(...)` zawierającą jedynie identyfikatory i punkty `(Id, PunktyECTS)` kursów oraz ich prerekwizytów, lub użyć zapytania `FromSqlInterpolated` z podzapytaniem `UNION`).*

------------------------------------------------------------------------

#### Część 5: Interfejs Użytkownika (`UI` – Konsola / Terminal.Gui / Spectre.Console / WPF) (20 pkt)

W projekcie `University.UI` przygotuj czytelny interfejs użytkownika spinający wszystkie warstwy za pomocą kontenera Dependency Injection (`Microsoft.Extensions.DependencyInjection`).

##### Wymagania szczegółowe:

1.  **\[5 pkt\] Uruchomienie generatora danych (`Bogus`):**
    - Opcja w menu/interfejsie pozwalająca zasilić bazę danych świeżym zestawem wygenerowanych encji (z wykorzystaniem transakcyjnego przydzielania indeksów z Części 2).
2.  **\[7.5 pkt\] Przeglądanie zawartości bazy danych (w tym *Shadow Property*) i zarządzanie encjami:**
    - Wyświetlanie listy studentów (wraz z ich wygenerowanymi indeksami, zformatowanymi adresami zamieszkania, odczytaną za pomocą `EF.Property<DateTime>(s, "OstatniaModyfikacja")` wartością *Shadow Property* oraz – dla magistrantów – tematem pracy i danymi promotora).
    - Wyświetlanie listy profesorów (wraz z przypisanymi gabinetami i prowadzonymi kursami – z użyciem projekcji lub `.AsSplitQuery()`, aby uniknąć eksplozji kartezjańskiej), listy kursów (wraz z ich wymaganiami wstępnymi) oraz aktualnej tabeli `LicznikiIndeksow` (prefiksy i ich bieżące wartości).
    - Możliwość ręcznego dodania nowego prefiksu do `LicznikIndeksow` (np. `"D"` z wartością początkową `0`) oraz ręcznego dodania lub edycji studenta/profesora (weryfikującej walidację w konstruktorze `Adres` oraz aktualizację *Shadow Property*).
3.  **\[2.5 pkt\] Usuwanie encji (Testowanie cofania licznika oraz `DeleteBehavior.SetNull`):**
    - Opcja usunięcia wybranego studenta lub profesora po `Id` / `IndeksUczelniany`:
      - Usunięcie ostatnio dodanego studenta (np. `"S1020"`) musi widocznie cofnąć licznik `"S"` w tabeli `LicznikiIndeksow` (np. z `1020` na `1019`), podczas gdy usunięcie studenta ze środka listy (np. `"S1005"`) nie zmienia stanu licznika.
      - Usunięcie profesora będącego promotorem magistranta musi pozostawić studenta w bazie z wyzerowanym polem promotora (`Brak promotora / NULL`).
4.  **\[5 pkt\] Uruchamianie i prezentacja wyników 3 zapytań analitycznych:**
    - Dedykowana sekcja raportów pozwalająca uruchomić każde z trzech zapytań z Części 4 (w tym wybrać wydział z listy dla Zapytania 2) i wyświetlić sformatowane wyniki na ekranie.

------------------------------------------------------------------------

### 4. Scenariusze Weryfikacyjne (Lista Kontrolna przed Oddaniem Projektu)

Przed przesłaniem rozwiązania przetestuj w aplikacji następujący scenariusz odbiorczy:

1.  **Test migracji i czystego startu:**
    - Usuń bazę danych (`dotnet ef database drop` lub `Drop-Database`) i uruchom aplikację. Baza powinna zostać automatycznie odtworzona na podstawie migracji, a tabela `LicznikiIndeksow` powinna posiadać zainicjowane prefiksy `"S"` i `"P"`.
2.  **Test walidacji Obiektu Wartości (`Adres`) oraz działania *Shadow Property*:**
    - Spróbuj utworzyć adres z niepoprawnym kodem pocztowym (np. `"123"`) – konstruktor `Adres` powinien rzucić wyjątek `ArgumentException`.
    - Dodaj lub zmodyfikuj studenta i sprawdź na liście studentów, czy odczytana przez `EF.Property<DateTime>(s, "OstatniaModyfikacja")` data modyfikacji została zaktualizowana.
3.  **Test generatora Bogus i ciągłości numeracji:**
    - Uruchom generator z poziomu UI. Sprawdź w widoku listy studentów i profesorów, czy ich numery `IndeksUczelniany` tworzą spójny ciąg (np. `S1001`, `S1002`, ..., `S1015`) oraz czy wartość w tabeli `LicznikiIndeksow` odpowiada ostatniemu nadanemu numerowi.
4.  **Test transakcyjnego cofania licznika przy usuwaniu:**
    - Zanotuj aktualną wartość licznika dla prefiksu `"S"` (np. `1015`).
    - Usuń studenta ze środka sekwencji (np. `S1005`) → licznik `"S"` **musi pozostać równy `1015`**.
    - Usuń studenta z końca sekwencji (`S1015`) → licznik `"S"` **musi cofnąć się na `1014`**.
    - Dodaj nowego studenta → musi otrzymać indeks **`S1015`**.
5.  **Test zachowania `DeleteBehavior.SetNull`:**
    - Wyświetl listę studentów studiów magisterskich i sprawdź profesora przypisanego jako promotor jednego z nich.
    - Usuń tego profesora z bazy danych.
    - Odśwież listę studentów magisterskich – student musi nadal istnieć w bazie, a jego pole promotora powinno wskazywać `NULL` (brak promotora).
6.  **Test poprawności zapytań LINQ:**
    - Uruchom kolejno Zapytania 1, 2 i 3 z poziomu UI i zweryfikuj, czy aplikacja nie rzuca wyjątków translacji LINQ oraz czy w Zapytaniu 3 wspólne prerekwizyty nie zawyżają sumy punktów ECTS.

------------------------------------------------------------------------

### 5. Tabela Podsumowująca Oceny (Suma: 120 pkt / 120%)

|  Część   | Warstwa / Moduł                  | Weryfikowane mechanizmy, wzorce i wymagania techniczne                                                                                                                                                                                                                                                                     |       Punkty       |
|:--------:|:---------------------------------|:---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|:------------------:|
|  **1**   | **Biblioteka `Domain`**          | Encje POCO (`Student`, `Profesor`, `Kurs`, `Wydzial`, `Gabinet`, `LicznikIndeksow`), niemodyfikowalny obiekt wartości `Adres` z walidacją inwariantów, związki (`1:1`, `1:N`, `Enrollment` N:M z oceną, `Prerequisites` self-referencing N:M) (20 pkt) + dziedziczenie `StudentStudiowMagisterskich` z promotorem (5 pkt). |     **25 pkt**     |
|  **2**   | **Biblioteka `Application`**     | Operacje CRUD dla encji i `LicznikIndeksow`, aktualizacja *Shadow Property* (`OstatniaModyfikacja`), transakcyjne przydzielanie indeksów oraz warunkowe cofanie licznika przy usuwaniu (15 pkt) + generator danych **Bogus** zintegrowany z serwisem numeracji (20 pkt).                                                   |     **35 pkt**     |
|  **3**   | **Biblioteka `Infrastructure`**  | `UniversityDbContext`, konfiguracja encji posiadanych (`.OwnsOne` dla `Adres`), definicja *Shadow Property* (`OstatniaModyfikacja`), konfiguracja związków we Fluent API, `OnDelete(DeleteBehavior.SetNull)` dla promotora, unikalne indeksy na `IndeksUczelniany` oraz migracje.                                          |     **20 pkt**     |
|  **4**   | **Zapytania (LINQ / SQL)**       | Wydajne zapytania z `.AsNoTracking()` i projekcją `.Select()`: 1. Profesor z największą liczbą studentów (5 pkt), 2. GPA i liczba ocenionych dla kursów na wydziale (5 pkt), 3. Student z najtrudniejszym planem zajęć z deduplikacją prerekwizytów (10 pkt).                                                              |     **20 pkt**     |
|  **5**   | **Interfejs Użytkownika (`UI`)** | Aplikacja (Konsola / Terminal.Gui / WPF): uruchomienie generatora (5 pkt), przeglądanie encji, adresów, *Shadow Property* (`EF.Property`) i liczników oraz CRUD (7.5 pkt), usuwanie testujące cofanie licznika i `SetNull` (2.5 pkt), prezentacja wyników 3 zapytań (5 pkt).                                               |     **20 pkt**     |
| **SUMA** |                                  | **Pełna realizacja zadania (wraz z pulą dodatkową +20%)**                                                                                                                                                                                                                                                                  | **120 pkt (120%)** |
