# Ręczne odświeżanie synchronizacji Outlook - instrukcja frontu

## Przycisk w UI

W zakładce `Ustawienia -> Integracje -> Outlook` dodaj przycisk z ikoną odświeżania:

`Synchronizuj teraz`

Podczas działania przycisk powinien być zablokowany i pokazywać stan ładowania.
Operacja może potrwać kilkanaście sekund, ponieważ backend pobiera wydarzenia,
porównuje je z CRM i naprawia brakujące połączenia.

## Endpoint

```http
POST /api/outlook/reconcile
Authorization: Bearer {token}
Content-Type: application/json
```

```json
{
  "pastDays": 30,
  "futureDays": 180
}
```

- `Owner` uzgadnia wszystkie aktywne integracje Outlook trenerów.
- `Trainer` uzgadnia tylko własną integrację.
- backend ogranicza `pastDays` do 365 i `futureDays` do 730.

## Co robi operacja

1. Pobiera wydarzenia Outlook z wybranego zakresu.
2. Aktualizuje lokalną kopię wydarzeń i powiązane sesje CRM.
3. Wykrywa wydarzenia usunięte w Outlooku i anuluje niezrealizowane sesje CRM.
4. Tworzy w Outlooku brakujące wydarzenia dla pojedynczych zaplanowanych sesji CRM.
5. Nie synchronizuje automatycznie niepełnych serii, ponieważ mogły być
   duplikatami. Zwraca ich `recurringGroupId` do ręcznej decyzji.

## Odpowiedź

```json
{
  "rangeStartAt": "2026-08-25T00:00:00Z",
  "rangeEndAt": "2027-03-24T00:00:00Z",
  "integrationsProcessed": 3,
  "outlookEventsFound": 84,
  "importedOrUpdatedEvents": 84,
  "missingOutlookEventsMarkedDeleted": 2,
  "crmSessionsSyncedToOutlook": 4,
  "seriesRequiringAttention": [
    "d3fb4d76070441cab6cf9d51cbddc148"
  ],
  "errors": []
}
```

Po zakończeniu pokaż krótkie podsumowanie. Jeżeli `errors` nie jest puste,
wyświetl je w rozwijanej sekcji zamiast zastępować ogólnym komunikatem błędu.

## Serie wymagające decyzji

Dla każdego `recurringGroupId` z `seriesRequiringAttention` pokaż dwie akcje:

- `Ponów synchronizację` - wywołuje
  `POST /api/sessions/series/{recurringGroupId}/sync-outlook`. Body `recurrence`
  jest opcjonalne; backend potrafi odtworzyć regułę z terminów CRM;
- `Usuń błędną serię` - po potwierdzeniu wywołuje
  `DELETE /api/sessions/series/{recurringGroupId}`.

Nie należy ponownie wysyłać `POST /api/sessions/series`, ponieważ utworzy kolejne
sesje CRM.

## Obsługa błędów

- `400` przy usuwaniu oznacza najczęściej, że seria zawiera już sesję
  zrealizowaną lub rozliczoną. Taką serię trzeba poprawić ręcznie.
- Brak aktywnej integracji Outlook również blokuje bezpieczne usunięcie serii,
  jeśli posiada ona istniejące wydarzenia Outlook.
- Po każdej operacji pobierz ponownie kalendarz i status integracji. Backend jest
  źródłem prawdy; front nie powinien samodzielnie zmieniać statusów sesji.
