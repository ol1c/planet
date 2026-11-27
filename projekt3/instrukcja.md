# Programowanie Aplikacji Lokalnych / Architektura Systemów .NET

**Rok akademicki:** 2026/27

## Instrukcja laboratoryjna: Wielozadaniowość, Programowanie Asynchroniczne (Maszyna Stanów), Równoległość (TPL) i Mechanizmy Synchronizacji w .NET (.NET 8/9/10)

**Prowadzący:** Tomasz Goluch  
**Wersja:** 1.0

------------------------------------------------------------------------

### 1. Wprowadzenie: Geneza Wielozadaniowości – Współbieżność, Równoległość i Asynchroniczność

**Cel:** Zrozumienie przyczyn odejścia od wykonywania sekwencyjnego, ścisłe rozgraniczenie pojęć przetwarzania współbieżnego, równoległego i asynchronicznego oraz wyeliminowanie najczęstszych błędów myślowych.

#### 1. Geneza: Dlaczego wykonywanie sekwencyjne przestało wystarczać?

Przez pierwsze dekady informatyki programy wykonywały się **ściśle sekwencyjnie (wsadowo)** – procesor pobierał kolejne instrukcje, a gdy program żądał odczytu danych z powolnego dysku lub karty sieciowej, cała maszyna bezczynnie czekała milisekundy lub sekundy na zakończenie operacji wejścia/wyjścia (I/O). Rozwój architektury komputerów i wymagań użytkowników wymusił fundamentalną zmianę paradygmatu z trzech powodów:

1.  **Koniec darmowego wzrostu taktowania (Zjawisko *Power Wall* ok. 2004 r.):** Przez lata wydajność aplikacji rosła automatycznie wraz ze wzrostem częstotliwości zegara pojedynczego rdzenia. Po przekroczeniu bariery ~4 GHz dalsze podnoszenie taktowania powodowało wykładniczy wzrost wydzielanego ciepła, którego nie dało się odprowadzić konwencjonalnym chłodzeniem. Producenci procesorów zamiast przyspieszać pojedynczy rdzeń zaczęli umieszczać w układach **wiele rdzeni fizycznych (8, 16, 64+)**. Program napisany jednowątkowo potrafi wykorzystać tylko jeden rdzeń, marnując ponad 90% mocy współczesnego procesora.
2.  **Responsywność interfejsu użytkownika (UI Thread Affinity):** W aplikacjach desktopowych (WPF, WinUI, MAUI) oraz mobilnych pojedynczy główny wątek UI odpowiada zarówno za rysowanie okna (60–120 klatek/s), jak i obsługę myszy oraz klawiatury. Zablokowanie tego wątku na operacji trwającej powyżej **50–100 ms** powoduje zamrożenie okna i komunikat systemu *„Brak odpowiedzi”*.
3.  **Przepać prędkości między CPU a operacjami I/O (*I/O Bottleneck*) i skalowalność serwerów:** Procesor wykonuje instrukcję w ułamku nanosekundy (<span class="math inline">\\10^{-9}\text{ s}\\</span>), podczas gdy zapytanie do bazy danych lub serwera HTTP przez sieć trwa dziesiątki milisekund (<span class="math inline">\\10^{-2}\text{ s}\\</span> – różnica **siedmiu rzędów wielkości!**). Gdyby serwer webowy blokował osobny wątek dla każdego oczekującego połączenia sieciowego, wyczerpałby pamięć RAM już przy kilku tysiącach klientów (problem *C10k / Thread Pool Starvation*).

------------------------------------------------------------------------

#### 2. Trzy wymiary wielozadaniowości: Współbieżność vs Równoległość vs Asynchroniczność

W potocznym języku programistycznym pojęcia te są często błędnie używane zamiennie. Z inżynierskiego punktu widzenia oznaczają trzy zupełnie różne mechanizmy:

``` text
1. WSPÓŁBIEŻNOŚĆ (Concurrency) — Przeplatanie w czasie (nawet na 1 rdzeniu CPU):
   Rdzeń CPU 1:  [ Zadanie A ][ Zadanie B ][ Zadanie A ][ Zadanie B ][ Zadanie A ]
   -------------------------------------------------------------------------------► Czas

2. RÓWNOLEGŁOŚĆ (Parallelism) — Fizyczne jednoczesne wykonanie na wielu rdzeniach:
   Rdzeń CPU 1:  [═══════════════ Zadanie A (Obliczenia) ═══════════════]
   Rdzeń CPU 2:  [═══════════════ Zadanie B (Obliczenia) ═══════════════]
   -------------------------------------------------------------------------------► Czas

3. ASYNCHRONICZNOŚĆ (Asynchrony) — Oczekiwanie na I/O BEZ zajmowania wątku CPU:
   Wątek CPU:    [Start A] ──(Wątek wolny! Obsługuje UI / inne zadania)──► [Koniec A]
   Kontroler I/O:         \_____________[ Pobieranie z Sieci / DMA ]______/ (Sprzęt)
   -------------------------------------------------------------------------------► CzasKopiuj
```

- **Przetwarzanie Współbieżne (*Concurrency*):**  
  Właściwość **struktury programu**, w którym dwie lub więcej operacji postępuje naprzód w nakładających się przedziałach czasu (*overlapping time periods*). Współbieżność **nie wymaga wielu rdzeni procesora** – na procesorze jednordzeniowym system operacyjny realizuje ją poprzez *wielozadaniowość z wywłaszczaniem (Preemptive Multitasking)*, przełączając kontekst między wątkami tysiące razy na sekundę (tzw. *Time Slicing* / przeplatanie).

  > *Cytat Roba Pike'a:* **„Współbieżność dotyczy radzenia sobie z wieloma rzeczami naraz (struktura). Równoległość dotyczy robienia wielu rzeczy naraz (wykonanie).”**

- **Przetwarzanie Równoległe (*Parallelism*):**  
  Właściwość **fizycznego wykonania na sprzęcie**. Oznacza sytuację, w której w dokładnie tej samej nanosekundzie zegara dwa lub więcej rdzeni CPU fizycznie wykonuje instrukcje maszynowe (np. wątek 1 przetwarza pierwszą połowę macierzy na Rdzeniu 0, a wątek 2 drugą połowę na Rdzeniu 1). Równoległość jest podzbiorem współbieżności dedykowanym dla zadań obciążających procesor (**CPU-bound**).

- **Przetwarzanie Asynchroniczne (*Asynchrony*):**  
  Mechanizm **nieblokującego oczekiwania na zasoby zewnętrzne**, dedykowany dla operacji wejścia/wyjścia (**I/O-bound**: zapytania HTTP, odczyt plików, kwerendy SQL, timery). W modelu asynchronicznym wątek zleca operację sterownikowi urządzenia (np. karcie sieciowej lub kontrolerowi dysku NVMe wykorzystującemu transfer DMA) i **natychmiast wraca do puli wątków lub pętli UI**. Podczas gdy serwer bazy danych przygotowuje odpowiedź, po stronie naszej aplikacji **żaden wątek nie czeka ani nie jest zablokowany**! Dopiero gdy sprzęt zgłosi przerwanie o odebraniu pakietu (przez port ukończenia I/O – *IOCP* w jądrze systemu), wolny wątek z puli podejmuje dalsze wykonanie metody od miejsca jej zawieszenia (`await`).

#### Zestawienie porównawcze modeli przetwarzania w .NET

| Kryterium                          | Równoległość (*Parallelism*)                                             | Asynchroniczność (*Asynchrony*)                                                 | Klasyczna Wielowątkowość (*Multithreading*)                      |
|:-----------------------------------|:-------------------------------------------------------------------------|:--------------------------------------------------------------------------------|:-----------------------------------------------------------------|
| **Typ problemu**                   | **CPU-Bound** (ciężkie pętle, grafika, kryptografia, algorytmy).         | **I/O-Bound** (sieć, bazy danych, system plików, oczekiwanie).                  | Długożyjące procesy tła, pętle nasłuchujące, dedykowane potoki.  |
| **Główny cel**                     | **Przyspieszenie obliczeń** poprzez wykorzystanie wszystkich rdzeni CPU. | **Skalowalność i responsywność** poprzez nieblokowanie wątków podczas czekania. | Izolacja strumieni wykonania i kontrola priorytetów OS.          |
| **Czy wymaga dodatkowych wątków?** | **Tak** (tyle wątków roboczych, ile rdzeni logicznych CPU).              | **Nie!** W czasie trwania `await` operacja I/O używa **0 wątków**.              | **Tak** (każdy `new Thread()` alokuje osobny wątek w jądrze OS). |
| **Narzędzia w .NET**               | `Parallel.For` / `ForEach`, `PLINQ`, `Task.Run(...)`.                    | `async` / `await`, `Task<T>`, `ValueTask<T>`, `IAsyncEnumerable<T>`.            | `Thread`, `ThreadPool.QueueUserWorkItem`.                        |

------------------------------------------------------------------------

#### 3. Najczęstsze błędy myślowe (Mity architektoniczne)

