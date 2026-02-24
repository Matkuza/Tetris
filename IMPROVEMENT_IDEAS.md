# Co można jeszcze zrobić w tym projekcie

Poniżej lista konkretnych kierunków rozwoju, które pasują do obecnej architektury gry.

## 1) Rozdzielenie logiki gry od UI (największy zwrot)
- Przenieść reguły gry (ruch, kolizje, czyszczenie linii, punktacja) do klasy `GameEngine` bez zależności od WPF.
- Dzięki temu łatwiej dopisać testy jednostkowe i uniknąć regresji przy dalszych zmianach.

## 2) 7-bag randomizer dla tetromino
- Zamiast losować kształty całkiem losowo, użyć algorytmu „7-bag” (tasowanie wszystkich 7 figur i pobieranie po kolei).
- Rozgrywka jest wtedy bardziej sprawiedliwa (mniej frustrujących serii bez „I”).

## 3) Ghost piece + hold piece
- Dodać „ghost piece” pokazujący miejsce lądowania.
- Dodać „hold piece” (przechowanie jednej figury), z limitem jednego użycia na turę.
- To standard w nowoczesnych wersjach Tetrisa i poprawia czytelność gry.

## 4) Usprawnienie sterowania i accessibility
- Konfigurowalne klawisze (zapis do `settings.json`).
- DAS/ARR (opóźnienie i szybkość auto-przesuwu przy przytrzymaniu).
- Lepsze wsparcie dla daltonizmu (alternatywne palety + oznaczenia wzorem/ikoną).

## 5) Więcej trybów rozgrywki
- Sprint (40 linii na czas).
- Ultra (2 minuty na wynik).
- Marathon z dłuższą progresją poziomów.
- Challenge dzienny z seedem i rankingiem dnia.

## 6) Lepsze statystyki i ranking
- Osobne rankingi per tryb.
- Statystyki sesji: APM, PPS, średni czas lock delay, liczba Tetrisów.
- Eksport wyników do CSV/JSON oraz prosty wykres progresu.

## 7) Testy automatyczne
- Testy jednostkowe na: rotację, kolizje, czyszczenie linii, game-over, naliczanie punktów.
- Testy regresyjne na zapisywanie/odczyt ustawień i high score.

## 8) Optymalizacja renderowania
- Ograniczyć pełne odrysowywanie planszy tylko do zmienionych pól (dirty rectangles) lub cache elementów.
- Przy większej liczbie efektów wizualnych poprawi to płynność.

## 9) UX i onboarding
- Krótki tutorial po pierwszym uruchomieniu.
- Podpowiedzi narzędziowe przy ustawieniach dźwięku, reklam i trybów.
- Ekran „co nowego” po aktualizacji.

## 10) Przygotowanie pod wydanie
- Dodać README (instalacja, sterowanie, konfiguracja, build).
- Dodać changelog i prosty workflow CI (build + testy + artefakt).
- Uporządkować strukturę zasobów (audio/grafiki) i wersjonowanie danych `AdAssets`.

---

## Priorytet na 1–2 sprinty
1. Rozdzielić silnik gry od UI.
2. Dodać testy jednostkowe dla silnika.
3. Dodać 7-bag + ghost piece.
4. Wprowadzić Sprint/Ultra i osobne rankingi.
