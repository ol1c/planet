# Programowanie Aplikacji Lokalnych / Architektura Systemów .NET

**Rok akademicki:** 2026/27

## Zadanie projektowe: Rozproszona Platforma Optymalizacji Problemów NP-Trudnych "OptiSync.NP" (Wieloprocesowość, TPL, Asynchroniczne IPC i Mechanizmy Synchronizacji w .NET 8/9/10)

**Prowadzący:** Tomasz Goluch  
**Wersja:** 1.0 **Wymagany standard:** C# 12+ / .NET 8 LTS, .NET 9 lub .NET 10 LTS  
**Maksymalna ocena zadania:** 120 punktów (120%)  
**Forma realizacji:** Projekt wieloprocesowy (**GUI WPF + Konsolowe Procesy Robocze + Shared Project**) tworzony **od zera (Greenfield)**

------------------------------------------------------------------------

### 1. Kontekst Biznesowy, Algorytmiczny i Architektoniczny Projektu

Celem zadania jest zaprojektowanie i zaimplementowanie wieloprocesowego systemu obliczeniowego **`OptiSync.NP`**, służącego do rozwiązywania wybranych problemów optymalizacji kombinatorycznej należących do klasy **NP-trudnych (NP-hard)** oraz eksperymentalnego porównania wydajności dwóch modeli zrównoleglania w środowisku .NET:

1.  **Modelu opartego o bibliotekę TPL (*Task Parallel Library*):** `Task`, `Parallel`, `Channel<T>` oraz algorytm *Work-Stealing*.
2.  **Modelu klasycznej wielowątkowości (*Thread Queue*):** Dedykowanej kolejki wątków / `ThreadPool.QueueUserWorkItem` oraz kolekcji blokujących `BlockingCollection<T>`.

Ponieważ przy założeniu `P != NP` dla problemów NP-trudnych nie istnieją deterministyczne algorytmy dokładne o złożoności wielomianowej, system `OptiSync.NP` wykorzystuje **wieloetapowy potok metaheurystyczny (Algorytm Memetyczny)**. Grupa *N* współbieżnych zadań/wątków wykonuje obliczenia w modelu hybrydowym: **równolegle** przeszukuje przestrzeń rozwiązań wewnątrz danej tury, a następnie **szeregowo (fazowo)** synchronizuje się na barierach (`Barrier`), aby wymienić informacje, odrzucić słabych kandydatów i przejść do kolejnej heurystyki. Wszystkie operatory heurystyczne muszą zostać zaimplementowane bezpośrednio w kodzie C# w projekcie współdzielonym.

------------------------------------------------------------------------

#### 1.1. Architektura Potoku Obliczeniowego (Faza 0, Faza 1, Faza 2)

Proces optymalizacji w każdym silniku obliczeniowym przebiega w trzech ściśle zdefiniowanych etapach:

- **Faza 0 – Jednorazowa Inicjalizacja Konstrukcyjna (`CountdownEvent`):**  
  Przed rozpoczęciem właściwych epok *N* zadań/wątków generuje równolegle populację startową liczącą `2 * N` rozwiązań (po jednej parze rodziców dla każdego wątku) za pomocą wielostartowej heurystyki zachłannej z losowym zaburzeniem (np. *Randomized Nearest Neighbor* dla TSP). Wątek główny procesu oczekuje na ukończenie inicjalizacji wszystkich uczestników za pomocą obiektu `CountdownEvent`.
- **Faza 1 – Eksploracja Globalna (`Barrier #1`):**  
  W pierwszej turze każdej epoki *N* zadań/wątków przez zadany czas wykonuje równolegle operatory krzyżowania populacyjnego (np. **PMX – *Partially Mapped Crossover*** lub **OX – *Order Crossover***) oraz szybkiej mutacji. Z każdej czwórki (2 rodziców + 2 potomków) wątek zachowuje 2 najlepsze rozwiązania. Po upływie czasu Fazy 1 wszystkie wątki wystawiają swoich kandydatów i zatrzymują się na barierze (`Barrier.SignalAndWait()`). W akcji kończącej fazę (`postPhaseAction`) połowa zgłoszonych rozwiązań o koszcie gorszym od mediany zostaje odrzucona, a pozostałych *N* najlepszych kandydatów trafia do Fazy 2.
- **Faza 2 – Intensyfikacja Lokalna (`Barrier #2`):**  
  W drugiej turze epoki każdy z *N* wątków ulepsza przydzielonego mu kandydata za pomocą przeszukiwania lokalnego: najpierw wykonuje szybkie wygładzenie algorytmem **2-opt** o złożoności `O(n^2)` (eliminującym przecięcia krawędzi), a następnie – w ramach pozostałego budżetu czasu tury – przeszukuje wymiany trójek krawędzi algorytmem **3-opt** o złożoności `O(n^3)`. Po wyczerpaniu przestrzeni wymian lub upływie czasu Fazy 2 wątki czekają na kolejnej barierze (`Barrier.SignalAndWait()`), gdzie *N* rozwiązań sprzed Fazy 2 oraz *N* rozwiązań ulepszonych w Fazie 2 jest losowo łączonych w pary rodziców na kolejną epokę.

------------------------------------------------------------------------

#### 1.2. Wybór Problemu NP-Trudnego: Wariant Główny (TSP) oraz 3 Warianty Alternatywne

Projekt należy zrealizować w oparciu o **Wariant Główny (Komiwojażer – TSP)** lub jeden z **trzech Wariantów Alternatywnych**. Każdy wariant korzysta z publicznie dostępnych zbiorów danych (*Benchmarks*) i realizuje opisany wyżej 3-etapowy potok równoległo-szeregowy:

| Wariant problemu NP-trudnego                                         | Publiczny zbiór danych (Benchmark)                                                                                  | Faza 0: Inicjalizacja (`CountdownEvent`)                                                  | Faza 1: Eksploracja Globalna (`Barrier #1`)                                            | Faza 2: Intensyfikacja Lokalna (`Barrier #2`)                                                         |
|:---------------------------------------------------------------------|:--------------------------------------------------------------------------------------------------------------------|:------------------------------------------------------------------------------------------|:---------------------------------------------------------------------------------------|:------------------------------------------------------------------------------------------------------|
| **1. Problem Komiwojażera (TSP / NTSP)** *(Domyślny)*                | **TSPLIB / Waterloo NTSP** (`.tsp`, format `EUC_2D`, np. Sahara Zach. `wi29`, Dżibuti `dj38`, Katar `qa194`)        | Wielostartowy losowy Najbliższy Sąsiad (*Randomized k-NN*)                                | Krzyżowanie permutacji **PMX** (*Partially Mapped Crossover*) lub **OX** + mutacja     | Szybki **2-opt** (`O(n^2)`) przechodzący płynnie w ograniczony czasowo **3-opt** (`O(n^3)`)           |
| **2. Marszrutyzacja Pojazdów (CVRP)** *(Alternatywa A)*              | **CVRPLIB** (`.vrp`, np. zbiory *Augerat A/B/P*, *Uchoa* – magazyn + klienci z popytem `q_i` i pojemnością aut `Q`) | Algorytm Oszczędnościowy **Clarke’a-Wrighta** (*Savings*) z losowym szumem                | Krzyżowanie tras **RBX** (*Route-Based Crossover*) + wymiana klientów *Cross-Exchange* | Równoległy **2-opt** wewnątrz tras ciężarówek + relokacja **Or-opt / Tabu Search**                    |
| **3. Problem Plecakowy / Bin Packing (MKP / BPP)** *(Alternatywa B)* | **OR-Library / PackLib2** (wagi, gabaryty i wartości przedmiotów, pojemność kontenerów)                             | Zachłanny **First-Fit Decreasing (FFD)** / współczynnik `v_i / w_i` z losowym zaburzeniem | Krzyżowanie jednorodne z operatorem naprawy zachłannej (*Greedy Repair Operator*)      | Przeszukiwanie sąsiedztwa **k-Swap** (wymiana `1 <-> 2` elementów) z **Symulowanym Wyżarzaniem (SA)** |
| **4. Kolorowanie Grafu (GCP / Max-Cut)** *(Alternatywa C)*           | **DIMACS Graph Coloring** (`.col`, np. grafy konfliktów `anna.col`, `queen8_8.col`, `le450`)                        | Zachłanny algorytm stopnia nasycenia **DSATUR** z losowym rozstrzyganiem remisów          | Krzyżowanie podziałów zbiorów niezależnych **GPX** (*Greedy Partition Crossover*)      | Algorytm **TabuCol** (lokalna zmiana barwy wierzchołków konfliktowych) + łańcuchy Kempego             |

*(Po uzgodnieniu z prowadzącym dopuszczalna jest również realizacja wariantów symulacyjnych: **Współbieżnego Labiryntu z Dynamiczną Barierą** lub **Symultanicznego Roslyna**, przy zachowaniu pełnej architektury wieloprocesowej i poniższej matrycy synchronizacji).*

------------------------------------------------------------------------

#### 1.3. Mapa Mechanizmów Synchronizacji Wymaganych w Projekcie

System musi wykorzystywać następujące mechanizmy współbieżności i synchronizacji w przypisanych im rolach architektonicznych:

| Poziom / Tryb           | Mechanizm w .NET (.NET 8/9/10)                                      | Konkretne zastosowanie w architekturze `OptiSync.NP`                                                                                                                                                               |
|:------------------------|:--------------------------------------------------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **No-Sync**             | `record`, `ThreadLocal<T>`, `Random.Shared`                         | Niemodyfikowalne punkty mapy i komunikaty IPC (`record`); prywatne tablice robocze wątków (`ThreadLocal<WorkerScratchpad>`) eliminujące alokacje na stercie i zjawisko *False Sharing*.                            |
| **User Mode (CAS)**     | `Interlocked.Increment`, `Interlocked.CompareExchange`              | Bezblokadowe zliczanie wszystkich przeliczonych rozwiązań we wszystkich wątkach oraz atomowa pętla CAS aktualizująca najniższy koszt liczbowy.                                                                     |
| **User Mode**           | `ConcurrentDictionary<K,V>`, `Channel<T>` / `BlockingCollection<T>` | Rejestr statystyk poszczególnych wątków (rywalizacja liderów) oraz kolejka Producent-Konsument przekazująca telemetrię do wątku komunikacji IPC.                                                                   |
| **User Mode / Hybrid**  | `ReaderWriterLockSlim` oraz `Lock` / `Monitor.TryEnter`             | Ochrona magazynu najlepszego rozwiązania (`BestSolutionVault` – częste odczyty kosztu przez wątki robocze, rzadkie zapisy nowej trasy) oraz bezpieczna wymiana populacji z timeoutem.                              |
| **Hybrid Mode**         | `ManualResetEventSlim`, `SemaphoreSlim`                             | Bramka **Pauza / Wznowienie** zatrzymująca wszystkie wątki robocze bez obciążania CPU (`ManualResetEventSlim`) oraz asynchroniczna sekcja krytyczna (`SemaphoreSlim(1,1)`) na strumieniu zapisu IPC.               |
| **Zespoły Zadań**       | `CountdownEvent`, `Barrier`                                         | Oczekiwanie na wygenerowanie populacji początkowej w Fazie 0 (`CountdownEvent`) oraz cykliczna synchronizacja między Fazą 1 a Fazą 2 wraz z odrzucaniem rozwiązań poniżej mediany w `postPhaseAction` (`Barrier`). |
| **Kernel Mode (IPC)**   | Nazwany `Mutex`, Nazwany `Semaphore`                                | Blokada uruchomienia drugiej instancji koordynatora GUI (`@"Global\OptiSync_GUI_Mutex"`) oraz limitowanie liczby aktywnych procesów roboczych w systemie (`@"Global\OptiSync_Workers_Sem"`).                       |
| **Asynchrony & Timers** | `PeriodicTimer`, `CancellationToken`, `IAsyncEnumerable<T>`         | Dynamiczne odmierzanie czasu trwania Fazy 1 i Fazy 2 (`CreateLinkedTokenSource`), cykliczny puls telemetrii co 250 ms (`PeriodicTimer`) oraz strumieniowy odbiór danych z potoku IPC w GUI (`await foreach`).      |