> **❌ MIT 1: „Słowo kluczowe `async` lub `await` automatycznie tworzy nowy wątek w tle.”**  
> **✅ FAKT:** Samo wywołanie metody `async` rozpoczyna się **synchronicznie na dokładnie tym samym wątku**, który ją wywołał! Gdy metoda napotka `await` na niezakończonej operacji I/O (np. `await httpClient.GetAsync(...)`), żaden nowy wątek nie jest tworzony do „czekania”. Bieżący wątek zostaje uwolniony, a stan operacji jest zarejestrowany w strukturze systemowej *I/O Completion Port (IOCP)*.

> **❌ MIT 2: „Programowanie asynchroniczne (`async/await`) przyspiesza wykonywanie pojedynczej operacji.”**  
> **✅ FAKT:** Pojedyncze zapytanie do bazy danych wykonane przez `await dbContext.Studenci.ToListAsync()` trwa ułamek milisekundy **dłużej** niż synchroniczne `dbContext.Studenci.ToList()` (ze względu na drobny narzut maszyny stanów). Zysk z `async/await` nie polega na tym, że dysk czy sieć działają szybciej, lecz na tym, że **w czasie oczekiwania wątek nie jest zablokowany** – w aplikacji WPF okno nie zamarza, a w ASP.NET Core ten sam wątek może w międzyczasie obsłużyć 500 innych pasażerów!

> **❌ MIT 3: „Zrównoleglenie pętli za pomocą `Parallel.ForEach` zawsze przyspieszy działanie programu.”**  
> **✅ FAKT:** Zgodnie z **Prawem Amdahla**, zysk z równoległości jest ograniczony przez część sekwencyjną programu oraz narzut synchronizacji. Jeśli pojedyncza iteracja pętli wykonuje prostą operację arytmetyczną trwającą kilkadziesiąt nanosekund, koszt partycjonowania danych, synchronizacji wątków i unieważniania linii pamięci podręcznej procesora (*Cache Coherency*) sprawi, że `Parallel.ForEach` będzie **wielokrotnie wolniejszy** od zwykłej pętli `for`!

> **❌ MIT 4: „Skoro używam tylko `async/await` i nie tworzę ręcznie wątków `new Thread()`, w moim programie nie wystąpią wyścigi danych (Race Conditions).”**  
> **✅ FAKT:** Jeśli uruchomisz kilka zadań asynchronicznych jednocześnie (np. przed `await Task.WhenAll(t1, t2, t3)`) lub jeśli kontynuacja po `await` wykona się na wątku z puli `ThreadPool`, zadania te mogą modyfikować ten sam obiekt w pamięci w tym samym czasie, prowadząc do uszkodzenia stanu.

------------------------------------------------------------------------

### 2. Przetwarzanie Równoległe (CPU-Bound): Biblioteka TPL (`Task`, `Parallel`, `PLINQ`)

**Cel:** Opanowanie wysokopoziomowego zrównoleglania obliczeń na wielu rdzeniach procesora przy użyciu puli wątków i algorytmu *Work-Stealing*.

Współczesny .NET odchodzi od ręcznego tworzenia wątków systemowych (`new Thread()`) na rzecz biblioteki **TPL (*Task Parallel Library*)** z przestrzeni `System.Threading.Tasks`. TPL operuje na abstrakcji **Zadania (`Task`)** – lekkiego obiektu reprezentującego jednostkę pracy, zarządzanego przez systemową **Pulę Wątków (`ThreadPool`)**.

#### 1. Jak działa `ThreadPool` i algorytm podkradania pracy (*Work-Stealing*)?

Tworzenie i niszczenie wątku w systemie operacyjnym jest bardzo kosztowne. `ThreadPool` utrzymuje zestaw gotowych wątków roboczych dopasowany do liczby rdzeni logicznych procesora (`Environment.ProcessorCount`).

- Oprócz jednej kolejki globalnej, każdy wątek roboczy w puli posiada **własną kolejkę lokalną** zadań, co minimalizuje konflikty blokad.
- Gdy wątek A opróżni swoją kolejkę lokalną, zamiast bezczynnie zasypiać, zagląda na koniec kolejki sąsiedniego, przeciążonego wątku B i **„podkrada” mu oczekujące zadania (*Work-Stealing*)**, gwarantując 100% wysycenie wszystkich rdzeni CPU.

#### 2. Klasa `Parallel` oraz `PLINQ` (*Parallel LINQ*)

Do zrównoleglania niezależnych obliczeń procesorowych (CPU-bound) służą statyczne metody klasy `Parallel` oraz rozszerzenie `.AsParallel()`:

- **`Parallel.For` / `Parallel.ForEach`:** Automatycznie dzieli kolekcję wejściową na porcje (*Partitions*) i przetwarza je równolegle na wielu rdzeniach:

  ``` csharp
  List<string> imagePaths = GetImagesToProcess();

  // TPL automatycznie rozdzieli obrazy pomiędzy wszystkie rdzenie CPU:
  Parallel.ForEach(imagePaths, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, path =>
  {
      using var img = LoadImage(path);
      img.ApplyHeavyGaussianBlur();
      img.Save(path + "_processed.jpg");
  });Kopiuj
  ```

- **`Parallel.Invoke`:** Uruchamia równolegle kilka różnych metod i blokuje do momentu zakończenia ostatniej z nich (`Parallel.Invoke( ObliczMacierzA, ObliczMacierzB );`).

- **`PLINQ` (`AsParallel()`):** Pozwala zrównoleglić deklaratywne zapytania LINQ na kolekcjach w pamięci RAM:

  ``` csharp
  var liczbyPierwsze = Enumerable.Range(2, 10_000_000)
      .AsParallel()
      .WithDegreeOfParallelism(Environment.ProcessorCount)
      .Where(CzyLiczbaPierwsza)
      .ToList();Kopiuj
  ```

#### 3. Zadania potomne (`AttachedToParent`)

Domyślnie zadanie `Task.Run(...)` uruchomione wewnątrz innego zadania jest od niego całkowicie niezależne (*Detached*). Jeśli chcemy, aby zadanie nadrzędne zakończyło się dopiero po ukończeniu wszystkich swoich podzadań (drzewo zadań), używamy flagi `TaskCreationOptions.AttachedToParent` z `Task.Factory.StartNew`.

------------------------------------------------------------------------

### 3. Przetwarzanie Asynchroniczne (I/O-Bound): Anatomia Maszyny Stanów `async/await` i Strumienie

**Cel:** Zrozumienie transformacji kompilatora Roslyn dla metod `async/await`, roli `SynchronizationContext`, strumieni `IAsyncEnumerable<T>` oraz wyeliminowanie najgroźniejszych antywzorców asynchronicznych.

Podczas gdy biblioteka `Parallel` i `Task.Run` służą do zadań obciążających procesor (**CPU-bound**), słowa kluczowe **`async`** oraz **`await`** służą do nieblokującej obsługi operacji wejścia/wyjścia (**I/O-bound**) oraz utrzymania responsywności wątku UI.

#### 1. Anatomia Maszyny Stanów (`IAsyncStateMachine`): Co kompilator Roslyn robi z metodą `async`?

Wielu programistów sądzi, że słowo kluczowe `await` w jakiś sposób „zatrzymuje” wątek w połowie metody. W rzeczywistości w maszynie wirtualnej CLR **nie da się zawiesić jednej ramki stosu i oddać wątku innej metodzie**.

Gdy oznaczasz metodę słowem `async`, kompilator Roslyn całkowicie przepisuje jej ciało w kodzie pośrednim CIL, generując **strukturę Maszyny Stanów implementującą interfejs `IAsyncStateMachine`** (analogicznie do słowa kluczowego `yield return`):

``` csharp
// 1. KOD NAPISANY PRZEZ PROGRAMISTĘ:
public async Task<string> FetchAndFormatAsync(int userId)
{
    string url = $"https://api.example.com/users/{userId}";
    string rawData = await _httpClient.GetStringAsync(url);
    return rawData.Trim().ToUpperInvariant();
}Kopiuj
```

Kompilator Roslyn przekształca powyższą metodę w strukturę posiadającą pola przechowujące stan oraz metodę **`MoveNext()`** z instrukcją skoku `switch`:

``` csharp
// 2. UPROSZCZONY KOD WYGENEROWANY PRZEZ KOMPILATOR ROSLYN (Deasemblacja CIL -> C#):
[CompilerGenerated]
private struct <FetchAndFormatAsync>d__1 : IAsyncStateMachine
{
    public int <>1__state; // Stan maszyny: -1 = start/w trakcie, 0 = czeka na 1. await, -2 = zakończono
    public AsyncTaskMethodBuilder<string> <>t__builder;

    // Parametry i zmienne lokalne zostają "podniesione" (hoisted) do pól struktury,
    // aby przetrwały zwolnienie ramki stosu na czas oczekiwania I/O!
    public MyService <>4__this;
    public int userId;
    private string <url>5__2;
    private TaskAwaiter<string> <>u__1;

    public void MoveNext()
    {
        int num = <>1__state;
        string result;
        try
        {
            TaskAwaiter<string> awaiter;
            if (num != 0)
            {
                // === KROK A: Kod PRZED instrukcją await (Wykonywany synchronicznie!) ===
                <url>5__2 = $"https://api.example.com/users/{userId}";
                awaiter = <>4__this._httpClient.GetStringAsync(<url>5__2).GetAwaiter();

                // Optymalizacja Fast-Path: Jeśli dane są już w buforze (np. w cache),
                // NIE zawieszaj maszyny, tylko przejdź od razu dalej bez narzutu!
                if (!awaiter.IsCompleted)
                {
                    <>1__state = 0; // Zapamiętaj, że zatrzymaliśmy się na pierwszym await (stan 0)
                    <>u__1 = awaiter;

                    // Zarejestruj metodę MoveNext() jako callback po zakończeniu I/O
                    // (w tym momencie maszyna stanów jest przenoszona na stertę!)
                    <>t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);

                    // KLUCZOWY MOMENT: Zwykły return! Wątek wychodzi z metody i jest WOLNY!
                    return;
                }
            }
            else
            {
                // === KROK B: Powrót do metody PO zakończeniu operacji sieciowej ===
                awaiter = <>u__1;
                <>u__1 = default;
                <>1__state = -1;
            }

            // Odczytanie wyniku z awaitera (jeśli wystąpił wyjątek sieciowy, zostanie rzucony tutaj!)
            string rawData = awaiter.GetResult();
            result = rawData.Trim().ToUpperInvariant();
        }
        catch (Exception exception)
        {
            <>1__state = -2;
            // Złapanie wyjątku i zapisanie go wewnątrz zwracanego obiektu Task:
            <>t__builder.SetException(exception);
            return;
        }

        <>1__state = -2;
        // Oznaczenie zadania Task jako ukończonego sukcesem wraz z wynikiem:
        <>t__builder.SetResult(result);
    }
}Kopiuj
```

#### Kluczowe wnioski z działania Maszyny Stanów:

1.  **Początek jest zawsze synchroniczny:** Wszystko od początku metody aż do pierwszego niezakończonego `await` wykonuje się synchronicznie na wątku wywołującym.
2.  **Optymalizacja `IsCompleted` (*Fast-Path*) oraz `ValueTask<T>`:** Jeśli operacja, na którą czekamy, zakończyła się natychmiast (np. odczyt z bufora pamięci), `awaiter.IsCompleted` zwraca `true` i kod przechodzi dalej bez zawieszania metody i bez alokacji maszyny stanów na stercie. W ścieżkach gorących (wywoływanych miliony razy) użycie struktury **`ValueTask<T>`** zamiast klasy `Task<T>` eliminuje również alokację samego obiektu zadania na stercie!
3.  **Przechwytywanie kontekstu (`SynchronizationContext`):** W momencie wywołania `AwaitUnsafeOnCompleted`, builder sprawdza bieżący `SynchronizationContext.Current`:
    - **W aplikacji desktopowej (WPF / WinForms):** Istnieje `DispatcherSynchronizationContext`. Maszyna stanów zapamiętuje go i po zakończeniu pobierania danych z sieci automatycznie zleca wykonanie dalszej części metody (`MoveNext` dla stanu `0`) **z powrotem na głównym wątku UI**! Dzięki temu po linijce `await` możemy bezpiecznie przypisać wynik do `textBox.Text = result;` bez ręcznego wywoływania `Dispatcher.Invoke`.
    - **W aplikacji konsolowej lub ASP.NET Core:** `SynchronizationContext.Current` wynosi `null`. Część metody po `await` wykona się na dowolnym wolnym wątku z puli `ThreadPool`.

------------------------------------------------------------------------

#### 2. Siedem grzechów głównych w programowaniu asynchronicznym (Antywzorce)

##### Błąd 1: Blokowanie asynchroniczności (*Sync-over-Async*: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) i klasyczny Deadlock

Wywołanie właściwości `.Result` lub metody `.Wait()` na zadaniu w wątku posiadającym kontekst synchronizacji (np. w wątku UI aplikacji WPF) prowadzi do **natychmiastowego, stuprocentowego zakleszczenia (*Deadlock*)**:

``` csharp
// ❌ KATASTROFA (DEADLOCK W WPF):
private void Button_Click(object sender, RoutedEventArgs e)
{
    // 1. Wątek UI wywołuje metodę asynchroniczną i BLOKUJE SIĘ na .Result, czekając na koniec zadania:
    string data = FetchAndFormatAsync(10).Result;
    ResultTextBlock.Text = data;
}

public async Task<string> FetchAndFormatAsync(int id)
{
    // 2. await zapamiętuje SynchronizationContext wątku UI.
    string raw = await _httpClient.GetStringAsync($"https://api.example.com/{id}");

    // 3. Pobieranie z sieci się kończy! Maszyna stanów próbuje wrócić na Wątek UI,
    //    aby wykonać resztę metody (return raw.ToUpper()), ALE Wątek UI śpi zablokowany w kroku 1!
    //    Wątek UI czeka na koniec FetchAndFormatAsync, a FetchAndFormatAsync czeka na wolny Wątek UI!
    return raw.ToUpper();
}Kopiuj
```

- **Rozwiązanie nr 1 (Zasada *„Async all the way”*):** Nigdy nie blokuj zadań przez `.Result` ani `.Wait()`. Zmień sygnaturę wywołującego na asynchroniczną: `private async void Button_Click(...) { string data = await FetchAndFormatAsync(10); }`.
- **Rozwiązanie nr 2 (W bibliotekach klas – `.ConfigureAwait(false)`):** W kodzie bibliotek, które nie dotykają bezpośrednio kontrolek UI, dodawaj `.ConfigureAwait(false)`. Informuje to maszynę stanów: *„po zakończeniu I/O nie musisz wracać na oryginalny wątek UI – wykonaj resztę metody na dowolnym wolnym wątku z ThreadPool”*.

##### Błąd 2: Stosowanie `async void` poza procedurami obsługi zdarzeń UI

Metoda o sygnaturze `public async void SaveDataAsync()` to tzw. metoda *„wystrzel i zapomnij” (Fire-and-Forget)* o bardzo groźnych konsekwencjach:

- Wywołujący nie otrzymuje obiektu `Task`, więc **nie może użyć `await`** i nie wie, kiedy zapis się zakończył.
- **Wyjątki zabijają cały proces aplikacji!** W metodzie `async Task` wyjątek jest bezpiecznie pakowany do obiektu `Task`. W metodzie `async void` nieobsłużony wyjątek zostaje rzucony bezpośrednio na `SynchronizationContext` / `ThreadPool`, co powoduje natychmiastowe zamknięcie całej aplikacji!
- **Zasada:** Zawsze zwracaj `async Task`. Jedynym dopuszczalnym miejscem użycia `async void` są subskrybenci zdarzeń UI (np. `private async void Button_Click(object sender, RoutedEventArgs e)`), których sygnatura jest narzucona przez delegat `RoutedEventHandler` (i zawsze umieszczaj wewnątrz nich blok `try...catch`!).

##### Błąd 3: *Fake Async* – opakowywanie operacji I/O w `Task.Run` oraz `Thread.Sleep`

``` csharp
// ❌ ANTYWZORZEC (Marnowanie wątku z puli na czekanie I/O):
public async Task<string> BadReadAsync(string path) =>
    await Task.Run(() => File.ReadAllText(path));

// ✅ POPRAWNIE (Prawdziwe asynchroniczne I/O bez użycia dodatkowego wątku):
public async Task<string> GoodReadAsync(string path) =>
    await File.ReadAllTextAsync(path);Kopiuj
```

`Task.Run` służy **wyłącznie** do przeniesienia ciężkich obliczeń procesorowych (**CPU-bound**) z wątku UI na wątek roboczy z puli. Nigdy nie opakowuj w `Task.Run` operacji bazodanowych czy sieciowych i nigdy nie używaj wewnątrz metod `async` blokującego `Thread.Sleep(1000)` zamiast nieblokującego `await Task.Delay(1000)`.

##### Błąd 4: Ukryte `async void` w wyrażeniach lambda (`List<T>.ForEach`)

Metoda `lista.ForEach(Action<T>)` przyjmuje delegat `Action<T>` zwracający `void`. Przekazanie do niej lambdy `lista.ForEach(async item => await ProcessAsync(item));` kompiluje się jako seria niezaczekanych wywołań **`async void`**! Pętla kończy się natychmiast, podczas gdy operacje jeszcze trwają w tle. Zamiast tego zawsze używaj klasycznej pętli `foreach (var item in lista) { await ProcessAsync(item); }`.

##### Błąd 5: Sekwencyjny `await` w pętli zamiast równoległego `Task.WhenAll`

