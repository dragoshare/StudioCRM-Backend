# Wypłaty trenerów na umowie zlecenie w statystykach kosztów (2026-11-01)

`GET /api/expenses/statistics` wlicza do kosztów **wypłacone** miesięczne rozliczenia trenerów, którzy w danym miesiącu mają w CRM wyłącznie aktywne umowy typu `Zlecenie`. Kontrakt odpowiedzi się nie zmienia.

- Koszt powstaje dopiero po „Oznacz jako wypłacone” (`POST /api/trainers/{id}/settlement/mark-as-paid`); „Otwórz ponownie” go usuwa.
- Kwota: `TotalAmount` zamkniętego rozliczenia, netto = brutto, VAT 0. Nie zawiera składek po stronie studia.
- Data kosztu: ostatni dzień rozliczanego miesiąca (wypłata za październik wpada w październik, także gdy wypłacono ją w listopadzie). Filtry `paidFrom`/`paidTo` działają po dacie wypłaty.
- Wpływa na `grossAmount`, `netAmount`, `paidGrossAmount`, `operatingProfit*` i rozbicia: kategoria `TrainerCosts`, status `Paid`, miesiąc pracy, podmiot „Wynagrodzenia trenerów (umowy zlecenie)” (`key` = `0`), lokalizacja „Bez lokalizacji”.
- Pomijane, gdy filtr wskazuje podmiot prawny, lokalizację, termin płatności, cykliczność, wyszukiwanie, zaległe, inną kategorię niż `TrainerCosts` albo inny status niż `Paid`.
- Trenerzy B2B (oraz trener mający w danym miesiącu choć jedną umowę B2B) nie są wliczani — ich faktury wpisuje się jako dokumenty kosztowe.
- Trener bez umowy w CRM nie jest wliczany. Lista dokumentów (`GET /api/expenses`) nie pokazuje tych pozycji.

Nie wpisuj ręcznie dokumentu kosztowego za wypłatę zleceniobiorcy — policzyłaby się podwójnie.