------------------------------------------------------------------------

### 2. Inicjalizacja Środowiska (Krok 0: Przygotowanie Solucji Wieloprocesowej od Zera)

W celu uniknięnych duplikacji kodu algorytmicznego i modeli IPC pomiędzy procesami roboczymi (`Worker.TPL` oraz `Worker.Threads`), wspólny kod należy wydzielić do projektu typu **Shared Project (`.shproj` / `.projitems`)**.

Otwórz terminal w pustym katalogu roboczym i wykonaj poniższy skrypt `.NET CLI`, aby utworzyć kompletną strukturę solucji (możesz dostosować flagę `-f net8.0` do `net9.0` lub `net10.0`):

``` bash
# 1. Utworzenie pliku rozwiązania (Solution)
dotnet new sln -n OptiSyncNP

# 2. Utworzenie aplikacji okienkowej WPF (Koordynator i Wizualizator GUI)
dotnet new wpf -n OptiSync.UI.WPF -f net8.0-windows

# 3. Utworzenie dwóch niezależnych procesów roboczych (Console Workers)
dotnet new console -n OptiSync.Worker.TPL -f net8.0
dotnet new console -n OptiSync.Worker.Threads -f net8.0

# 4. Utworzenie katalogu dla Projektu Współdzielonego (Shared Project)
mkdir OptiSync.Shared

# 5. Dodanie projektów wykonywalnych do solucji
dotnet sln add OptiSync.UI.WPF/OptiSync.UI.WPF.csproj
dotnet sln add OptiSync.Worker.TPL/OptiSync.Worker.TPL.csproj
dotnet sln add OptiSync.Worker.Threads/OptiSync.Worker.Threads.csproj

# 6. Instalacja pakietów NuGet (wsparcie dla Roslyn C# Scripting)
dotnet add OptiSync.UI.WPF/OptiSync.UI.WPF.csproj package Microsoft.CodeAnalysis.CSharp.Scripting
dotnet add OptiSync.Worker.TPL/OptiSync.Worker.TPL.csproj package Microsoft.CodeAnalysis.CSharp.Scripting
dotnet add OptiSync.Worker.Threads/OptiSync.Worker.Threads.csproj package Microsoft.CodeAnalysis.CSharp.ScriptingKopiuj
```

> **💡 Konfiguracja projektu współdzielonego (`OptiSync.Shared`):**
>
> - W środowisku Visual Studio kliknij prawym przyciskiem myszy na solucję → *Add* → *New Project* → wybierz szablon **Shared Project** o nazwie `OptiSync.Shared`, a następnie w projektach `UI.WPF`, `Worker.TPL` oraz `Worker.Threads` wybierz *Add* → *Shared Project Reference* i zaznacz `OptiSync.Shared`.
> - Alternatywnie (pracując z poziomu `.NET CLI`), dodaj wewnątrz znacznika `<Project>` w plikach `.csproj` projektów `UI.WPF`, `Worker.TPL` oraz `Worker.Threads` poniższy wpis kompilujący wspólne pliki źródłowe:
>
> ``` xml
> <ItemGroup>
>   <Compile Include="..\OptiSync.Shared\**\*.cs" LinkBase="Shared" />
> </ItemGroup>Kopiuj
> ```

------------------------------------------------------------------------

### 3. Szczegółowe Wymagania i Podział Punktów (Łącznie: 120 pkt / 120%)

``` text
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                     ARCHITEKTURA SYSTEMU OPTISYNC.NP (MULTIPROCESS)                      │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│  [Część 1: 25 pkt] Projekt Shared (Algorytmy) ──► Parser, Potok 3 Faz, ThreadLocal, NoSync│
│  [Część 2: 35 pkt] Synchronizacja i Bariery   ──► Barrier, RWLockSlim, Interlocked, Pause│
│  [Część 3: 20 pkt] Procesy Worker (TPL/Wątki) ──► TPL vs ThreadQueue, Mutex, Semaphore   │
│  [Część 4: 20 pkt] Asynchroniczny Kanał IPC   ──► NamedPipes, SemaphoreSlim, PeriodicTmr │
│  [Część 5: 20 pkt] Aplikacja GUI (WPF+Roslyn) ──► Wizualizacja 2D, Sterowanie, Roslyn API│
└──────────────────────────────────────────────────────────────────────────────────────────┘Kopiuj
```

------------------------------------------------------------------------

#### Część 1: Projekt Współdzielony (`OptiSync.Shared`) – Model Problemu NP-Trudnego, Potok Heurystyk i Strategia „No-Sync” (25 pkt)

W projekcie `OptiSync.Shared` zaimplementuj parser plików problemowych, struktury danych oparte o strategię *No-Sync* oraz 3-fazowy potok heurystyczny.

##### Wymagania szczegółowe:

1.  **\[8 pkt\] Parser danych benchmarkowych i niemodyfikowalny model dziedziny (`record`):**
    - Zaimplementuj wczytywanie danych z pliku zewnętrznego dla wybranego problemu (dla TSP: obsługa formatu **TSPLIB** – sekcja `NODE_COORD_SECTION`, format `EUC_2D`, np. Sahara Zachodnia `wi29.tsp`, Dżibuti `dj38.tsp`, Katar `qa194.tsp`).
    - Współrzędne punktów/elementów oraz definicja instancji problemu muszą być reprezentowane przez **niemodyfikowalne rekordy (`public readonly record struct CityPoint(int Id, double X, double Y)`)** oraz pre-kalkulowaną, płaską macierz odległości tylko do odczytu (`double[]`), do której wszystkie wątki mają bezpieczny dostęp bez blokad.
    - Dla formatu `EUC_2D` odległość między dwoma punktami `A(xA, yA)` oraz `B(xB, yB)` wyliczana jest ze wzoru euklidesowego: `d(A, B) = Math.Sqrt((xB - xA) * (xB - xA) + (yB - yA) * (yB - yA))`