Gdy pobieramy dane z 10 niezależnych adresów URL, wywołanie `await` wewnątrz pętli `foreach` wykonuje zapytania jedno po drugim (czas sumaryczny: <span class="math inline">\\10 \times 200\text{ ms} = 2000\text{ ms}\\</span>). Jeśli operacje są niezależne i zasób na to pozwala (uwaga: pamiętaj, że `DbContext` w EF Core nie pozwala na równoległe zapytania!), uruchamiamy je razem i czekamy zbiorczo przez **`await Task.WhenAll(tasks)`** (czas sumaryczny: <span class="math inline">\\\approx 200\text{ ms}\\</span>).

##### Błąd 6: Pomijanie `async/await` wewnątrz bloku `using` (*Eliding async/await*)

``` csharp
// ❌ KRYTYCZNY BŁĄD (Przedwczesne zamknięcie strumienia / HttpClient / DbContext!):
public Task<string> GetContentBrokenAsync()
{
    using var client = new HttpClient();
    // Zwracamy sam Task BEZ słowa await! Metoda natychmiast wychodzi z bloku,
    // wywołuje client.Dispose() i przerywa trwające w tle połączenie sieciowe!
    return client.GetStringAsync("https://example.com");
}Kopiuj
```

Jeśli wewnątrz metody korzystasz z bloku `using` lub `try...catch`, **musisz** użyć słów `async` i `await`, aby kompilator umieścił wywołanie `Dispose()` wewnątrz maszyny stanów (dopiero po otrzymaniu wyniku z `await`).

------------------------------------------------------------------------

#### 3. Opakowywanie starych API w model Task: `TaskCompletionSource<T>`

Gdy integrujemy starą bibliotekę opartą na zdarzeniach lub callbackach z nowoczesnym kodem `async/await`, używamy klasy **`TaskCompletionSource<T>`**, która pozwala ręcznie sterować stanem zwracanego zadania (`SetResult`, `SetException`, `SetCanceled`):

``` csharp
public Task<string> WaitForHardwareClickAsync()
{
    var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    void OnButtonPressed(object? sender, HardwareEventArgs e)
    {
        _device.ButtonPressed -= OnButtonPressed;
        tcs.TrySetResult(e.ButtonCode); // Budzi wszystkie miejsca czekające na await!
    }

    _device.ButtonPressed += OnButtonPressed;
    return tcs.Task;
}Kopiuj
```

------------------------------------------------------------------------

#### 4. Asynchroniczne strumienie danych (`IAsyncEnumerable<T>`)

Tradycyjna metoda `async Task<List<T>>` ma istotne ograniczenie: wywołujący musi czekać, aż **wszystkie** elementy zostaną pobrane i zaalokowane w pamięci, zanim wyświetli pierwszy wiersz. Interfejs **`IAsyncEnumerable<T>`** łączy potęgę generatorów `yield return` z nieblokującym `await`:

``` csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Atrybut [EnumeratorCancellation] przekazuje token z metody .WithCancellation(token)
public static async IAsyncEnumerable<int> FetchSensorReadingsAsync(
    [EnumeratorCancellation] CancellationToken token = default)
{
    for (int i = 0; i < 5; i++)
    {
        token.ThrowIfCancellationRequested();

        // Symulacja asynchronicznego oczekiwania na odczyt z czujnika / bazy danych:
        await Task.Delay(Random.Shared.Next(500, 1500), token);

        // Zwrócenie pojedynczego elementu do konsumenta bez przerywania działania metody:
        yield return Random.Shared.Next(10, 100);
    }
}

// Konsumpcja strumienia za pomocą konstrukcji await foreach:
await foreach (int reading in FetchSensorReadingsAsync().WithCancellation(cts.Token))
{
    Console.WriteLine($"Odebrano pomiar w czasie rzeczywistym: {reading}");
}Kopiuj
```

------------------------------------------------------------------------

### 4. Kooperatywne Anulowanie Zadań (`CancellationToken`) oraz Mechanizmy Czasowe (`Timers`)

**Cel:** Bezpieczne przerywanie długotrwałych operacji za pomocą tokenów anulowania oraz właściwy dobór timerów do architektury aplikacji.

#### 1. Model kooperatywnego anulowania (`CancellationTokenSource` & `CancellationToken`)

W środowisku .NET brutalne zabijanie wątku z zewnątrz (`Thread.Abort`) jest zablokowane, ponieważ pozostawiało uszkodzone dane i niezamknięte uchwyty plików. Zamiast tego obowiązuje **model kooperatywny**: obiekt zewnętrzny wysyła prośbę o przerwanie pracy, a wykonywana metoda bezpiecznie kończy bieżący krok, zwalnia zasoby i przerywa działanie.

Architektura rozdziela rolę **zarządcy** od **odbiorcy** sygnału:

- **`CancellationTokenSource` (CTS):** Obiekt sterujący posiadający metody `Cancel()` oraz `CancelAfter(TimeSpan)` (automatyczny timeout). Implementuje `IDisposable`!
- **`CancellationToken` (CT):** Lekka, niemodyfikowalna struktura przekazywana jako parametr do metod asynchronicznych i pętli. Pozwala wyłącznie sprawdzić stan (`IsCancellationRequested`), rzucić standardowy wyjątek przerywający zadanie (**`ThrowIfCancellationRequested()`**) lub podpiąć callback (`Register(Action)`).

``` csharp
public async Task PerformLongWorkAsync(CancellationToken token)
{
    try
    {
        for (int i = 0; i <= 100; i += 10)
        {
            // 1. Weryfikacja w pętli obliczeniowej (rzuca OperationCanceledException):
            token.ThrowIfCancellationRequested();
            Console.WriteLine($"Postęp zadania: {i}%");

            // 2. Przekazanie tokenu w głąb wywoływanych metod asynchronicznych I/O:
            await Task.Delay(500, token);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Zadanie zostało bezpiecznie wycofane i wyczyszczone.");
        throw; // Ponowne rzucenie wyjątku informuje wywołującego, że Task ma status Canceled!
    }
}

// Łączenie wielu źródeł anulowania (np. przycisk "Anuluj" użytkownika ORAZ timeout 5 sekund):
using var userCts = new CancellationTokenSource();
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);

await PerformLongWorkAsync(linkedCts.Token);Kopiuj
```

------------------------------------------------------------------------

#### 2. Mechanizmy odmierzania czasu (`Timers`): Model *Push* vs Model *Pull*

Zamiast zamrażać wątek instrukcją `Thread.Sleep()`, do cyklicznego wykonywania operacji wykorzystujemy wyspecjalizowane timery dzielące się na dwie filozofie:

1.  **Model *Push* (Callback / Event):** Timer wywołuje naszą metodę zwrotną w osobnym wątku za każdym razem, gdy minie interwał. **Ryzyko (*Re-entrancy*):** Jeśli wykonanie naszej metody trwa 3 sekundy, a timer tyka co 1 sekundę, kolejne wywołania zaczną nakładać się na siebie równolegle!
2.  **Model *Pull* (Async Loop – `PeriodicTimer`):** Nasz kod sam czeka na kolejne tyknięcie wewnątrz pętli `while (await timer.WaitForNextTickAsync(token))`. Gwarantuje to brak nakładania się wywołań i pełną integrację z `async/await`.

| Cecha                                 | `PeriodicTimer` (.NET 6+)                                          | `DispatcherTimer` (WPF)                                   | `System.Threading.Timer`                                    | `System.Timers.Timer`                           |
|:--------------------------------------|:-------------------------------------------------------------------|:----------------------------------------------------------|:------------------------------------------------------------|:------------------------------------------------|
| **Wątek wykonania**                   | Kontekst wywołującego (`Async`)                                    | **Główny wątek UI (WPF)**                                 | Wątek roboczy (`ThreadPool`)                                | `ThreadPool` (lub komponent UI)                 |
| **Model pracy**                       | **Pull (`await` w pętli `while`)**                                 | Push (Zdarzenie `Tick`)                                   | Push (`TimerCallback`)                                      | Push (Zdarzenie `Elapsed`)                      |
| **Ryzyko nakładania (*Re-entrancy*)** | **Brak** (naturalna sekwencyjność)                                 | Brak (kolejka `Dispatcher`)                               | **Tak** (wiele wątków naraz!)                               | **Tak** (gdy `AutoReset = true`)                |
| **Główne zastosowanie**               | **Zalecany standard** dla pętli w tle i usług `BackgroundService`. | Odświeżanie zegarów i kontrolek bezpośrednio w oknie WPF. | Lekkie, niskopoziomowe zadania synchroniczne w puli wątków. | Kompatybilność ze starszym kodem komponentowym. |

``` csharp
// Nowoczesna pętla czasowa z PeriodicTimer (odporna na nakładanie się zadań):
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
int seconds = 0;

while (await timer.WaitForNextTickAsync(cancellationToken))
{
    seconds++;
    Console.WriteLine($"[Tick #{seconds}] Wykonywanie bezpiecznej pracy cyklicznej...");
}Kopiuj
```

------------------------------------------------------------------------

### 5. Ciemna Strona Współbieżności: Problemy Współdzielonego Stanu i Koszt Systemowy

