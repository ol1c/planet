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
│   │   │   ├── Cell.cs                pole: roślina, zwierzę (wiek, id, głód, sytość)
│   │   │   ├── CountSet.cs            zbiór liczb sąsiadów 0–8 (R, W, B, P, Z) jako maska bitowa
│   │   │   ├── RuleSet.cs             parametry R…L oraz S, D, H; zapis „R…/W…/…/L…[/S…][/D…][/H…]”
│   │   │   └── World.cs               plansza 50–1000, sąsiedztwo Moore’a, torus, wyszukiwanie zwierzęcia po id
│   │   ├── Simulation/
│   │   │   ├── Simulator.cs           krok automatu (równoległe liczenie sąsiadów, bufory wielokrotnego użytku)
│   │   │   ├── StepStatistics.cs      zdarzenia z kroku (z listą narodzin) i sumy
│   │   │   └── WorldGenerator.cs      losowanie stanu
│   │   └── IO/
│   │       ├── WorldFile.cs           zapis i odczyt pliku tekstowego (plansza, reguły, historia zwierząt)
│   │       ├── WorldRasterizer.cs     plansza lub wycinek → obraz indeksowany (kolory nadaje widok)
│   │       └── GifWriter.cs           własny koder animowanego GIF-a (LZW), bez zewnętrznych bibliotek
│   └── Planet.App/                    aplikacja WPF w architekturze MVVM (net10.0-windows)
│       ├── App.xaml                   scala słowniki zasobów z Themes/
│       ├── MainWindow.xaml(.cs)       układ okna; code-behind obsługuje tylko kółko myszy i przesuwanie
│       ├── Themes/
│       │   ├── Colors.xaml            paleta kolorów (wspólna dla widoku, PNG, GIF i miniatur)
│       │   ├── Controls.xaml          style i szablony przycisków, zakładek, pól tekstowych, wskaźników
│       │   └── Board.xaml             kształty roślin i zwierząt, szablon pola, styl widoku bitmapowego
│       ├── Controls/
│       │   └── WorldBitmapView.cs     kontrolka rysująca planszę lub jej fragment jako bitmapę (widok i minimapa)
│       ├── ViewModels/
│       │   ├── MainViewModel.cs           plansza, symulacja, edycja, zapis i odczyt
│       │   ├── MainViewModel.View.cs      płynny zoom, przesuwanie, próg widoku szczegółowego
│       │   ├── MainViewModel.Export.cs    obraz PNG, nagrywanie GIF i sekwencji PNG
│       │   ├── MainViewModel.Births.cs    lista narodzin, zdjęcia, goto, śledzenie
│       │   ├── BirthRecordViewModel.cs    wpis na liście narodzin z wycinkiem otoczenia
│       │   ├── CellViewModel.cs           pole widoku szczegółowego
│       │   ├── RulesViewModel.cs          edycja reguł z walidacją (INotifyDataErrorInfo)
│       │   ├── StatisticsViewModel.cs
│       │   ├── ObservableObject.cs, RelayCommand.cs
│       ├── Models/Enums.cs            EditTool, RecordFormat, CellPaintRequest
│       ├── Converters/                EnumToBoolean, Percent, InverseBoolean
│       └── Services/
│           ├── IDialogService.cs, DialogService.cs   okna plików, folderu i komunikaty
│           ├── BoardImages.cs         paleta z zasobów, tworzenie i zapis PNG
│           └── FrameRecorder.cs       nagrywanie klatek: GIF albo sekwencja PNG
└── tests/
    └── Planet.Core.Tests/             testy xUnit (50): reguły, symulator, historia, plik, GIF/LZW, obrazy