2.  **\[12 pkt\] Wieloetapowy Potok Heurystyczny (Faza 0, Faza 1, Faza 2):**
    - **Faza 0 (Inicjalizacja populacji `2 * N` osobników):** Wygenerowanie `2 * N` rozwiązań początkowych (gdzie *N* to liczba wątków/zadań) za pomocą wielostartowej heurystyki zachłannej (*Randomized Nearest Neighbor* lub odpowiednika z Tabeli 1.2).
    - **Faza 1 (Eksploracja Globalna – np. Krzyżowanie PMX / OX):**
      - Każdy wątek otrzymuje parę rodziców `(P1, P2)`. W pętli ograniczonej czasem trwania Fazy 1 losuje podciąg indeksów `[cut1, cut2]`, wykonuje operator **PMX (*Partially Mapped Crossover*)** tworząc dwójkę potomków `(C1, C2)` (zamieniając rodziców rolami), poddaje je opcjonalnej mutacji i z czwórki `{P1, P2, C1, C2}` wybiera 2 najlepsze rozwiązania do dalszej iteracji.
    - **Faza 2 (Intensyfikacja Lokalna – 2-opt → 3-opt):**
      - Dla pojedynczego kandydata przekazanego po selekcji na barierze wątek wykonuje lokalne ulepszanie trasy: najpierw eliminuje przecięcia krawędzi za pomocą szybkiego algorytmu **2-opt** (`O(n^2)`), a następnie – dopóki nie wyczerpie się czas Fazy 2 lub przestrzeń kombinacji – przeszukuje wymiany trójek krawędzi algorytmem **3-opt** (`O(n^3)`).
3.  **\[5 pkt\] Izolacja danych na poziomie wątku (`ThreadLocal<T>` oraz `Random.Shared`):**
    - Aby wyeliminować alokacje pamięci na stercie wewnątrz gorących pętli i uniknąć zjawiska *False Sharing*, zastosuj klasę **`ThreadLocal<WorkerScratchpad>`**, zapewniającą każdemowi wątkowi roboczemu jego własny, pre-alokowany bufor tablic odwzorowań dla PMX i bufor odwracania podciągów w 2-opt/3-opt.
    - Do generowania liczb pseudolosowych w wątkach roboczych wykorzystuj bezpieczną wątkowo właściwość **`Random.Shared`**.

**💡 Wskazówki implementacyjne: Bezblokadowy bufor ThreadLocal\<WorkerScratchpad\> oraz operator PMX i 2-opt (Rozwiń)**

``` csharp
using System;
using System.Threading;

namespace OptiSync.Shared.Algorithms;

public sealed class WorkerScratchpad
{
    public int[] Mapping1 { get; }
    public int[] Mapping2 { get; }
    public bool[] Visited { get; }

    public WorkerScratchpad(int cityCount)
    {
        Mapping1 = new int[cityCount];
        Mapping2 = new int[cityCount];
        Visited = new bool[cityCount];
    }
}

public sealed class TspMemeticOperators
{
    private readonly int _n;
    private readonly double[] _distMatrix; // Płaska tablica [i * _n + j] przyjazna dla pamięci Cache L1
    private readonly ThreadLocal<WorkerScratchpad> _scratchpad;

    public TspMemeticOperators(int cityCount, double[] distMatrix)
    {
        _n = cityCount;
        _distMatrix = distMatrix;
        _scratchpad = new ThreadLocal<WorkerScratchpad>(() => new WorkerScratchpad(_n));
    }

    // Operator PMX (Partially Mapped Crossover) wykorzystujący bufory ThreadLocal (Zero-GC)
    public void ExecutePmx(ReadOnlySpan<int> parent1, ReadOnlySpan<int> parent2, Span<int> child)
    {
        var pad = _scratchpad.Value!;
        Array.Fill(pad.Mapping1, -1);
        child.Fill(-1);

        int cut1 = Random.Shared.Next(0, _n - 1);
        int cut2 = Random.Shared.Next(cut1 + 1, _n);

        // Krok 1: Skopiowanie wycinka [cut1..cut2] z Rodzica 1 do Potomka
        for (int i = cut1; i <= cut2; i++)
        {
            int city1 = parent1[i];
            int city2 = parent2[i];
            child[i] = city1;
            pad.Mapping1[city1] = city2;
        }

        // Krok 2: Uzupełnienie pozostałych pozycji z Rodzica 2 (z rozwiązywaniem konfliktów PMX)
        for (int i = 0; i < _n; i++)
        {
            if (i >= cut1 && i <= cut2) continue;

            int candidate = parent2[i];
            while (pad.Mapping1[candidate] != -1)
            {
                candidate = pad.Mapping1[candidate];
            }
            child[i] = candidate;
        }
    }

    // Szybki krok poprawy lokalnej ze sprawdzaniem przyrostu delta w czasie O(1)
    public double ImproveLocal(int[] tour, double currentCost, CancellationToken phaseToken, ManualResetEventSlim pauseGate)
    {
        bool improved = true;
        while (improved && !phaseToken.IsCancellationRequested)
        {
            pauseGate.Wait(phaseToken); // Punkt wstrzymania na bramce (Pauza = 0% CPU)
            improved = false;

            for (int i = 0; i < _n - 2 && !phaseToken.IsCancellationRequested; i++)
            {
                int a = tour[i], b = tour[i + 1];
                double distAB = _distMatrix[a * _n + b];

                for (int j = i + 2; j < _n; j++)
                {
                    if (i == 0 && j == _n - 1) continue;
                    int c = tour[j], d = tour[(j + 1) % _n];

                    double delta = (_distMatrix[a * _n + c] + _distMatrix[b * _n + d])
                                 - (distAB + _distMatrix[c * _n + d]);

                    if (delta < -1e-6)
                    {
                        Array.Reverse(tour, i + 1, j - i);
                        currentCost += delta;
                        distAB = _distMatrix[tour[i] * _n + tour[i + 1]];
                        improved = true;
                    }
                }
            }
        }
        return currentCost;
    }
}Kopiuj
```

------------------------------------------------------------------------