**Cel:** Zrozumienie dlaczego równoległy dostęp wielu wątków do tej samej pamięci prowadzi do błędów nieliniowych oraz jaki jest koszt przełączania kontekstu między trybem użytkownika a jądrem systemu.

Gdy uruchamiamy wiele zadań równoległych (`Task.Run`, `Parallel.For`) lub obsługujemy współbieżne żądania operujące na wspólnych polach w pamięci, brak koordynacji prowadzi do czterech klasycznych awarii:

#### 1. Wyścig danych (*Race Condition*) i Zgubiona Aktualizacja (*Lost Update*)

Operacja `counter++;` w języku C# wygląda na pojedynczą instrukcję, jednak na poziomie procesora i kodu pośredniego CIL składa się z **trzech odrębnych kroków (*Read – Modify – Write*)**:

1.  **Odczyt (`ldfld`):** Pobranie wartości zmiennej `counter` z pamięci RAM do rejestru procesora (np. `0`).
2.  **Modyfikacja (`add`):** Zwiększenie wartości w rejestrze procesora o `1` (`0 + 1 = 1`).
3.  **Zapis (`stfld`):** Zapisanie wyniku z rejestru z powrotem do pamięci RAM.

``` text
Stan początkowy w pamięci RAM: counter = 0

Wątek 1 (Rdzeń 0)                           Wątek 2 (Rdzeń 1)
──────────────────────────────────────────  ──────────────────────────────────────────
1. Odczytuje counter z RAM (widzi: 0)
                                            1. Odczytuje counter z RAM (widzi: 0!)
2. Dodaje 1 w rejestrze RAX (wynik: 1)      2. Dodaje 1 w rejestrze RBX (wynik: 1)
3. Zapisuje 1 do RAM (counter = 1)
                                            3. Nadpisuje RAM wartością 1 (counter = 1!)Kopiuj
```

**Skutek:** Dwa wątki wykonały inkrementację, ale końcowa wartość w pamięci wynosi `1` zamiast `2` – jedna aktualizacja została bezpowrotnie utracona!

#### 2. Widoczność pamięci (*Memory Visibility*), Bufory Rdzeni i Reordering

Współczesne procesory nie czytają każdej zmiennej bezpośrednio z pamięci RAM, lecz utrzymują lokalne pamięci podręczne dla każdego rdzenia (**Cache L1 / L2** oraz bufory zapisu *Store Buffers*). Co więcej, kompilator RyuJIT oraz sam procesor mogą zmieniać kolejność instrukcji (*Out-of-Order Execution*), o ile w ramach pojedynczego wątku wynik się nie zmienia. Bez użycia barier pamięciowych (*Memory Barriers* / `volatile` / `lock` / `Interlocked`), wątek kręcący się w pętli `while (!isStopped)` na Rdzeniu 1 może **nigdy nie zauważyć**, że wątek na Rdzeniu 0 zmienił flagę `isStopped = true`!

#### 3. Uszkodzenie wewnętrznych struktur kolekcji (*State Corruption*)

Standardowe kolekcje z przestrzeni `System.Collections.Generic` (`List<T>`, `Dictionary<K,V>`, `Queue<T>`) **nie są bezpieczne wątkowo**. Gdy dwa wątki w tej samej chwili wywołają `list.Add(item)` w momencie przekroczenia pojemności wewnętrznej tablicy (`_items`), dochodzi do wyścigu przy realokacji bufora i przesuwaniu indeksu `_size` – skutkuje to nadpisaniem elementów, wpisaniem wartości `null` lub rzuceniem wyjątku `IndexOutOfRangeException`.

#### 4. Zakleszczenie (*Deadlock*), Uwięzienie (*Livelock*) i Zagłodzenie (*Starvation*)

- **Deadlock (Zakleszczenie):** Wątek 1 zajmuje Zasób A i czeka na zwolnienie Zasobu B, podczas gdy Wątek 2 zajął Zasób B i czeka w nieskończoność na zwolnienie Zasobu A.
- **Livelock (Uwięzienie aktywne):** Dwa wątki próbują uniknąć blokady, stale ustępując sobie nawzajem w pętli – procesor jest obciążony w 100%, ale żaden wątek nie posuwa pracy do przodu.

------------------------------------------------------------------------

#### Koszt wątków i tryby pracy: *User Mode* vs *Kernel Mode*

Aby wybrać właściwe narzędzie do rozwiązania powyższych problemów, musimy uwzględnić architekturę systemu operacyjnego:

- **Koszt pamięciowy i czasowy wątku:** Każdy wątek systemowy rezerwuje domyślnie **1 MB pamięci wirtualnej na stos wątku** oraz struktury administracyjne (*Thread Environment Block – TEB*). Gdy wątków jest więcej niż rdzeni, planista OS wykonuje **przełączanie kontekstu (*Context Switch*)** – zapisuje rejestry CPU bieżącego wątku, ładuje rejestry kolejnego i wytrąca pamięć podręczną procesora (*Cache Miss*).
- **Tryb Użytkownika (*User Mode*):** Instrukcje wykonują się bezpośrednio w przestrzeni procesu aplikacji (nanosekundy) bez udziału jądra systemu operacyjnego.
- **Tryb Jądra (*Kernel Mode*):** Wywołanie funkcji systemowej (*System Call*) zmusza procesor do przejścia w uprzywilejowany tryb jądra OS i uśpienia wątku, co jest **rzędy wielkości wolniejsze** (mikrosekundy) niż operacja w trybie użytkownika.

**Trzy reguły doboru mechanizmu synchronizacji (Od najlżejszych do najcięższych):**

1.  **Poziom 0 – Brak synchronizacji (*No-Sync*):** Niemodyfikowalność (`record`), izolacja (`ThreadLocal<T>`) lub partycjonowanie danych.
2.  **Poziom 1 – Tryb użytkownika (*User Mode*):** Operacje atomowe CPU (`Interlocked`), kolekcje współbieżne (`ConcurrentDictionary`), sekcje krytyczne (`lock` / `Monitor`).
3.  **Poziom 2 – Tryb hybrydowy (*Hybrid Mode*):** Lekkie prymitywy z prefiksem `*Slim` (`SemaphoreSlim`, `ManualResetEventSlim`, `ReaderWriterLockSlim`), które najpierw krótko kręcą się w pętli w *User Mode* (*Spinning*), a dopiero przy dłuższym oczekiwaniu przechodzą do jądra.
4.  **Poziom 3 – Tryb jądra (*Kernel Mode*):** Ciężkie obiekty systemowe (`Mutex`, `Semaphore`, `AutoResetEvent`, `ManualResetEvent`), stosowane głównie do synchronizacji **między różnymi procesami (IPC)**.

------------------------------------------------------------------------

### 6. Strategia „No-Sync”: Kiedy i jak unikać synchronizacji?

**Cel:** Eliminacja konieczności stosowania blokad poprzez właściwy projekt struktur danych.

Najwydajniejsza blokada to taka, której w ogóle nie trzeba zakładać. W trzech poniższych scenariuszach możemy całkowicie wyeliminować synchronizację:

#### 1. Niezmienność (*Immutability* – `readonly` oraz `record`)

Obiekty, których stanu nie da się zmodyfikować po zakończeniu konstruktora, są **naturalnie bezpieczne wątkowo**. Dowolna liczba wątków może jednocześnie odczytywać ten sam obiekt bez żadnych blokad:

``` csharp
// Niemodyfikowalny rekord: bezpieczny do współdzielenia między wszystkimi wątkami
public record AppConfig(string ApiEndpoint, int MaxConnections, TimeSpan Timeout);Kopiuj
```

#### 2. Izolacja danych na poziomie wątku (*Thread Locality* – `ThreadLocal<T>` oraz `Random.Shared`)

Zamiast zmuszać 16 wątków do walki o jeden współdzielony obiekt pomocniczy (np. bufor roboczy lub generator liczb losowych), dajemy każdemu wątkowi jego własną, prywatną instancję za pomocą klasy **`ThreadLocal<T>`**:

``` csharp
// Każdy wątek otrzyma własną, odizolowaną instancję obiektu StringBuilder:
private static readonly ThreadLocal<StringBuilder> _localBuffer =
    new(() => new StringBuilder(1024));

public string BuildRow(int id)
{
    var sb = _localBuffer.Value!;
    sb.Clear();
    return sb.Append("Row_").Append(id).ToString();
}Kopiuj
```

*(W przypadku generatora liczb losowych `Random`, począwszy od .NET 6 zamiast ręcznego tworzenia `ThreadLocal<Random>` możemy używać wbudowanej właściwości **`Random.Shared.Next()`**, która pod maską automatycznie realizuje bezpieczną izolację wątkową).*

#### 3. Partycjonowanie danych (*Data Partitioning* i unikanie *False Sharing*)

Gdy przetwarzamy dużą tablicę, dzielimy ją na rozłączne przedziały indeksów (np. Wątek 0 przetwarza elementy `0..999`, a Wątek 1 elementy `1000..1999`).

