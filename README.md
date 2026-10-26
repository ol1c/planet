# planet
Programowanie lokalnych aplikacji .NET

**Planet** to symulator ekosystemu w WPF (C#, .NET 10) oparty na automacie komórkowym. Na kwadratowej planszy żyją
rośliny i zwierzęta. Wszystkie pola aktualizują się w każdym kroku jednocześnie, jak w grze Life Conwaya, a do tego
zwierzęta mogą się poruszać. Treść zadania jest w pliku [INSTRUKCJA.txt](INSTRUKCJA.txt).

## Uruchomienie

Wymagany jest .NET SDK 10 i system Windows (WPF).

```bash
dotnet run --project src/Planet.App
```

Testy jednostkowe logiki symulacji:

```bash
dotnet test
```

Można też otworzyć `Planet.sln` w Visual Studio 2022 lub w Riderze.

## Struktura projektu

```
planet/
├── Planet.sln                         rozwiązanie (3 projekty)
├── INSTRUKCJA.txt                     treść zadania
├── src/
│   ├── Planet.Core/                   logika bez zależności od WPF (net10.0)
│   │   ├── Model/
│   │   │   ├── Cell.cs                pole: roślina, zwierzę (wiek, id)
│   │   │   ├── CountSet.cs            zbiór liczb sąsiadów 0-8 (R, W, B, P, Z) jako maska bitowa
│   │   │   ├── RuleSet.cs             parametry R, W, B, P, Z, M, L; zapis „R…/W…/B…/P…/Z…/M…/L…”
│   │   │   └── World.cs               plansza 50-1000, sąsiedztwo Moore’a, torus, wyszukiwanie zwierzęcia po id
│   │   ├── Simulation/
│   │   │   ├── Simulator.cs           krok automatu (równoległe liczenie sąsiadów, bufory wielokrotnego użytku)
│   │   │   ├── StepStatistics.cs      zdarzenia z kroku (z listą narodzin) i sumy
│   │   │   └── WorldGenerator.cs      losowanie stanu
│   │   └── IO/
│   │       ├── WorldFile.cs           zapis i odczyt pliku tekstowego (plansza i reguły)
│   │       └── WorldRasterizer.cs     plansza lub wycinek → obraz indeksowany (kolory nadaje widok)
│   └── Planet.App/                    aplikacja WPF w architekturze MVVM (net10.0-windows)
│       ├── App.xaml                   scala słowniki zasobów z Themes/
│       ├── MainWindow.xaml(.cs)       układ okna; code-behind obsługuje tylko kółko myszy i przesuwanie
│       ├── Themes/
│       │   ├── Colors.xaml            paleta kolorów (wspólna dla widoku, PNG i miniatur)
│       │   ├── Controls.xaml          proste style kontrolek (marginesy), Start↔Stop, szablon błędu walidacji, wskaźnik pracy
│       │   └── Board.xaml             kształty roślin i zwierząt, szablon pola, styl widoku bitmapowego
│       ├── Controls/
│       │   └── WorldBitmapView.cs     kontrolka rysująca planszę lub jej fragment jako bitmapę (widok i minimapa)
│       ├── ViewModels/
│       │   ├── MainViewModel.cs           plansza, symulacja, edycja, zapis i odczyt
│       │   ├── MainViewModel.View.cs      płynny zoom, przesuwanie, próg widoku szczegółowego
│       │   ├── MainViewModel.Export.cs    zapis obrazu PNG (cała plansza lub widoczny fragment)
│       │   ├── MainViewModel.Births.cs    lista narodzin, przejście do zwierzęcia, zdjęcia, śledzenie
│       │   ├── BirthRecordViewModel.cs    wpis na liście narodzin z wycinkiem otoczenia
│       │   ├── CellViewModel.cs           pole widoku szczegółowego
│       │   ├── RulesViewModel.cs          edycja reguł z walidacją (INotifyDataErrorInfo)
│       │   ├── StatisticsViewModel.cs
│       │   ├── ObservableObject.cs, RelayCommand.cs
│       ├── Models/Enums.cs            EditTool, CellPaintRequest
│       ├── Converters/                EnumToBoolean, Percent
│       └── Services/
│           ├── IDialogService.cs, DialogService.cs   okna plików i komunikaty
│           └── BoardImages.cs         paleta z zasobów, tworzenie i zapis PNG
└── tests/
    └── Planet.Core.Tests/             testy xUnit (39): reguły, symulator, zwierzęta, plik, obrazy
```

## Reguły symulacji

| Parametr | Znaczenie | Domyślnie |
|---|---|---|
| **R** | liczby sąsiednich roślin, przy których na pustym polu powstaje roślina | `1234` |
| **W** | liczby sąsiednich roślin, przy których roślina przeżywa | `123456` |
| **B** | liczby sąsiednich zwierząt, przy których rodzi się zwierzę | `23` |
| **P** | liczby sąsiednich roślin wymagane do narodzin zwierzęcia | `01` |
| **Z** | liczby sąsiednich **zwierząt**, przy których zwierzę przeżywa | `0123` |
| **M** | zasięg skoku zwierzęcia (1-20) | `2` |
| **L** | długość życia zwierzęcia w krokach | `10` |

### Kolejność działań w kroku (`Simulator.Step`)

Wszystkie decyzje zapadają na podstawie stanu z początku kroku.

1. **Rośliny.** Na polu bez rośliny i bez zwierzęcia powstaje roślina, jeśli liczba sąsiednich roślin należy do R.
   Roślina przeżywa, jeśli liczba sąsiadów należy do W. Roślina, na której stoi zwierzę, zostaje zjedzona.
2. **Narodziny zwierząt.** Na polu bez zwierzęcia rodzi się zwierzę (wiek 0, nowy numer `#id`), jeśli liczba sąsiednich
   zwierząt należy do B, a liczba sąsiednich roślin do P.
3. **Zwierzęta.** Są przetwarzane w losowej kolejności:
   - po L krokach zwierzę umiera ze starości;
   - umiera z tłoku, jeśli liczba sąsiednich **zwierząt** nie należy do Z. Rośliny nie mają tu znaczenia: zwierzę
     otoczone wyłącznie roślinami ma 0 sąsiadów, a 0 ∈ Z (sprawdza to test `Animal_SurroundedOnlyByPlants_DoesNotDieOfCrowding`);
   - stojąc na roślinie, zjada ją i zostaje na miejscu;
   - głodne (niestojące na roślinie) idzie na sąsiednią roślinę, potem skacze na roślinę do M pól dalej, a w ostateczności rusza się losowo.
     Nie może wejść na pole zajęte przez inne zwierzę.

> Przy domyślnych regułach z instrukcji rośliny szybko pokrywają planszę, a warunek P = {0, 1} rzadko jest wtedy
> spełniony. Dlatego zwierzęta zwykle wymierają po kilkudziesięciu krokach ze starości (L = 10), a nie z tłoku.
> Warto poeksperymentować z szerszym zbiorem `P` albo z mniejszą gęstością roślin.

## Obsługa aplikacji

Panel boczny ma stałą minimapę i zakładki **Symulacja**, **Reguły**, **Statystyki**, **Narodziny**, **Eksport**.

| Funkcja | Jak |
|---|---|
| Rozmiar planszy | *Symulacja → Plansza*: bok od 50 do 1000 |
| Duże plansze | suwak **Kroków na klatkę** (1-50) liczy kilka kroków między odświeżeniami widoku; krok 1000×1000 trwa ok. 60 ms (kompilacja Release) |
| Edycja | narzędzie **Roślina / Zwierzę / Gumka**; LPM stawia, PPM usuwa; w widoku bitmapowym można malować przeciągając |
| Krok / animacja | **Krok** (F6), **Start/Stop** (F5), suwak prędkości; po zatrzymaniu wraca tryb edycji |
| Płynny zoom | suwak nad planszą (bok fragmentu od 10 pól do całej planszy), **kółko myszy** (punkt pod kursorem zostaje w miejscu), `Ctrl +`/`Ctrl −`, **Cała plansza** (Ctrl+1) |
| Zmiana wyglądu przy zoomie | powyżej **40×40** pól widok jest bitmapą (siatka pojawia się od ok. 6 px na pole); od 40×40 w dół pola rysuje DataTemplate z kształtami liści i zwierząt oraz animacjami |
| Przesuwanie | paski przewijania, **środkowy przycisk myszy** (przeciąganie), kliknięcie w minimapę |
| Zapis i odczyt | Ctrl+S / Ctrl+O; plik zawiera planszę i reguły |
| Obraz | *Eksport → Zapisz obraz PNG* (Ctrl+E): cała plansza albo tylko widoczny fragment |
| Narodziny | *Narodziny*: lista ostatnich 200 urodzonych zwierząt z miniaturą otoczenia 15×15 z chwili narodzin |
| Przejście do zwierzęcia | kliknięcie wpisu: zoom do 21×21 i wyśrodkowanie na zwierzęciu (lub na miejscu narodzin, jeśli już nie żyje); zwierzę dostaje niebieską ramkę, a na minimapie kółko. Po kolejnym kroku zaznaczenie znika (jak po **Nie śledź**), chyba że zaznaczone jest **Śledź nowe narodziny** |
| Kolejne egzemplarze | **Nowsze / Starsze**; **Śledź nowe narodziny**: podczas animacji widok sam przechodzi do najnowszego zwierzęcia |
| Fotografia | **Zapisz zdjęcie**: otoczenie z chwili narodzin jako PNG (24 px na pole) |
| Statystyki | rośliny, zwierzęta, narodziny, śmierci (ze starości / z tłoku), nowe rośliny, obumarłe rośliny i zjedzone rośliny (osobne wiersze); z ostatniego kroku i łącznie |

### Format pliku

```
# Planet - zapis ekosystemu
RULES R1234/W123456/B23/P01/Z0123/M2/L10
SIZE 60
WRAP 1
GENERATION 0
GRID
. P A3 PA0 A5 . P ...        ← SIZE wierszy po SIZE pól
```

Pola: `.` puste, `P` roślina, `A<wiek>` zwierzę, `PA<wiek>` zwierzę na roślinie. Identyfikatory zwierząt nie są
zapisywane, przy odczycie dostają nowe numery. Przy błędzie odczytu aplikacja podaje numer linii.

## Wykorzystane mechanizmy WPF

| Mechanizm | Gdzie |
|---|---|
| **MVVM, binding, komendy** | `MainViewModel` (klasa częściowa w 4 plikach) i `RelayCommand`; okno jest wiązane do view-modelu |
| **Zasoby** | `Themes/Colors.xaml`: kolory i pędzle (również dla PNG i miniatur); `Themes/Board.xaml`: kształty i rozmiar pola |
| **Style** | style niejawne (marginesy przycisków, pól, suwaków), `RunButtonStyle` (BasedOn), sekcje, etykiety; styl `WorldBitmapView` przekazuje paletę do bitmapy |
| **ControlTemplate** | pole planszy (`CellButtonStyle`), szablon błędu walidacji; pozostałe kontrolki (przyciski, zakładki, RadioButton) mają standardowy wygląd WPF |
| **DataTemplate** | `CellTemplate` (pole w widoku szczegółowym), `BirthTemplate` (wpis listy narodzin z miniaturą) |
| **Triggery** | `Trigger` (Validation.HasError), `MultiTrigger` (IsMouseOver + IsEnabled na polu planszy), `DataTrigger` (zawartość pola, śledzone zwierzę, Start↔Stop, poziom szczegółowości przy zoomie), `MultiDataTrigger` |
| **Animacje (Storyboard)** | pojawienie się nowego zwierzęcia (ScaleTransform + BackEase), wyłanianie rośliny i pulsujący wskaźnik pracy |
| **Walidacja** | `INotifyDataErrorInfo` w `RulesViewModel` + `Validation.ErrorTemplate` |
| **Własna kontrolka** | `WorldBitmapView`: właściwości zależnościowe, `OnRender`, `WriteableBitmap`, fragment planszy, siatka, znacznik, mysz → `ICommand` |
| **InputBindings** | skróty klawiszowe, PPM na polu (`MouseBinding`) |

### Jak zmienić wygląd

- Kolory: `src/Planet.App/Themes/Colors.xaml`. Zmiana obejmuje cały interfejs i wszystkie eksportowane obrazy.
- Kształty roślin i zwierząt: `PlantGeometry` i `AnimalGeometry` w `Themes/Board.xaml`.
- Próg zmiany wyglądu przy zoomie: `MainViewModel.DetailThreshold` (domyślnie 40).