#### Część 2: Synchronizacja Wewnątrzprocesowa i Koordynacja Fazowa (`Barrier`, `ReaderWriterLockSlim`, `Interlocked`, `ManualResetEventSlim`) (35 pkt)

Zaimplementuj koordynator epok w procesie roboczym, łączący równoległą pracę *N* zadań/wątków z szeregową synchronizacją między fazami algorytmu.

##### Wymagania szczegółowe:

1.  **\[12 pkt\] Koordynacja Fazowa (`CountdownEvent` oraz `System.Threading.Barrier`):**
    - **Faza 0 (`CountdownEvent`):** Wątek koordynujący inicjalizację uruchamia *N* zadań wyznaczających populację startową i czeka na metodzie `countdown.Wait()`, aż każdy pracownik zgłosi gotowość przez `countdown.Signal()`.
    - **Przejście Faza 1 → Faza 2 (`Barrier`):** Wszystkie *N* wątków wykonuje Fazę 1. Po upływie czasu Fazy 1 każdy wątek zapisuje swoją najlepszą parę rozwiązań do wspólnej tablicy kandydatów i wywołuje **`barrier.SignalAndWait()`**.
    - **Akcja po fazie (`postPhaseAction` w `Barrier`):** W delegacie wykonywanym automatycznie po dotarciu ostatniego wątku do bariery następuje:
      - **Po Fazie 1:** Wyznaczenie mediany ze wszystkich `2 * N` zgłoszonych kandydatów, **odrzucenie połowy rozwiązań poniżej mediany**, przydzielenie pozostałych *N* najlepszych pojedynczych tras do *N* wątków na potrzeby Fazy 2 oraz zresetowanie zegara na czas Fazy 2.
      - **Po Fazie 2 (Koniec Epoki):** Zebranie *N* osobników sprzed Fazy 2 oraz *N* osobników ulepszonych w Fazie 2, losowe sparowanie ich w *N* par rodziców na kolejną epokę, zwiększenie licznika epoki oraz wysłanie raportu postępu do GUI.
    - **Bezpieczeństwo Bariery (*Dynamic Barrier Management*):** W przypadku przedwczesnego zakończenia lub anulowania zadania należy bezpiecznie wyrejestrować uczestnika metodą `barrier.RemoveParticipant()`, zapobiegając zakleszczeniu (*Deadlock*) pozostałych wątków.
2.  **\[10 pkt\] Magazyn Najlepszego Rozwiązania (`ReaderWriterLockSlim`) i Sekcje Krytyczne (`Lock` / `Monitor.TryEnter`):**
    - Zaimplementuj klasę `BestSolutionVault` przechowującą globalnie najlepsze znalezione rozwiązanie, jego koszt oraz identyfikator wątku (`WorkerId`), który je wyznaczył.
    - Dostęp do `BestSolutionVault` musi być chroniony przez **`ReaderWriterLockSlim`**:
      - Wątki robocze sprawdzające aktualny rekord wchodzą w trybie współdzielonym (`EnterReadLock` / `ExitReadLock`).
      - Gdy wątek odkryje lepsze rozwiązanie, wchodzi w tryb wyłącznego zapisu (`EnterWriteLock` / `ExitWriteLock`), weryfikuje ponownie warunek poprawy, kopiuje tablicę rozwiązania i zleca wysłanie komunikatu o nowym rekordzie do procesu GUI.
    - Do ochrony współdzielonych struktur pomocniczych zastosuj sekcję krytyczną `lock` (`System.Threading.Lock` w .NET 9+) lub `Monitor.TryEnter` z jawnym limitem czasu.
3.  **\[7 pkt\] Bezblokadowa Telemetria (`Interlocked` CAS) i Statystyki Liderów (`ConcurrentDictionary`):**
    - Każde przeliczenie funkcji celu dla nowego osobnika w dowolnym wątku musi zwiększać globalny licznik rozwiązanych instancji za pomocą **`Interlocked.Increment(ref _totalEvaluated)`**.
    - Zaimplementuj atomową aktualizację najniższego kosztu liczbowego za pomocą pętli bezblokadowej **CAS (`Interlocked.CompareExchange`)**.
    - Każdy z *N* wątków roboczych musi posiadać unikalny identyfikator (`WorkerId` od `1` do *N*) i aktualizować swoje statystyki (liczba wykonanych iteracji, liczba ustanowionych rekordów globalnych) w **`ConcurrentDictionary<int, WorkerStats>`**.
4.  **\[6 pkt\] Kooperatywne Przerywanie, Dynamiczny Czas Faz (`CancellationToken`) oraz Pauza (`ManualResetEventSlim`):**
    - Użyj **`ManualResetEventSlim`** jako bramki wstrzymującej obliczenia: wywołanie `Pause()` z poziomu GUI zamyka bramkę (`_pauseGate.Reset()`), a `Resume()` otwiera ją (`_pauseGate.Set()`). W stanie wstrzymania proces roboczy nie może obciążać procesora (0% CPU).
    - Czas trwania Fazy 1 i Fazy 2 musi być sterowany przez połączone źródło tokenów (`CancellationTokenSource.CreateLinkedTokenSource(globalStopToken, phaseTimeoutToken)`). Zmiana czasu fazy przez użytkownika w GUI w trakcie obliczeń musi być uwzględniana dynamicznie przy starcie kolejnej tury.

**💡 Wskazówki implementacyjne: Magazyn BestSolutionVault z ReaderWriterLockSlim oraz Interlocked (Rozwiń)**