> **⚠️ Pułapka sprzętowa: *False Sharing (Fałszywe Współdzielenie)***  
> Pamięć podręczna procesora (Cache L1/L2) nie wymienia z pamięcią RAM pojedynczych zmiennych `int` (4 bajty), lecz całe **linie pamięci podręcznej (*Cache Lines*) o rozmiarze 64 bajtów**. Jeśli dwa wątki na różnych rdzeniach w pętli modyfikują dwie sąsiadujące komórki tablicy `liczniki[0]` oraz `liczniki[1]` (leżące w tej samej 64-bajtowej linii cache!), sprzętowy protokół spójności pamięci (*MESI*) będzie przy każdym zapisie unieważniał linię cache drugiego rdzenia, powodując drastyczny spadek wydajności! Dlatego każdy wątek powinien sumować dane w swojej zmiennej lokalnej na stosie i zapisać wynik końcowy tylko raz.

------------------------------------------------------------------------

### 7. Synchronizacja w Trybie Użytkownika (User Mode)

**Cel:** Opanowanie najszybszych mechanizmów synchronizacji wewnątrzprocesowej: instrukcji atomowych procesora (`Interlocked`), kolekcji współbieżnych, monitorów (`lock`) oraz blokad odczytu/zapisu.

#### 1. Operacje atomowe procesora: Klasa `System.Threading.Interlocked`

Klasa `Interlocked` tłumaczy wywołania C# bezpośrednio na sprzętowe instrukcje atomowe procesora (np. `LOCK XADD`, `LOCK CMPXCHG` na architekturze x86-64). Operacja atomowa jest niepodzielna – żaden inny rdzeń procesora nie może zobaczyć jej stanu pośredniego:

- **`Interlocked.Increment(ref int location)` / `Decrement`:** Atomowo zwiększa/zmniejsza zmienną o `1` i zwraca nową wartość.
- **`Interlocked.Add(ref int location, int value)`:** Atomowo dodaje wartość całkowitą.
- **`Interlocked.Exchange(ref T location, T value)`:** Atomowo podmienia wartość zmiennej na nową i zwraca jej **poprzednią (starą)** wartość.
- **`Interlocked.CompareExchange(ref T location, T value, T comparand)` (Operacja CAS – *Compare-And-Swap*):** Atomowo porównuje bieżącą wartość zmiennej z `comparand`. Jeśli są równe – wpisuje `value`. Zawsze zwraca **oryginalną wartość** zastaną w zmiennej.

##### Wzorzec pętli bezblokadowej (*Lock-Free Retry Loop* z użyciem CAS)

Za pomocą `CompareExchange` możemy zaimplementować dowolną operację matematyczną (np. mnożenie lub funkcję nieliniową) bez użycia blokady `lock`:

``` csharp
int oldValue, newValue;
do
{
    // 1. Odczytujemy bieżący stan zmiennej współdzielonej (optymistycznie)
    oldValue = Volatile.Read(ref _sharedScore);

    // 2. Wyliczamy nową wartość lokalnie
    newValue = (oldValue + 10) * 2;

    // 3. Próbujemy atomowo zapisać newValue TYLKO WTEDY, gdy w międzyczasie
    //    żaden inny wątek nie zmienił _sharedScore (czyli nadal wynosi oldValue):
} while (Interlocked.CompareExchange(ref _sharedScore, newValue, oldValue) != oldValue);Kopiuj
```

> **⚠️ Ograniczenie algorytmów Lock-Free: Problem ABA**  
> Samodzielne pisanie złożonych struktur bezblokadowych (np. stosów czy list wiązanych) jest narażone na tzw. **Problem ABA**: Wątek 1 odczytuje wskaźnik wierzchołka stosu `A` i zostaje wywłaszczony. W tym czasie Wątek 2 zdejmuje ze stosu `A`, zdejmuje `B`, a następnie alokuje nowy element pod tym samym adresem pamięci `A` i odkłada go na stos. Gdy Wątek 1 wznawia pracę, `CompareExchange` widzi, że na szczycie nadal jest adres `A` i błędnie uznaje, że struktura stosu nie uległa zmianie, uszkadzając wskaźniki! Dlatego zamiast pisać własne struktury lock-free, w kodzie produkcyjnym zawsze korzystamy z gotowych kolekcji współbieżnych.

------------------------------------------------------------------------

#### 2. Bezpieczne wątkowo kolekcje (`System.Collections.Concurrent`)

Zamiast opakowywać zwykłą `List<T>` lub `Dictionary<K,V>` w pojedynczą blokadę `lock` (która wstrzymuje wszystkie wątki przy każdym odczycie), stosujemy zoptymalizowane kolekcje współbieżne wykorzystujące operacje atomowe CAS oraz drobnoziarniste blokowanie koszyków (*Fine-Grained Locking*):

| Klasa kolekcji                  | Charakterystyka i zastosowanie                                                                                                                              | Kluczowe metody                                               |
|:--------------------------------|:------------------------------------------------------------------------------------------------------------------------------------------------------------|:--------------------------------------------------------------|
| **`ConcurrentDictionary<K,V>`** | Wielowątkowy słownik klucz-wartość. Odczyty są całkowicie bezblokadowe (*lock-free*), a zapisy blokują tylko pojedynczy koszyk skrótu.                      | `TryAdd`, `TryGetValue`, **`GetOrAdd`**, **`AddOrUpdate`**    |
| **`ConcurrentQueue<T>`**        | Bezblokadowa kolejka **FIFO** (*First-In, First-Out*).                                                                                                      | `Enqueue`, `TryDequeue`, `TryPeek`                            |
| **`ConcurrentStack<T>`**        | Bezblokadowy stos **LIFO** (*Last-In, First-Out*).                                                                                                          | `Push`, `TryPop`, `TryPeek`                                   |
| **`ConcurrentBag<T>`**          | Nieuporządkowany zbiór obiektów zoptymalizowany pod scenariusze, w których **ten sam wątek zarówno dodaje, jak i pobiera** elementy (np. pula obiektów).    | `Add`, `TryTake`                                              |
| **`BlockingCollection<T>`**     | Klasyczna realizacja wzorca **Producent-Konsument**. Pozwala uśpić wątek konsumenta, gdy kolejka jest pusta, oraz ograniczyć pojemność bufora (*Bounding*). | `Add`, `Take`, `GetConsumingEnumerable()`, `CompleteAdding()` |

*(W nowoczesnym kodzie asynchronicznym `async/await` odpowiednikiem `BlockingCollection<T>` jest przestrzeń **`System.Threading.Channels` (`Channel<T>`)**, oferująca w pełni nieblokujące metody `Writer.WriteAsync` oraz `Reader.ReadAllAsync`).*

------------------------------------------------------------------------

#### 3. Sekcje krytyczne: `Monitor`, słowo kluczowe `lock` oraz `System.Threading.Lock` (.NET 9+)

Gdy musimy atomowo wykonać blok kodu modyfikujący kilka powiązanych zmiennych w obrębie jednego procesu, używamy sekcji krytycznej **`lock`**.

``` csharp
private readonly object _syncRoot = new();

public void TransferFunds(decimal amount)
{
    lock (_syncRoot)
    {
        // Tylko jeden wątek naraz może wykonywać ten blok kodu
        _sourceBalance -= amount;
        _targetBalance += amount;
    }
}Kopiuj
```

Kompilator Roslyn tłumaczy instrukcję `lock (_syncRoot)` dla typu `object` na wywołanie klasy `Monitor` wewnątrz bloku `try...finally`:

``` csharp
bool lockTaken = false;
try
{
    Monitor.Enter(_syncRoot, ref lockTaken);
    // ... kod sekcji krytycznej ...
}
finally
{
    if (lockTaken) Monitor.Exit(_syncRoot);
}Kopiuj
```

> **💡 Nowość w .NET 9 / .NET 10 i C# 13: Dedykowana klasa `System.Threading.Lock`**  
> Dawniej blokada `lock` opierała się na nagłówku każdego obiektu (`SyncBlockIndex`). Od .NET 9 wprowadzono dedykowany typ `private readonly Lock _syncObj = new();`. Gdy kompilator C# 13+ widzi `lock (_syncObj)` na tym typie, zamiast starszego `Monitor.Enter` generuje szybsze wywołanie `using (_syncObj.EnterScope())`.

##### Unikanie zakleszczeń (Deadlock) za pomocą `Monitor.TryEnter` z limitem czasu

Klasyczne `lock` czeka w nieskończoność. Jeśli Wątek 1 potrzebuje dwóch zasobów (`lock(A)` i `lock(B)`), a Wątek 2 zajmuje je w odwrotnej kolejności (`lock(B)` i `lock(A)`), aplikacja zawiśnie na zawsze. Użycie **`Monitor.TryEnter`** pozwala wycofać się po przekroczeniu zadanego czasu:

``` csharp
private readonly object _locker = new();

public bool TryDoWorkWithTimeout()
{
    bool lockTaken = false;
    try
    {
        // Próbujemy zająć blokadę przez maksymalnie 2 sekundy:
        Monitor.TryEnter(_locker, TimeSpan.FromSeconds(2), ref lockTaken);
        if (!lockTaken)
        {
            Console.WriteLine("OSTRZEŻENIE: Zasób zajęty powyżej 2s – wycofanie operacji (ochrona przed Deadlockiem).");
            return false;
        }

        // Bezpieczna praca na zasobie...
        return true;
    }
    finally
    {
        if (lockTaken) Monitor.Exit(_locker);
    }
}Kopiuj
```

*(Uwaga: Wewnątrz bloku `lock` **nie wolno** używać operatora `await`! Do synchronizacji kodu asynchronicznego służy `SemaphoreSlim.WaitAsync()`).*

------------------------------------------------------------------------

#### 4. Optymalizacja częstych odczytów: `ReaderWriterLockSlim`

Zwykły `lock` blokuje dostęp wszystkim wątkom – nawet wtedy, gdy 99 wątków chce wyłącznie **odczytać** dane ze struktury, a tylko 1 wątek raz na minutę je aktualizuje. Klasa **`ReaderWriterLockSlim`** rozdziela te uprawnienia:

- **Blokada odczytu (`EnterReadLock`):** Pozwala **wielu wątkom jednocześnie** czytać dane bez blokowania się nawzajem.
- **Blokada zapisu (`EnterWriteLock`):** Zapewnia **całkowitą wyłączność** – czeka, aż obecni czytelnicy wyjdą, i na czas zapisu nie wpuszcza żadnego innego czytelnika ani pisarza.

``` csharp
private readonly ReaderWriterLockSlim _rwLock = new();
private readonly Dictionary<int, string> _cache = new();

public string? ReadFromCache(int key)
{
    _rwLock.EnterReadLock();
    try
    {
        return _cache.TryGetValue(key, out var val) ? val : null;
    }
    finally
    {
        _rwLock.ExitReadLock(); // Zawsze zwalniaj w bloku finally!
    }
}

public void WriteToCache(int key, string value)
{
    _rwLock.EnterWriteLock();
    try
    {
        _cache[key] = value;
    }
    finally
    {
        _rwLock.ExitWriteLock();
    }
}Kopiuj
```

------------------------------------------------------------------------

#### 5. Bezpieczna leniwa inicjalizacja: *Double-Checked Locking* vs `Lazy<T>`

Gdy tworzenie obiektu jest bardzo kosztowne (np. ładowanie modelu ML lub konfiguracji), chcemy odroczyć jego inicjalizację do momentu pierwszego użycia.

``` csharp
// Wariant 1: Klasyczny wzorzec Double-Checked Locking
// UWAGA: Pole _instance MUSI być oznaczone jako 'volatile', aby zapobiec
// zmianie kolejności instrukcji (reordering) przez kompilator JIT i CPU!
private volatile ExpensiveService? _instance;
private readonly object _initLock = new();

public ExpensiveService Instance
{
    get
    {
        if (_instance is null) // 1. Szybkie sprawdzenie bez kosztownej blokady lock
        {
            lock (_initLock)
            {
                if (_instance is null) // 2. Ponowne sprawdzenie wewnątrz sekcji krytycznej
                {
                    _instance = new ExpensiveService();
                }
            }
        }
        return _instance;
    }
}

// Wariant 2 (ZALECANY W .NET): Wbudowana, bezpieczna wątkowo klasa Lazy<T>
private readonly Lazy<ExpensiveService> _lazyService =
    new(() => new ExpensiveService(), LazyThreadSafetyMode.ExecutionAndPublication);

public ExpensiveService SafeInstance => _lazyService.Value;Kopiuj
```

------------------------------------------------------------------------

### 8. Synchronizacja Hybrydowa i Obiekty Jądra Systemu (Kernel Mode)

**Cel:** Koordynacja pracy między różnymi procesami w systemie operacyjnym (`Mutex`, `Semaphore`), asynchroniczny *Throttling* (`SemaphoreSlim`) oraz sygnalizacja zdarzeniowa wątków (`AutoResetEvent`, `ManualResetEvent`).

Obiekty dziedziczące po klasie `WaitHandle` (`Mutex`, `Semaphore`, `EventWaitHandle`) są zarządzane bezpośrednio przez **jądro systemu operacyjnego (OS Kernel)**. Choć są wolniejsze od blokad w trybie użytkownika, posiadają dwie unikalne cechy:

1.  **Synchronizacja międzyprocesowa (IPC):** Mogą posiadać globalną nazwę systemową (np. `@"Global\NazwaBlokady"`), widoczną dla zupełnie różnych programów `.exe` uruchomionych na tym samym komputerze.
2.  **Oczekiwanie zbiorcze:** Pozwalają uśpić wątek do czasu zasygnalizowania wszystkich (`WaitHandle.WaitAll`) lub dowolnego z zestawu uchwytów (`WaitHandle.WaitAny`).

#### 1. Klasa `Mutex` (*Mutual Exclusion*) – Wyłączność międzyprocesowa

Działa analogicznie do `lock` (wpuszcza tylko 1 wątek naraz), ale działa na poziomie całego systemu operacyjnego. Posiada restrykcyjne pojęcie **własności wątku (Thread Ownership)** – tylko ten sam wątek, który wywołał `WaitOne()`, ma prawo wywołać `ReleaseMutex()`.

- Jeśli proces trzymający `Mutex` ulegnie awarii (zostanie zabity w Menedżerze Zadań bez wywołania `ReleaseMutex()`), jądro systemu operacyjnego wykryje porzucenie blokady i obudzi kolejny czekający proces, rzucając w nim specjalny wyjątek **`AbandonedMutexException`**!

**Klasyczne zastosowanie: Blokada przed uruchomieniem drugiej instancji aplikacji (*Single-Instance Application*)**

``` csharp
using System;
using System.Threading;

// Prefiks "Global\" oznacza widoczność we wszystkich sesjach użytkowników (Terminal Services / RDP)
using var appMutex = new Mutex(initiallyOwned: false, name: @"Global\UniversityCleanArch_SingleInstance");

bool hasHandle = false;
try
{
    try
    {
        // Sprawdzamy natychmiastowo (timeout = 0ms), czy inna instancja trzyma już Mutex:
        hasHandle = appMutex.WaitOne(TimeSpan.Zero, exitContext: false);
    }
    catch (AbandonedMutexException)
    {
        // Poprzednia instancja aplikacji zamknęła się awaryjnie - przejmujemy Mutex!
        hasHandle = true;
    }

    if (!hasHandle)
    {
        Console.WriteLine("BŁĄD: Aplikacja jest już uruchomiona w innym oknie!");
        return;
    }

    Console.WriteLine("Aplikacja uruchomiona pomyślnie. Naciśnij Enter, aby zamknąć...");
    Console.ReadLine();
}
finally
{
    if (hasHandle)
    {
        appMutex.ReleaseMutex();
    }
}Kopiuj
```

------------------------------------------------------------------------

#### 2. `Semaphore` vs `SemaphoreSlim` – Limitowanie puli dostępów i Asynchroniczny Throttling

Podczas gdy `lock` i `Mutex` to kabina jednoosobowa (pojemność = 1), **Semafor** to parking z licznikiem wolnych miejsc – pozwala przebywać wewnątrz sekcji krytycznej maksymalnie **<span class="math inline">\\N\\</span> wątkom/zadaniom jednocześnie**.