```

## Reguły symulacji

| Parametr | Znaczenie | Domyślnie |
|---|---|---|
| **R** | liczby sąsiednich roślin, przy których na pustym polu powstaje roślina | `1234` |
| **W** | liczby sąsiednich roślin, przy których roślina przeżywa | `123456` |
| **B** | liczby sąsiednich (dojrzałych) zwierząt, przy których rodzi się zwierzę | `23` |
| **P** | liczby sąsiednich roślin wymagane do narodzin zwierzęcia | `01` |
| **Z** | liczby sąsiednich **zwierząt**, przy których zwierzę przeżywa | `0123` |
| **M** | zasięg skoku zwierzęcia (1–20) | `2` |
| **L** | długość życia zwierzęcia w krokach | `10` |

### Historia zwierząt (parametry dodatkowe, 0 = wyłączone)

Każde zwierzę pamięta swój wiek, licznik głodu (kroki od ostatniego posiłku) i licznik sytości.

| Parametr | Znaczenie | Przykład |
|---|---|---|
| **S** | przez tyle kroków po zjedzeniu rośliny zwierzę szuka **innych zwierząt** (podchodzi do najbliższego w zasięgu M), a potem znów szuka jedzenia | `S2` |
| **D** | wiek, od którego zwierzę może zostać rodzicem: młodsze nie liczą się do B, czyli nowo narodzone nie rozmnaża się przez D kroków | `D3` |
| **H** | po tylu krokach bez jedzenia zwierzę umiera z głodu | `H8` |

Zapis reguł z historią: `R1234/W123456/B23/P01/Z0123/M2/L10/S2/D3/H8`. Bez S, D i H zachowanie jest takie jak w
treści zadania.

### Kolejność działań w kroku (`Simulator.Step`)

Wszystkie decyzje zapadają na podstawie stanu z początku kroku.

1. **Rośliny.** Na polu bez rośliny i bez zwierzęcia powstaje roślina, jeśli liczba sąsiednich roślin należy do R.
   Roślina przeżywa, jeśli liczba sąsiadów należy do W. Roślina, na której stoi zwierzę, zostaje zjedzona.
2. **Narodziny zwierząt.** Na polu bez zwierzęcia rodzi się zwierzę (wiek 0, nowy numer `#id`), jeśli liczba sąsiednich
   zwierząt w wieku ≥ D należy do B, a liczba sąsiednich roślin do P.
3. **Zwierzęta.** Są przetwarzane w losowej kolejności:
   - po L krokach zwierzę umiera ze starości;
   - umiera z tłoku, jeśli liczba sąsiednich **zwierząt** nie należy do Z. Rośliny nie mają tu znaczenia: zwierzę
     otoczone wyłącznie roślinami ma 0 sąsiadów, a 0 ∈ Z (sprawdza to test `Animal_SurroundedOnlyByPlants_DoesNotDieOfCrowding`);
   - umiera z głodu, gdy H > 0 i od H kroków nic nie jadło;
   - stojąc na roślinie, zjada ją (głód = 0, sytość = S) i zostaje na miejscu;
   - najedzone (sytość > 0) podchodzi do najbliższego zwierzęcia widocznego w jednym z 8 kierunków (do M pól);
   - głodne idzie na sąsiednią roślinę, potem skacze na roślinę do M pól dalej, a w ostateczności rusza się losowo.
     Nie może wejść na pole zajęte przez inne zwierzę.

> Przy domyślnych regułach z instrukcji rośliny szybko pokrywają planszę, a warunek P = {0, 1} rzadko jest wtedy
> spełniony. Dlatego zwierzęta zwykle wymierają po kilkudziesięciu krokach ze starości (L = 10), a nie z tłoku.
> Warto poeksperymentować z szerszym zbiorem `P` albo z mniejszą gęstością roślin.

## Obsługa aplikacji

Panel boczny ma stałą minimapę i zakładki **Symulacja**, **Reguły**, **Statystyki**, **Narodziny**, **Eksport**.

| Funkcja | Jak |
|---|---|
| Rozmiar planszy | *Symulacja → Plansza*: bok 50–300; po zaznaczeniu **trybu wielkich ekosystemów** do 1000×1000 |
| Duże plansze | suwak **Kroków na klatkę** (1–50) liczy kilka kroków między odświeżeniami widoku; krok 1000×1000 trwa ok. 60 ms (kompilacja Release) |
| Edycja | narzędzie **Roślina / Zwierzę / Gumka**; LPM stawia, PPM usuwa; w widoku bitmapowym można malować przeciągając |
| Krok / animacja | **Krok** (F6), **Start/Stop** (F5), suwak prędkości; po zatrzymaniu wraca tryb edycji |
| Płynny zoom | suwak nad planszą (bok fragmentu od 10 pól do całej planszy), **kółko myszy** (punkt pod kursorem zostaje w miejscu), `Ctrl +`/`Ctrl −`, **Cała plansza** (Ctrl+1) |
| Zmiana wyglądu przy zoomie | powyżej **40×40** pól widok jest bitmapą (siatka pojawia się od ok. 6 px na pole); od 40×40 w dół pola rysuje DataTemplate z kształtami liści i zwierząt oraz animacjami |
| Przesuwanie | paski przewijania, **środkowy przycisk myszy** (przeciąganie), kliknięcie w minimapę |
| Zapis i odczyt | Ctrl+S / Ctrl+O; plik zawiera planszę, reguły i historię zwierząt |
| Obraz | *Eksport → Zapisz obraz PNG* (Ctrl+E): cała plansza albo tylko widoczny fragment |
| Film / sekwencja | *Eksport → Nagrywaj* (Ctrl+R): **GIF** (film) albo **sekwencja PNG** (`klatka_00001.png`…); jedna klatka na odświeżenie |
| Narodziny | *Narodziny*: lista ostatnich 200 urodzonych zwierząt z miniaturą otoczenia 15×15 z chwili narodzin |
| Przejście do zwierzęcia | kliknięcie wpisu: zoom do 21×21 i wyśrodkowanie na zwierzęciu (lub na miejscu narodzin, jeśli już nie żyje); zwierzę dostaje niebieską ramkę, a na minimapie kółko |
| Kolejne egzemplarze | **Nowsze / Starsze**; **Śledź nowe narodziny**: podczas animacji widok sam przechodzi do najnowszego zwierzęcia |
| Fotografia | **Zapisz zdjęcie**: otoczenie z chwili narodzin jako PNG (24 px na pole) |
| Goto | pole *Idź do*: `x,y` (pole) albo `#id` (żywe zwierzę), Enter |
| Statystyki | rośliny, zwierzęta, narodziny, śmierci (ze starości / z tłoku / z głodu), rośliny nowe, obumarłe i zjedzone; z ostatniego kroku i łącznie |