``` csharp
using System;
using System.Linq;
using System.Threading;

namespace OptiSync.Shared.Synchronization;

public sealed class BestSolutionVault : IDisposable
{
    private readonly ReaderWriterLockSlim _rwLock = new();
    private double _bestCost = double.MaxValue;
    private int[] _bestTour = Array.Empty<int>();
    private int _discovererWorkerId = -1;

    public long TotalSolutionsEvaluated; // Modyfikowane przez Interlocked.Increment

    public double ReadBestCost()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _bestCost;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public bool TryUpdateBest(int workerId, ReadOnlySpan<int> candidateTour, double candidateCost,
        out BestSolutionSnapshot? newRecordSnapshot)
    {
        newRecordSnapshot = null;

        // 1. Szybki filtr bezblokadowy (Optymistyczny odczyt Volatile):
        if (candidateCost >= Volatile.Read(ref _bestCost))
            return false;

        // 2. Wyłączna blokada zapisu tylko wtedy, gdy kandydat pobija dotychczasowy rekord:
        _rwLock.EnterWriteLock();
        try
        {
            if (candidateCost < _bestCost)
            {
                _bestCost = candidateCost;
                _discovererWorkerId = workerId;
                if (_bestTour.Length != candidateTour.Length)
                    _bestTour = new int[candidateTour.Length];

                candidateTour.CopyTo(_bestTour);

                newRecordSnapshot = new BestSolutionSnapshot(
                    WorkerId: workerId,
                    Cost: candidateCost,
                    Tour: _bestTour.ToArray(),
                    TotalEvaluated: Interlocked.Read(ref TotalEvaluated));
                return true;
            }
            return false;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public void Dispose() => _rwLock.Dispose();
}

public record BestSolutionSnapshot(int WorkerId, double Cost, int[] Tour, long TotalEvaluated);Kopiuj
```

------------------------------------------------------------------------

#### Część 3: Dwa Silniki Obliczeniowe (`Worker.TPL` vs `Worker.Threads`) oraz Synchronizacja Jądra OS (`Mutex`, `Semaphore`) (20 pkt)

Zaimplementuj dwa procesy konsolowe korzystające ze wspólnego kodu z Części 1 i 2, różniące się mechanizmem planowania zadań, oraz zabezpiecz je obiektami jądra systemu operacyjnego.

##### Wymagania szczegółowe:

1.  **\[8 pkt\] Proces 1: `OptiSync.Worker.TPL` (Biblioteka TPL i Kanały):**
    - Do uruchomienia *N* równoległych strumieni obliczeniowych wykorzystaj bibliotekę **TPL (`Task.Run` / `Task.Factory.StartNew` lub `Parallel.For`)**.
    - Do przekazywania komunikatów telemetrycznych i nowych rekordów z wątków roboczych do pętli nadawczej IPC wykorzystaj asynchroniczny kanał **`System.Threading.Channels.Channel<IpcEnvelope>`** (`Writer.TryWrite` → `Reader.ReadAllAsync`).
2.  **\[7 pkt\] Proces 2: `OptiSync.Worker.Threads` (Kolejka Wątków i `BlockingCollection<T>`):**
    - Do uruchomienia *N* równoległych strumieni obliczeniowych wykorzystaj kolejkę wątków roboczych (**`ThreadPool.QueueUserWorkItem`** lub dedykowaną pulę obiektów `System.Threading.Thread`).
    - Do przekazywania komunikatów telemetrycznych do wątku nadawczego IPC wykorzystaj kolekcję współbieżną **`BlockingCollection<IpcEnvelope>`** (`Add` → `GetConsumingEnumerable()`).
3.  **\[5 pkt\] Obiekty Jądra Systemu Operacyjnego (`Mutex` oraz `Semaphore`):**
    - **Ochrona pojedynczej instancji GUI (`Mutex`):** W procesie `OptiSync.UI.WPF` przy starcie aplikacji utwórz nazwany obiekt jądra **`new Mutex(false, @"Global\OptiSyncNP_SingleGuiInstance")`**. Obsłuż wyjątek `AbandonedMutexException` i zablokuj możliwość uruchomienia drugiego okna koordynatora.
    - **Limitowanie puli procesów roboczych (`Semaphore`):** W procesach `Worker.TPL` oraz `Worker.Threads` przy starcie pobierz slot z nazwanego semafora systemowego **`new Semaphore(initialCount: 2, maximumCount: 2, name: @"Global\OptiSyncNP_WorkerProcessLimit")`**, ograniczającego liczbę jednocześnie działających procesów obliczeniowych w systemie operacyjnym (ze zwolnieniem slotu `Release()` w bloku `finally`).

------------------------------------------------------------------------

#### Część 4: Asynchroniczna Komunikacja Międzyprocesowa (IPC), Strumienie `IAsyncEnumerable<T>` i `PeriodicTimer` (20 pkt)

Zaimplementuj dwukierunkowy, nieblokujący protokół komunikacji między procesem GUI a uruchomionym procesem roboczym (`Worker.TPL.exe` lub `Worker.Threads.exe`).

##### Wymagania szczegółowe:

1.  **\[8 pkt\] Dwukierunkowy kanał IPC (Potoki Nazwane – `NamedPipeServerStream` / `NamedPipeClientStream`):**
    - Proces GUI uruchamia wybrany proces roboczy za pomocą `System.Diagnostics.Process.Start(...)` (przekazując nazwę potoku w argumentach `args`) i zestawia z nim połączenie przez **Potoki Nazwane (`System.IO.Pipes`)** (lub alternatywnie gniazda TCP / gRPC / Memory-Mapped Files).
    - Komunikacja musi być **w pełni asynchroniczna** (`ConnectAsync`, `ReadLineAsync` / `ReadAsync`, `WriteLineAsync` / `FlushAsync` z serializacją JSON `System.Text.Json`). Zakaz używania metod blokujących wątek UI (`.Result`, `.Wait()`).
    - **Kierunek GUI → Worker:** Przesyłanie konfiguracji startowej (ścieżka do pliku danych, liczba wątków *N*, liczba epok, czasy Fazy 1 i Fazy 2, opcjonalny skrypt Roslyn) oraz komend sterujących w locie (`Pause`, `Resume`, `Stop`, `UpdatePhaseTimes`).
    - **Kierunek Worker → GUI:** Natychmiastowe przesyłanie każdego nowo odkrytego najlepszego rozwiązania (`NewBestTour`), powiadomień o zmianie fazy/epoki na barierze (`PhaseChanged`) oraz cyklicznej telemetrii (`TelemetryTick`).