| Cecha                          | `Semaphore` (Obiekt Jądra)                       | `SemaphoreSlim` (Lekki Hybrydowy)              |
|:-------------------------------|:-------------------------------------------------|:-----------------------------------------------|
| **Architektura**               | Czysty obiekt jądra OS (*Kernel Mode*).          | Hybrydowy (*User-Mode SpinWait* + *Kernel*).   |
| **Zasięg działania**           | **Międzyprocesowy** (obsługuje nazwy `Global\`). | **Wewnątrzprocesowy** (tylko jedna aplikacja). |
| **Wydajność**                  | Wolniejszy (każde wywołanie to *System Call*).   | **Nawet 5–10x szybszy** wewnątrz procesu.      |
| **Wsparcie dla `async/await`** | **Brak!** (Tylko blokujące `WaitOne()`).         | **Pełne wsparcie (`await WaitAsync(token)`)!** |

##### Przykład A: `Semaphore` – Ograniczenie liczby uruchomionych instancji programu w systemie do 2 (np. limit licencji)

``` csharp
using var licenseSemaphore = new Semaphore(initialCount: 2, maximumCount: 2, name: @"Global\MojProgram_Limit2");

if (!licenseSemaphore.WaitOne(TimeSpan.FromSeconds(1)))
{
    Console.WriteLine("Wyczerpano limit 2 jednoczesnych licencji programu w systemie!");
    return;
}

try
{
    Console.WriteLine("Pobrano slot licencyjny. Praca programu...");
    Console.ReadLine();
}
finally
{
    licenseSemaphore.Release(); // Zwrócenie biletu do puli systemowej
}Kopiuj
```

##### Przykład B: `SemaphoreSlim` – Asynchroniczna sekcja krytyczna i dławienie zapytań (*Throttling*)

Ponieważ instrukcja `lock` nie współpracuje ze słowem `await`, `SemaphoreSlim` jest **podstawowym narzędziem synchronizacji w kodzie `async/await`** (dla `new SemaphoreSlim(1, 1)` działa jak asynchroniczny `lock`, a dla `new SemaphoreSlim(N, N)` ogranicza liczbę jednoczesnych połączeń sieciowych):

``` csharp
// Maksymalnie 3 równoległe zapytania HTTP jednocześnie (ochrona przed zablokowaniem przez serwer API):
private static readonly SemaphoreSlim _apiThrottler = new(initialCount: 3, maxCount: 3);

public async Task<string> DownloadWithThrottlingAsync(string url, CancellationToken token)
{
    // Czekamy ASYNCHRONICZNIE na wolny slot bez blokowania wątku!
    await _apiThrottler.WaitAsync(token);
    try
    {
        return await _httpClient.GetStringAsync(url, token);
    }
    finally
    {
        // ZAWSZE zwalniamy slot w bloku finally!
        _apiThrottler.Release();
    }
}Kopiuj
```

------------------------------------------------------------------------

#### 3. Sygnalizacja między wątkami: `AutoResetEvent` vs `ManualResetEvent` (`ManualResetEventSlim`)

Klasy te działają jak bramki wjazdowe sterowane sygnałem z innego wątku (`WaitOne()` – czekaj przed bramką, `Set()` – otwórz bramkę, `Reset()` – zamknij bramkę):

- **`AutoResetEvent` (Bramka obrotowa / Kołowrotek w metrze):**  
  Wywołanie `_autoEvent.Set()` przepuszcza **dokładnie JEDEN czekający wątek** i natychmiast **automatycznie zamyka bramkę** z powrotem. Jeśli na bramce czekało 10 wątków, 1 ruszy do pracy, a pozostałe 9 nadal czeka na kolejne sygnały `Set()`.
- **`ManualResetEvent` / `ManualResetEventSlim` (Brama startowa maratonu):**  
  Wywołanie `_manualEvent.Set()` otwiera bramę na oścież – **WSZYSTKIE czekające wątki ruszają jednocześnie!** Brama pozostaje otwarta aż do ręcznego wywołania metody `_manualEvent.Reset()`.

``` csharp
// Przykład: ManualResetEventSlim jako brama startowa dla grupy wątków roboczych
using var startGate = new ManualResetEventSlim(initialState: false);

for (int i = 1; i <= 3; i++)
{
    int workerId = i;
    Task.Run(() =>
    {
        Console.WriteLine($"Pracownik #{workerId} gotowy na linii startu...");
        startGate.Wait(); // Wszystkie wątki zatrzymują się tutaj!
        Console.WriteLine($"Pracownik #{workerId} wystartował jednocześnie z innymi!");
    });
}

await Task.Delay(1000);
Console.WriteLine("SYGNAŁ START!");
startGate.Set(); // Otwiera bramę dla wszystkich wątków naraz!Kopiuj
```

------------------------------------------------------------------------

### 9. Zaawansowana Koordynacja Zespołów Zadań: `CountdownEvent` oraz `Barrier`

**Cel:** Synchronizacja wieloetapowych obliczeń grupowych w architekturach typu *Fork-Join* oraz algorytmach fazowych.

#### 1. Klasa `CountdownEvent` – Wzorzec „Czekaj na zakończenie wszystkich” (*Fork-Join*)

`CountdownEvent` działa odwrotnie do semafora: wątek nadzorujący czeka na metodzie `Wait()`, dopóki zadana liczba <span class="math inline">\\N\\</span> wątków roboczych nie zgłosi wykonania swojej części pracy za pomocą metody **`Signal()`** (zmniejszającej wewnętrzny licznik aż do `0`):

``` csharp
using var countdown = new CountdownEvent(initialCount: 3);

for (int i = 1; i <= 3; i++)
{
    int moduleId = i;
    ThreadPool.QueueUserWorkItem(_ =>
    {
        Thread.Sleep(Random.Shared.Next(300, 1000));
        Console.WriteLine($"Moduł #{moduleId} zakończył inicjalizację.");
        countdown.Signal(); // Dekrementacja licznika o 1
    });
}

countdown.Wait(); // Czeka, aż licznik spadnie z 3 do 0
Console.WriteLine("Wszystkie moduły gotowe – uruchamianie systemu głównego!");Kopiuj
```

#### 2. Klasa `Barrier` – Cykliczna synchronizacja wielofazowa (*Phased Computation*)

W symulacjach fizycznych, przetwarzaniu grafów czy algorytmach macierzowych grupa <span class="math inline">\\N\\</span> wątków pracuje wspólnie w kilku kolejnych **fazach (etapach)**. Żaden wątek nie może rozpocząć Fazy <span class="math inline">\\k+1\\</span>, dopóki **wszyscy uczestnicy** nie zakończą Fazy <span class="math inline">\\k\\</span>.

- Gdy wątek kończy swoją część bieżącej fazy, wywołuje **`barrier.SignalAndWait()`** – melduje wykonanie zadania i zasypia, czekając na pozostałych kolegów z grupy.
- Gdy ostatni wątek dotrze do bariery, wywoływany jest opcjonalny delegat **`Post-Phase Action`** (wykonywany pojedynczo przed puszczeniem grupy dalej, np. w celu zagregowania wyników etapu lub wypisania postępu), po czym bariera automatycznie otwiera się do kolejnej fazy!

``` csharp
// Bariera dla 3 równoległych wątków z akcją podsumowującą na końcu każdej fazy:
using var barrier = new Barrier(participantCount: 3, postPhaseAction: b =>
{
    Console.WriteLine($"=== [KONIEC FAZY {b.CurrentPhaseNumber}] Wszyscy uczestnicy zsynchronizowani ===\n");
});

void RunSimulationWorker(int workerId)
{
    // FAZA 0: Pobranie i przygotowanie wektorów
    Thread.Sleep(Random.Shared.Next(300, 1200));
    Console.WriteLine($"Wątek #{workerId}: Zakończył Fazę 0 (Przygotowanie danych) – czeka na barierze.");
    barrier.SignalAndWait();

    // FAZA 1: Obliczenia na zagregowanych danych z Fazy 0
    Thread.Sleep(Random.Shared.Next(300, 1200));
    Console.WriteLine($"Wątek #{workerId}: Zakończył Fazę 1 (Obliczenia główne) – czeka na barierze.");
    barrier.SignalAndWait();
}

Parallel.Invoke(
    () => RunSimulationWorker(1),
    () => RunSimulationWorker(2),
    () => RunSimulationWorker(3)
);Kopiuj
```

------------------------------------------------------------------------

### 10. Podsumowanie: Matryca Decyzyjna Wyboru Mechanizmu Synchronizacji

| Problem inżynierski w aplikacji                                                                 | Rekomendowany mechanizm w .NET (.NET 8/9/10)            | Tryb pracy               |
|:------------------------------------------------------------------------------------------------|:--------------------------------------------------------|:-------------------------|
| **Zwiększenie prostego licznika lub flagi z wielu wątków**                                      | `Interlocked.Increment` / `Interlocked.CompareExchange` | User Mode (CAS)          |
| **Współdzielony słownik / pamięć podręczna klucz-wartość**                                      | `ConcurrentDictionary<TKey, TValue>`                    | User Mode / Fine-Grained |
| **Krótka sekcja krytyczna na polach w kodzie synchronicznym**                                   | `lock (_syncObj)` (`System.Threading.Lock` / `Monitor`) | User Mode / Hybrid       |
| **Sekcja krytyczna wewnątrz metody `async/await`**                                              | `SemaphoreSlim(1, 1)` + `await WaitAsync(token)`        | Hybrid (Async)           |
| **Limitowanie jednoczesnych zapytań sieciowych (*Throttling*)**                                 | `SemaphoreSlim(N, N)` + `await WaitAsync(token)`        | Hybrid (Async)           |
| **Struktura czytana przez setki wątków, rzadko modyfikowana**                                   | `ReaderWriterLockSlim`                                  | Hybrid                   |
| **Bezpieczna wątkowo jednorazowa inicjalizacja ciężkiego obiektu**                              | `Lazy<T>`                                               | User Mode                |
| **Blokada uruchomienia drugiej kopii programu `.exe` w Windows**                                | Nazwany `Mutex` (`@"Global\NazwaAplikacji"`)            | Kernel Mode (IPC)        |
| **Asynchroniczna pętla wykonywana cyklicznie co <span class="math inline">\\X\\</span> sekund** | `PeriodicTimer` + `await WaitForNextTickAsync(token)`   | Async (Pull)             |
| **Wieloetapowy algorytm równoległy (synchronizacja co krok)**                                   | `Barrier` + `SignalAndWait()`                           | Hybrid                   |