MP4 z sekwencji PNG można zrobić np. tak:

```bash
ffmpeg -framerate 10 -i klatka_%05d.png -pix_fmt yuv420p film.mp4
```

### Format pliku

```
# Planet – zapis ekosystemu
RULES R1234/W123456/B23/P01/Z0123/M2/L10/S2/D3/H8
SIZE 60
WRAP 1
GENERATION 0
GRID
. P A3 PA0 A5h2s1 . P ...        ← SIZE wierszy po SIZE pól
```

Pola: `.` puste, `P` roślina, `A<wiek>[h<głód>][s<sytość>]` zwierzę, `PA…` zwierzę na roślinie. Identyfikatory
zwierząt nie są zapisywane, przy odczycie dostają nowe numery. Pliki z poprzedniej wersji (bez S/D/H i bez `h`/`s`)
wczytują się bez zmian. Przy błędzie odczytu aplikacja podaje numer linii.

## Wykorzystane mechanizmy WPF

| Mechanizm | Gdzie |
|---|---|
| **MVVM, binding, komendy** | `MainViewModel` (klasa częściowa w 4 plikach) i `RelayCommand`; okno jest wiązane do view-modelu |
| **Zasoby** | `Themes/Colors.xaml`: kolory i pędzle (również dla PNG, GIF i miniatur); `Themes/Board.xaml`: kształty i rozmiar pola |
| **Style** | przyciski, przełączniki, zakładki, pola tekstowe, sekcje; styl `WorldBitmapView` przekazuje paletę do bitmapy |
| **ControlTemplate** | przyciski, RadioButton jako przełącznik, `TabItem`, pole planszy (`CellButtonStyle`), szablon błędu walidacji |
| **DataTemplate** | `CellTemplate` (pole w widoku szczegółowym), `BirthTemplate` (wpis listy narodzin z miniaturą) |
| **Triggery** | `Trigger` (IsMouseOver, IsPressed, IsSelected, Validation.HasError), `MultiTrigger`, `DataTrigger` (zawartość pola, śledzone zwierzę, Start↔Stop, nagrywanie, poziom szczegółowości przy zoomie), `MultiDataTrigger` |
| **Animacje (Storyboard)** | pojawienie się nowego zwierzęcia (ScaleTransform + BackEase), wyłanianie rośliny, pulsujący wskaźnik pracy i przycisk nagrywania |
| **Walidacja** | `INotifyDataErrorInfo` w `RulesViewModel` + `Validation.ErrorTemplate` |
| **Własna kontrolka** | `WorldBitmapView`: właściwości zależnościowe, `OnRender`, `WriteableBitmap`, fragment planszy, siatka, znacznik, mysz → `ICommand` |
| **InputBindings** | skróty klawiszowe, PPM na polu (`MouseBinding`), Enter w polu goto |

### Jak zmienić wygląd

- Kolory: `src/Planet.App/Themes/Colors.xaml`. Zmiana obejmuje cały interfejs i wszystkie eksportowane obrazy.
- Kształty roślin i zwierząt: `PlantGeometry` i `AnimalGeometry` w `Themes/Board.xaml`.
- Próg zmiany wyglądu przy zoomie: `MainViewModel.DetailThreshold` (domyślnie 40).
