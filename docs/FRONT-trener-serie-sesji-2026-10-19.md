# Front: serie sesji w portalu trenera (2026-10-19)

Trener może tworzyć sesje cykliczne ze swojego portalu. Body i odpowiedzi są takie same jak w ownerowym `/api/sessions/series`, opisanym w `docs/session-series-frontend.md`. Różnice opisują sekcje poniżej.

Role: `Trainer` i `Owner`. Owner musi mieć profil trenera, bo seria zawsze trafia do zalogowanego trenera. Do zwykłego planowania owner dalej używa `/api/sessions/series`.

## 1. Utworzenie serii

```
POST /api/trainer-portal/sessions/series
```

```json
{
  "session": {
    "startAt": "2026-10-27T08:00:00",
    "endAt": "2026-10-27T09:00:00",
    "locationId": 2,
    "plannedSessionType": "OneToOne",
    "title": null,
    "note": null,
    "participants": [{ "clientId": 15, "countsAgainstPackage": true, "sessionsCharged": 1 }]
  },
  "recurrence": { "frequency": "Weekly", "interval": 1, "daysOfWeek": ["Monday", "Thursday"], "occurrencesCount": 12 }
}
```

Reguły:
- `session.trainerId` jest ignorowane. Backend zawsze ustawia zalogowanego trenera, więc front może je pominąć.
- `locationId` musi być lokalizacją przypisaną do trenera. Uczestnicy muszą być jego klientami (`Client.TrainerId`). W innym razie backend zwraca 400.
- Limity serii są takie same jak u ownera:
  - `frequency`: `Daily` albo `Weekly`; `interval` od 1 do 12;
  - koniec serii wyznacza `occurrencesCount` albo `endDate`;
  - seria ma od 2 do 104 sesji; domyślnie 12, gdy nie podano ani `occurrencesCount`, ani `endDate`;
  - `daysOfWeek` podaje się po angielsku (`Monday`…). Pusta lista oznacza dzień tygodnia `startAt`.
- Pierwsza sesja ma status `Planned`. Wszystkie wystąpienia są walidowane, w tym kolizje grafiku, zanim zapisze się pierwsze.
- **Outlook:** seria zapisuje się w Outlooku trenera jako jedna seria cykliczna. Jeśli trener nie ma podłączonego Outlooka albo synchronizacja się nie uda, seria **nie powstaje** i backend zwraca 400 z komunikatem (`Session series was not created because Outlook synchronization failed: …`). Front powinien wtedy pokazać komunikat i podpowiedź „Podłącz Outlook w ustawieniach”.

Odpowiedź `201` ma kształt `SessionSeriesDto`, taki sam jak u ownera: `recurringGroupId`, `frequency`, `interval`, `occurrencesCount`, `outlookSeriesSynced`, `outlookSyncWarning` i `sessions[]`.

### Tylko sesje indywidualne i semi

Trener może tworzyć serie tylko z sesji indywidualnych i semi. Zajęcia grupowe i sesje z publicznym zapisem planuje owner. Backend zwraca 400 („Trainers can create series of individual or semi sessions only…”), gdy:
- `isPubliclyBookable: true`;
- `plannedSessionType: "Group"`;
- `outlookCategories` zawiera „Zajęcia grupowe”, bo Outlook zamieniłby takie sesje w zajęcia grupowe.

W formularzu trenera należy ukryć te opcje. Typy `OneToOne`, `Semi` i pozostałe niegrupowe działają jak przy pojedynczej sesji.

## 2. Ponowienie synchronizacji z Outlookiem

```
POST /api/trainer-portal/sessions/series/{recurringGroupId}/sync-outlook
```

Body może być puste. Backend sam odtworzy reguły powtarzania z dat sesji. Opcjonalnie można wysłać `SessionRecurrenceDto` z formularza.

Endpoint służy do ponowienia synchronizacji, gdy odpowiedź zawiera `outlookSeriesSynced: false` albo `outlookSyncWarning`. Odpowiedź `200` to `{ recurringGroupId, outlookSeriesSynced, outlookSyncWarning }`.

Seria już w pełni powiązana z Outlookiem nie jest wysyłana drugi raz. Endpoint wtedy po prostu zwraca `outlookSeriesSynced: true`.

## 3. Usunięcie serii

```
DELETE /api/trainer-portal/sessions/series/{recurringGroupId}
```

Usuwa **tylko przyszłe, niezrealizowane sesje**: termin po bieżącej chwili, status inny niż `Completed` i żaden uczestnik nie ma odliczonego wejścia. Sesje przeszłe, zrealizowane i rozliczone zostają bez zmian.

- Jeśli cała seria jest jeszcze przed nami, znika razem z serią w Outlooku (`outlookSeriesDeleted: true`).
- Jeśli część sesji już się odbyła, każde przyszłe wystąpienie jest usuwane osobno, także z Outlooka. Seria w Outlooku zostaje z przeszłymi terminami (`outlookSeriesDeleted: false`).

Odpowiedź `200`:

```json
{ "recurringGroupId": "…", "deletedSessionsCount": 8, "keptSessionsCount": 4, "outlookSeriesDeleted": false }
```

`keptSessionsCount` to nowe pole, czyli liczba sesji, które zostały w serii. W ownerowym `DELETE /api/sessions/series/{id}` zawsze wynosi 0. Ownerowy endpoint działa bez zmian: odmawia usunięcia serii zawierającej zrealizowane sesje.

Sugerowany tekst potwierdzenia: „Usunąć przyszłe sesje z tej serii? Sesje, które już się odbyły, zostaną bez zmian.”

## 4. Błędy

| Kod | Kiedy |
| --- | --- |
| 400 `{ message }` | walidacja serii, obca lokalizacja lub klient, seria grupowa lub publiczna, brak Outlooka lub nieudana synchronizacja przy tworzeniu, kolizja w grafiku |
| 403 | studio bez pełnego CRM (`OrganizationCapabilityUnavailable`), tak jak pozostałe endpointy portalu trenera |
| 404 | `sync-outlook` i `DELETE`: seria nie istnieje albo nie należy w całości do zalogowanego trenera |
