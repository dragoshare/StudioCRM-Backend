# Stawka trenera za zajęcia grupowe

Owner ustawia kwotę za pojedyncze odbyte zajęcia grupowe w ustawieniach stawek trenera.

`PUT /api/trainers/{id}/rates`

```json
{ "groupSessionRate": 120 }
```

Można przesłać również `hourlyRate`. Pominięcie pola lub null zachowuje jego dotychczasową wartość. Zero jest dozwolone, kwoty ujemne są odrzucane. Zapis tej samej wartości nie tworzy nowego okresu.

`GET /api/trainers/{id}/rates` zwraca aktywne stawki: `sessionType: "Hourly"` i `sessionType: "Group"`; `rate` dla Group to kwota za całe zajęcia, nie za godzinę ani uczestnika.

Nowa stawka grupowa obowiązuje od zapisu, według daty rozpoczęcia sesji, do następnej zmiany. Poprzednie okresy pozostają w historii. Przed pierwszym ustawieniem obowiązuje dotychczasowe liczenie godzinowe.

Rozliczenie nadal obejmuje sesje Completed i zachowuje dotychczasowe zasady pokrycia umową. Pozycja rozliczenia ma dodatkowe `rateType`: `PerSession` lub `Hourly`. Dla PerSession wyświetlaj `rate` jako zł/zajęcia i kwotę `amount` zwróconą przez API, bez mnożenia przez `hours`. Godziny nadal opisują czas zajęć.

Analiza kosztów zwraca dodatkowe `groupSessionRate` (null przy rozliczaniu godzinowym). Koszt jest już policzony przez API. Stawka grupowa dotyczy zajęć publicznych lub oznaczonych Group; treningi 2:1–4:1 zachowują dotychczasowe zasady.

Nie jest potrzebna migracja bazy. Frontend musi dodać pole „Stawka za zajęcia grupowe” i obsłużyć jednostkę PerSession.
