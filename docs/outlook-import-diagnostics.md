# Diagnostyka importu z Outlooka (Owner)

`GET /api/outlook/diagnostics?trainerId={id}&from={iso}&to={iso}` — tylko rola Owner (Trainer: 403).

Endpoint **tylko czyta**: pobiera wydarzenia z kalendarza Outlook wskazanego trenera (Graph `calendarView`) i dla każdego
podaje stan w CRM oraz powód, dla którego jest albo nie jest sesją. Niczego nie importuje ani nie zmienia (może jedynie
odświeżyć token dostępu, jak każde wywołanie Outlooka). Kolejność sprawdzeń odpowiada importowi
(`OutlookWebhookService.ImportOrUpdateEventAsync`); reguła jest w `OutlookImportDiagnosis`.

- `from` / `to` — opcjonalne; domyślnie 7 dni wstecz i 30 dni naprzód. Zakres maksymalnie 62 dni, `to` po `from` (inaczej 400).
- Nieznany `trainerId` w organizacji: 404.
- Trener bez aktywnego połączenia Outlook: `integrationActive = false`, pusta lista `events` (CRM nic z tego kalendarza nie dostaje).
- `subscriptionActive = false`: powiadomienia nie docierają; zmiany importuje dopiero uzgodnienie (`POST /api/outlook/reconcile`).

Każdy element `events` ma dane wydarzenia (`subject`, `startAt`, `endAt`, `type`, `categories`, `locationName`,
`locationEmail`, `organizerEmail`, `attendees`), fakty (`createdByCrm`, `knownLocation`, `beforeImportCutoff`), stan
w CRM (`importedEventId`, `lastImportedAt`, `sessionId`, `sessionStatus`, `sessionTrainerName`, `linkedElsewhere`,
`warnings`) oraz `code` i `explanation`. `summary` liczy wydarzenia według `code`.

| `code` | Znaczenie | Co zrobić |
| --- | --- | --- |
| `Linked` | Połączone z sesją CRM `sessionId`. | Nic. |
| `LinkedCancelled` | Połączone z anulowaną sesją. | Przywróć sesję w CRM albo usuń wydarzenie. Pozostałość sprzed poprawki 2.11.2026 usuwa `DELETE /api/outlook/sync-session/{sessionId}`. |
| `StaleCopy` | Wskazuje sesję, której wydarzenie jest w innym kalendarzu (pozostałość po przeniesieniu). | Usuń kopię w Outlooku: CRM nie przesuwa ani nie anuluje sesji według kopii (przy usunięciu tylko odpina ją od sesji). Anulowanie lub usunięcie sesji w CRM usuwa także takie kopie. |
| `SessionMissing` | Wskazuje sesję, której nie ma w CRM. | Uzgodnienie kalendarza. |
| `UnknownLocation` | Lokalizacja nie pasuje do aktywnej lokalizacji CRM; pomijane bez ostrzeżenia. | Popraw lokalizację w Outlooku. |
| `NotReceived` | CRM nie ma wydarzenia (nie dotarło powiadomienie albo jego obsługa się nie powiodła). | `POST /api/outlook/reconcile` z `trainerId`. |
| `CrmEventWithoutSession` | Wydarzenie utworzył CRM, a jego sesji już nie ma; import celowo je pomija. | Utwórz wydarzenie od nowa w Outlooku albo dodaj sesję w CRM. |
| `BeforeImportCutoff` | Przed datą `Outlook:ImportSessionsFrom`. | Nic albo wpis ręczny. |
| `PastNeedsReview` | Wydarzenie z przeszłości; nie tworzy sesji automatycznie. | „Zamień na sesję” w Problemach Outlook. |
| `MappingWarning` | Import zostawił ostrzeżenie (`explanation` = treść). | Według ostrzeżenia. |
| `SessionDeleted` | Sesja usunięta w CRM; następna zmiana albo uzgodnienie zaimportuje ponownie. | Uzgodnienie kalendarza. |
| `SeriesMaster` | Główne wydarzenie serii; importowane są wystąpienia. | Nic. |
| `NotImported` | Zapisane bez sesji i bez ostrzeżenia. | Uzgodnienie kalendarza; jeśli nie pomoże — zgłoś. |

Przykład:

```http
GET /api/outlook/diagnostics?trainerId=3&from=2026-11-01T00:00:00Z&to=2026-11-15T00:00:00Z
```