2.  **\[6 pkt\] Asynchroniczna sekcja krytyczna (`SemaphoreSlim`) i `PeriodicTimer`:**
    - Zabezpiecz współbieżny zapis do strumienia wyjściowego IPC za pomocą **`SemaphoreSlim(1, 1)`** i wywołania `await _pipeWriteLock.WaitAsync(token)`.
    - W procesie roboczym uruchom pętlę opartą o **`PeriodicTimer(TimeSpan.FromMilliseconds(250))`** (`while (await timer.WaitForNextTickAsync(token))`), która co 250 ms wysyła do GUI aktualny licznik przeliczonych instancji (`Interlocked.Read`), aktywną fazę, bieżącą epokę oraz zagregowane statystyki wątków z `ConcurrentDictionary`.
3.  **\[6 pkt\] Konsumpcja wiadomości w GUI za pomocą strumienia `IAsyncEnumerable<IpcMessage>`:**
    - Po stronie GUI opakuj nasłuchiwanie potoku IPC w metodę zwracającą asynchroniczny strumień **`IAsyncEnumerable<IpcMessage>`** z atrybutem `[EnumeratorCancellation]` i odbieraj komunikaty za pomocą konstrukcji `await foreach (var msg in ReadWorkerStreamAsync(ct))`.

**💡 Wskazówki implementacyjne: Asynchroniczny kanał NamedPipe z SemaphoreSlim i IAsyncEnumerable (Rozwiń)**

``` csharp
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OptiSync.Shared.Ipc;

public record IpcMessage(string MessageType, string JsonPayload);

public sealed class AsyncPipeChannel : IAsyncDisposable
{
    private readonly PipeStream _pipeStream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    // Asynchroniczny odpowiednik locka dla operacji await WriteLineAsync:
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AsyncPipeChannel(PipeStream pipeStream)
    {
        _pipeStream = pipeStream;
        _reader = new StreamReader(_pipeStream);
        _writer = new StreamWriter(_pipeStream) { AutoFlush = true };
    }

    public async Task SendAsync<T>(string type, T payload, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(new IpcMessage(type, JsonSerializer.Serialize(payload)));
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async IAsyncEnumerable<IpcMessage> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested && _pipeStream.IsConnected)
        {
            string? line = await _reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) break;

            var msg = JsonSerializer.Deserialize<IpcMessage>(line);
            if (msg is not null)
                yield return msg;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        _reader.Dispose();
        await _writer.DisposeAsync();
        await _pipeStream.DisposeAsync();
    }
}Kopiuj
```

------------------------------------------------------------------------

#### Część 5: Interfejs Graficzny (`OptiSync.UI.WPF`) oraz Dynamiczne Skryptowanie (`Roslyn`) (20 pkt)

W projekcie `OptiSync.UI.WPF` przygotuj responsywny interfejs graficzny prezentujący na żywo przebieg optymalizacji i rywalizację wątków.

##### Wymagania szczegółowe:

1.  **\[6 pkt\] Panel Konfiguracji i Sterowania Procesami:**
    - Wybór pliku wejściowego z dysku (`OpenFileDialog`, np. `.tsp`, `.vrp`, `.col`).
    - Wybór silnika wykonawczego: **`TPL Worker Process`** vs **`Thread Queue Worker Process`**.
    - Pola numeryczne określające: liczbę równoległych zadań/wątków (*N*), docelową liczbę epok oraz czasy trwania **Fazy 1** i **Fazy 2** w sekundach (z możliwością ich zmiany **dynamicznie w trakcie trwania obliczeń**).
    - Przyciski sterujące: **Start**, **Pause / Resume** (ten sam przycisk zmieniający etykietę i stan bramki `ManualResetEventSlim`), **Stop** (kooperatywne przerwanie obliczeń) oraz **Exit** (przycisk zamknięcia aplikacji oraz przycisk `X` okna muszą być **zablokowane podczas trwania obliczeń** i odblokowywane dopiero po bezpiecznym zakończeniu lub zatrzymaniu procesu roboczego).
2.  **\[6 pkt\] Dynamiczna Wizualizacja 2D i Responsywność Wątku UI:**
    - Kontrolka `Canvas` (lub `WriteableBitmap` / `DrawingVisual`) rysująca na bieżąco wierzchołki oraz krawędzie najlepszego aktualnie znalezionego rozwiązania.
    - Wykres/mapa musi automatycznie skalować współrzędne do całej dostępnej powierzchni okna i dynamicznie reagować na zmianę rozmiaru okna (**zdarzenie `SizeChanged`**).
    - Aktualizacja kontrolek UI (paska postępu `ProgressBar`, aktywnej fazy, numeru epoki, tabeli współrzędnych *Optimal Tour*) musi odbywać się bezpiecznie wątkowo (`Dispatcher.InvokeAsync`, `IProgress<T>` lub naturalny powrót `await` na `DispatcherSynchronizationContext`). Interfejs użytkownika nie może ulegać zamrożeniu.
3.  **\[4 pkt\] Monitor Rywalizacji Wątków i Porównania Wydajności:**
    - Wyświetlanie na żywo: najlepszego kosztu rozwiązania (`Best Result`), ID wątku z puli, który go wyliczył (`Leader Thread #ID`), łącznej liczby przeliczonych rozwiązań (`Solution Count`) oraz przepustowości (**Rozwiązań / sekundę** – mierzącej wydajność silnika TPL względem Kolejki Wątków).
4.  **\[4 pkt\] Dynamiczne wstrzykiwanie heurystyki w locie przez kompilator Roslyn (`C# Scripting API`):**
    - Pole tekstowe w GUI pozwalające użytkownikowi wprowadzić w języku C# własny operator mutacji lub lokalnej poprawy trasy, który przed startem obliczeń jest kompilowany w czasie wykonania za pomocą **`CSharpScript.Create<...>`** (`Microsoft.CodeAnalysis.CSharp.Scripting`) i wykonywany przez wątki robocze.

------------------------------------------------------------------------

### 4. Scenariusze Weryfikacyjne (Lista Kontrolna przed Oddaniem Projektu)

Przed przesłaniem rozwiązania przetestuj w aplikacji następujący scenariusz odbiorczy:

1.  **Test ochrony `Mutex` oraz `Semaphore` (Kernel Mode):**
    - Uruchom aplikację `OptiSync.UI.WPF`, a następnie spróbuj uruchomić drugie okno `.exe` – druga instancja musi wykryć zajęty `Mutex`, wyświetlić komunikat ostrzegawczy i zamknąć się.
2.  **Test responsywności UI i skalowania `SizeChanged`:**
    - Wczytaj plik `wi29.tsp` (Sahara Zachodnia – 29 miast, optimum: ok. `27603`) lub `dj38.tsp` (Dżibuti – 38 miast, optimum: ok. `6656`) i uruchom obliczenia dla `N = 8` wątków.
    - W trakcie trwających obliczeń płynnie zmieniaj rozmiar okna WPF – rysunek rozwiązania musi natychmiastowo dopasowywać się do nowego rozmiaru bez zacinania interfejsu.
    - Spróbuj zamknąć okno podczas trwających obliczeń – przycisk **Exit** oraz zamknięcie okna muszą być zablokowane.
3.  **Test bramki `ManualResetEventSlim` (Pauza / Wznowienie przy 0% CPU):**
    - Otwórz Menedżer Zadań Windows i obserwuj proces `OptiSync.Worker.TPL.exe`.
    - Kliknij przycisk **Pause** w GUI – postęp zatrzymuje się, a zużycie CPU przez proces roboczy musi natychmiast spaść do **0%**. Kliknij **Resume** – obliczenia wznawiają pracę z tego samego miejsca.
4.  **Test synchronizacji fazowej na `Barrier` i dynamicznej zmiany czasu:**
    - Ustaw czas Fazy 1 na 5 s oraz Fazy 2 na 5 s i obserwuj na pasku stanu przejścia: `Epoka 1: Faza 1` → *Bariera (Selekcja mediany)* → `Epoka 1: Faza 2` → `Epoka 2`.
    - W trakcie trwania obliczeń zmień w GUI czas Fazy 2 z 5 s na 2 s – kolejna faza musi zakończyć się zgodnie z nowym limitem czasu.
5.  **Test porównania wydajności `Worker.TPL` vs `Worker.Threads`:**
    - Uruchom obliczenia na 20 s dla silnika **TPL**, zanotuj liczbę przeliczonych instancji (`Solution Count` z licznika `Interlocked`), a następnie powtórz test dla silnika **Thread Queue** przy tych samych parametrach.
6.  **Test bezpiecznego anulowania (`CancellationToken`) i zamykania procesów:**
    - Kliknij przycisk **Stop** w połowie epoki – proces roboczy musi natychmiast przerwać pętle, zwolnić semafor systemowy i zamknąć potok IPC, a w GUI najlepszy dotychczasowy wynik pozostaje widoczny na ekranie i przycisk **Exit** staje się aktywny.

------------------------------------------------------------------------

### 5. Tabela Podsumowująca Oceny (Suma: 120 pkt / 120%)

|  Część   | Warstwa / Moduł                                          | Weryfikowane mechanizmy współbieżności, synchronizacji i wymagania techniczne                                                                                                                                                                                                                                                                                            |       Punkty       |
|:--------:|:---------------------------------------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|:------------------:|
|  **1**   | **Projekt `OptiSync.Shared` (Algorytmy i No-Sync)**      | Parser plików benchmarkowych (`.tsp` / `.vrp` / `.col`), niemodyfikowalne rekordy (`record`), potok 3 heurystyk (Faza 0: Konstrukcja, Faza 1: PMX/OX, Faza 2: 2-opt/3-opt) (20 pkt) + izolacja buforów roboczych przez `ThreadLocal<T>` i `Random.Shared` (5 pkt).                                                                                                       |     **25 pkt**     |
|  **2**   | **Synchronizacja Fazowa i Wewnątrzprocesowa**            | Koordynacja `CountdownEvent` (Faza 0) oraz `Barrier` z `postPhaseAction` (odrzucanie poniżej mediany między fazami) (12 pkt), ochrona najlepszego wyniku przez `ReaderWriterLockSlim` i `Lock` (10 pkt), liczniki atomowe `Interlocked` (w tym CAS) + `ConcurrentDictionary` (7 pkt), pauza na `ManualResetEventSlim` i dynamiczne timeouty `CancellationToken` (6 pkt). |     **35 pkt**     |
|  **3**   | **Silniki `Worker.TPL` vs `Worker.Threads` + Kernel OS** | Proces roboczy oparty o TPL (`Task` / `Parallel` + `Channel<T>`) (8 pkt), proces roboczy oparty o kolejkę wątków (`ThreadPool` / `Thread` + `BlockingCollection<T>`) (7 pkt) oraz synchronizacja międzyprocesowa Kernel Mode: nazwany `Mutex` i nazwany `Semaphore` (5 pkt).                                                                                             |     **20 pkt**     |
|  **4**   | **Asynchroniczna Komunikacja IPC i Timery**              | Dwukierunkowa komunikacja IPC (`NamedPipeServerStream` / `ClientStream`) bez blokowania wątków (8 pkt), asynchroniczna sekcja krytyczna `SemaphoreSlim(1,1)` na zapisie do potoku + puls telemetrii z `PeriodicTimer` (6 pkt), odbiór przez strumień `IAsyncEnumerable<T>` (`await foreach`) (6 pkt).                                                                    |     **20 pkt**     |
|  **5**   | **Interfejs Graficzny (`OptiSync.UI.WPF` + Roslyn)**     | Panel sterowania (Start/Pause/Resume/Stop, blokada Exit, dynamiczne czasy faz) (6 pkt), skalowalna wizualizacja 2D (`SizeChanged`, bezpieczna aktualizacja UI) (6 pkt), statystyki liderów wątków i przepustowości (4 pkt) + dynamiczna kompilacja skryptu heurystyki w `Roslyn` (4 pkt).                                                                                |     **20 pkt**     |
| **SUMA** |                                                          | **Pełna realizacja zadania (wraz z pulą dodatkową +20%)**                                                                                                                                                                                                                                                                                                                | **120 pkt (120%)** |
