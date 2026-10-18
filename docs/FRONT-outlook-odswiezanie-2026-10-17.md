# Odświeżanie Outlooka (reconcile) — zmiany dla frontu (2026-10-17)

Backend jest wdrożony (commit `f0083d7`). Wywołanie bez nowych pól działa jak dotąd: owner odświeża kalendarze wszystkich trenerów, a trener tylko własny. Odpowiedź może jednak przyjść jako **202**, więc front musi obsłużyć ten kod (punkt 3).

## 1. Panel ownera: rozdzielenie odświeżania

Zamiast jednego przycisku „Odśwież Outlook” dodaj trzy akcje:

| Akcja w UI | Body `POST /api/outlook/reconcile` |
| --- | --- |
| Odśwież wszystkich trenerów | `{}` (jak dotąd) |
| Odśwież mój kalendarz | `{ "onlyMine": true }` |
| Odśwież kalendarz trenera… (wybór z listy) | `{ "trainerId": 14 }` |

- Listę trenerów do wyboru weź z `GET /api/owner/filter-options` (`trainers[]`) albo z `GET /api/trainers`.
- „Wszystkich trenerów” trwa najdłużej, więc niech to nie będzie akcja domyślna. Domyślnie użyj „Mój kalendarz” albo wybranego trenera.
- `trainerId` jest dostępny tylko dla ownera.

## 2. Panel trenera: nowy przycisk „Odśwież Outlook”

Trener ma dostęp do tego samego endpointu (`Authorize: Trainer, Owner`). Dodaj przycisk w ustawieniach Outlooka i/lub w grafiku:

```
POST /api/outlook/reconcile
{ "onlyMine": true }
```

U trenera backend i tak ogranicza zakres do jego kalendarza. `onlyMine` wysyłaj dla czytelności. **Nie wysyłaj `trainerId`**, bo trener dostanie wtedy 403.

## 3. Odpowiedź: 200 albo 202

Backend czeka do **15 s**. Synchronizacja całego kalendarza może trwać dłużej, dlatego są dwa warianty odpowiedzi.

**200**: zakończone w tym czasie; body jak dotąd:

```json
{
  "rangeStartAt": "2026-09-17T00:00:00Z", "rangeEndAt": "2027-04-16T00:00:00Z",
  "integrationsProcessed": 1, "outlookEventsFound": 42, "importedOrUpdatedEvents": 42,
  "missingOutlookEventsMarkedDeleted": 0, "crmSessionsSyncedToOutlook": 3,
  "seriesRequiringAttention": [], "errors": []
}
```

**202**: synchronizacja trwa w tle. Nagłówek `Location: /api/outlook/reconcile/{jobId}`, body:

```json
{
  "jobId": "6b1f…", "state": "Running",
  "message": "Synchronizacja z Outlookiem trwa w tle. Grafik odświeży się po jej zakończeniu.",
  "result": null, "queuedAt": "…", "startedAt": "…", "finishedAt": null
}
```

Przy 202 odpytuj co 3–5 s `GET /api/outlook/reconcile/{jobId}`. Zwraca ten sam kształt co body 202:
- `state: "Queued" | "Running"`: nadal trwa;
- `state: "Completed"`: gotowe; `result` ma te same pola co odpowiedź 200; odśwież grafik;
- `state: "Failed"`: pokaż `message`.

## 4. Zachowanie UI

- Na czas synchronizacji przycisk ma być zablokowany, ze stanem „Synchronizuję…”.
- Ponowne kliknięcie w trakcie nie uruchamia drugiej synchronizacji. Backend zwraca to samo zadanie (ten sam `jobId`).
- Po zakończeniu pokaż krótkie podsumowanie, np. „Zaimportowano/zaktualizowano 42 wydarzenia, wysłano 3 sesje do Outlooka”.
- Wpisy z `result.errors[]` (np. wygasłe połączenie trenera) pokaż jako ostrzeżenie.
- Jeśli `result.seriesRequiringAttention[]` nie jest puste, pokaż informację, że te serie wymagają ponownej synchronizacji serii.
- Opcjonalne pola zakresu: `pastDays` (0–365, domyślnie 30) i `futureDays` (1–730, domyślnie 180).

## 5. Błędy

| Kod | Kiedy | Co pokazać |
| --- | --- | --- |
| 400 `{ message }` | np. brak aktywnego połączenia z Outlookiem | `message` |
| 403 | trener wysłał `trainerId` | „Brak uprawnień” |
| 404 | nieznany trener albo nieznane / wygasłe `jobId` przy odpytywaniu | zakończ odpytywanie, pokaż „Nie udało się sprawdzić stanu synchronizacji” |
| 500 `{ message }` | nieoczekiwany błąd | `message` |

Uwaga: status zadań jest trzymany w pamięci serwera. Po restarcie lub wdrożeniu backendu stare `jobId` zwraca 404. Wtedy wystarczy kliknąć „Odśwież” jeszcze raz.

## 6. Nowe zgłoszenia „do weryfikacji w CRM” (`GET /api/outlook/issues`)

Outlook nie zmienia już po cichu rozliczeń. Zamiast tego dodaje zgłoszenia, które **owner widzi z kalendarzy wszystkich trenerów**. Pozostałe typy zgłoszeń każdy widzi, jak dotąd, tylko z własnego kalendarza.

| `issueType` | Kiedy | Akcja w UI |
| --- | --- | --- |
| `CompletedSessionDeletedInOutlook` | W Outlooku usunięto zrealizowaną sesję. CRM zostawia ją rozliczoną. | Przycisk „Otwórz sesję” (`sessionId`), a jeśli się nie odbyła: korekta albo anulowanie w CRM |
| `CompletedSessionAttendeeRemovedInOutlook` | Z wydarzenia zrealizowanej sesji usunięto klienta (adres w `message`/`email`). Wejście nadal jest rozliczone. | „Otwórz sesję” → korekta listy uczestników (`POST /api/sessions/{id}/complete`) |
| `StartedSessionDeletedInOutlook` | Usunięto sesję, która już się zaczęła lub minęła. CRM jej **nie** anulował. | „Otwórz sesję” → jeśli się nie odbyła, anuluj w CRM |
| `PastEventNeedsReview` | Wydarzenie z przeszłości, którego nie ma w CRM. Nie zostało automatycznie zamienione na sesję, bo to rozliczyłoby wejścia bez potwierdzenia. | „Zamień na sesję” (`POST /api/outlook/imported-events/{externalCalendarEventId}/convert-to-session`) albo „Ignoruj” |

Owner może teraz użyć „Zamień na sesję” i „Ignoruj” (`POST /api/outlook/issues/ignore`) także dla wydarzeń z kalendarzy trenerów.

Zmiany zachowania:
- usunięcie w Outlooku **przyszłej** sesji anuluje ją jak dotąd, a przy zajęciach grupowych uczestnicy dostają teraz to samo powiadomienie co przy anulowaniu w CRM;
- gdy Outlook zwróci pusty kalendarz, a CRM zna w nim wydarzenia, odświeżenie niczego nie anuluje ani nie wysyła, tylko zwraca błąd w `errors[]`.
