# Outlook i korekty zrealizowanych sesji — notatka dla frontu (2026-10-18)

Zbiorcza instrukcja do paneli: połączenie z Outlookiem, odświeżanie, lista zgłoszeń, korekta zrealizowanej sesji. Backend jest wdrożony. Wszystkie odpowiedzi błędów mają postać `{ "message": "…" }`. Komunikat pokazuj użytkownikowi bez zamiany na ogólne „Nie udało się”.

---

## 1. Połączenie z Outlookiem (owner i trener)

- **Start:** `GET /api/outlook/connect-url` zwraca `{ "url": "…" }`. Otwórz ten adres w tej samej przeglądarce.
- **Microsoft najpierw pokazuje wybór konta.** Ekran zgody pojawia się tylko wtedy, gdy uprawnienia nie są jeszcze przyznane. W UI dodaj podpowiedź: „Wybierz konto Microsoft, w którym prowadzisz kalendarz”.
- **Stan:** `GET /api/outlook/status` zwraca `{ isConnected, provider, email, connectedAt }`. Pokaż `email`, żeby było widać, które konto jest podłączone.
- **Rozłączenie:** `POST /api/outlook/disconnect`.

### Strona powrotu (callback)

Microsoft wraca na `/api/outlook/callback`. Sukces to 200 `{ message }`. Błędy to 400:

| Pole `code` | Znaczenie | Tekst w UI |
| --- | --- | --- |
| `AdminConsentRequired` | firmowe konto Microsoft 365, w którym zgodę musi dać administrator | pokaż `message` i dopisz „Poproś administratora Microsoft 365 o zatwierdzenie aplikacji” |
| `AccountNotSupported` | konto z innego tenanta, nieobsługiwany typ konta albo błędny adres powrotu | pokaż `message` |
| `AccessDenied` | użytkownik anulował logowanie albo odmówił zgody | pokaż `message` i przycisk „Spróbuj ponownie” |
| `SignInRequired`, `MicrosoftUnavailable`, `MicrosoftError` | inne odpowiedzi Microsoftu | pokaż `message` |
| brak `code`, `message` = `OAuth browser correlation failed…` | łączenie rozpoczęte w innej przeglądarce | „Rozpocznij łączenie ponownie w tej samej przeglądarce” |
| brak `code`, `message` = `Invalid or expired OAuth state.` | minęło ponad 10 minut albo strona została otwarta dwa razy | „Łączenie wygasło — spróbuj ponownie” |

Błąd z `code` ma też pola `microsoftCode` (np. `AADSTS65001`) i `detail`. Pokaż je drobną czcionką, bo przydają się administratorowi.

---

## 2. Odświeżanie Outlooka

`POST /api/outlook/reconcile`

| Gdzie | Przycisk | Body |
| --- | --- | --- |
| Owner | Odśwież mój kalendarz (domyślny) | `{ "onlyMine": true }` |
| Owner | Odśwież kalendarz trenera… (lista trenerów z `GET /api/owner/filter-options` → `trainers[]`) | `{ "trainerId": 14 }` |
| Owner | Odśwież wszystkich trenerów (najwolniejsze) | `{}` |
| Trener (**nowy przycisk**) | Odśwież mój Outlook | `{ "onlyMine": true }` (bez `trainerId`, inaczej 403) |

Opcjonalne pola: `pastDays` (0–365, domyślnie 30) i `futureDays` (1–730, domyślnie 180).

**Odpowiedź:**
- **200**: gotowe. Wynik ma pola:
  - `integrationsProcessed` (kalendarze), `outlookEventsFound` (wydarzenia w Outlooku), `importedOrUpdatedEvents` (zaimportowane lub zaktualizowane);
  - `crmSessionsSyncedToOutlook` (sesje CRM wysłane do Outlooka), `missingOutlookEventsMarkedDeleted` (oznaczone jako usunięte);
  - `seriesRequiringAttention[]` (serie wymagające uwagi), `errors[]` (błędy).
- **202**: trwa w tle. Body: `{ jobId, state, message }`. Co 3–5 s odpytuj `GET /api/outlook/reconcile/{jobId}`, aż `state` będzie `Completed` (wynik w `result`) albo `Failed` (pokaż `message`). 404 przy odpytywaniu oznacza restart serwera: poproś o ponowne kliknięcie.

**UI:**
- Na czas trwania przycisk jest zablokowany z tekstem „Synchronizuję…”. Ponowne kliknięcie nie startuje drugiej synchronizacji.
- Po zakończeniu pokaż podsumowanie i odśwież grafik.
- `errors[]` pokaż jako ostrzeżenie. Szczególnie ważny jest komunikat „Outlook zwrócił pusty kalendarz…”: oznacza problem z połączeniem, a backend wtedy niczego nie anulował.

---

## 3. Lista zgłoszeń Outlooka (panel „Problemy z kalendarzem”)

`GET /api/outlook/issues` → lista:

```json
{
  "externalCalendarEventId": 812, "sessionId": 345, "subject": "Trening Anna K",
  "startAt": "2026-10-14T16:00:00Z", "endAt": "2026-10-14T17:00:00Z",
  "locationName": "Kłaj", "organizerEmail": "trener@…",
  "issueType": "CompletedSessionAttendeeRemovedInOutlook",
  "message": "Do weryfikacji w CRM: klienta usunięto w Outlooku z zrealizowanej sesji: anna@…",
  "email": "anna@…", "importedAt": "…"
}
```

**Kto co widzi:**
- trener: zgłoszenia ze swojego kalendarza;
- owner: zgłoszenia ze swojego kalendarza oraz zgłoszenia „do weryfikacji w CRM” z kalendarzy wszystkich trenerów (typy z prefiksem w tabeli niżej).

| `issueType` | Co się stało | Przyciski |
| --- | --- | --- |
| `CompletedSessionDeletedInOutlook` | W Outlooku usunięto **zrealizowaną** sesję. W CRM zostaje rozliczona. | „Otwórz korektę sesji” (`sessionId`, sekcja 4), „Ignoruj” |
| `CompletedSessionAttendeeRemovedInOutlook` | Z zrealizowanej sesji usunięto w Outlooku klienta (`email`). Wejście nadal jest rozliczone. | „Otwórz korektę sesji”, „Ignoruj” |
| `StartedSessionDeletedInOutlook` | Usunięto sesję, która już się zaczęła. CRM jej **nie** anulował. | „Otwórz sesję” (anuluj, jeśli się nie odbyła), „Ignoruj” |
| `PastEventNeedsReview` | Wydarzenie z przeszłości, którego nie ma w CRM. Nie stało się sesją automatycznie. | „Zamień na sesję”, „Ignoruj” |
| `UnknownClient` | nieznany adres uczestnika (`email`) | „Wyślij zaproszenie”, „Powiąż z klientem”, „Ignoruj” |
| `UnknownLocation`, `UnknownTrainer`, `LocationLimitExceeded`, `Other` | problemy z mapowaniem | „Ignoruj” (i ewentualnie poprawka w Outlooku) |

**Akcje:**
- **Zamień na sesję:** `POST /api/outlook/imported-events/{externalCalendarEventId}/convert-to-session` z body `{}`. Zwraca ID sesji. Sesja z przeszłości zostanie automatycznie zakończona i rozliczona przez CRM, więc przed wysłaniem pokaż potwierdzenie: „Utworzyć sesję i rozliczyć wejścia uczestników?”.
- **Ignoruj:** `POST /api/outlook/issues/ignore` z body `{ "externalCalendarEventId": 812, "message": "<dokładnie pole message>" }`.
- **Wyślij zaproszenie:** `POST /api/outlook/issues/send-invite` z body `{ "email": "…" }`.
- **Powiąż z klientem:** `POST /api/outlook/issues/link-client` z body `{ "clientId": 41, "email": "…" }`.

Owner może zamieniać i ignorować także wydarzenia z kalendarzy trenerów. Po każdej akcji pobierz listę od nowa.

---

## 4. Korekta zrealizowanej sesji (owner)

Zrealizowanej sesji nie edytuje się zwykłym formularzem. Każda zmiana jest korektą rozliczenia: backend cofa poprzednie naliczenie, rozlicza od nowa i zapisuje historię. **Wszystkie poniższe akcje są tylko dla ownera.** Trenerowi ich nie pokazuj.

### 4.1 Zmiana uczestników, obecności lub typu — „Skoryguj rozliczenie”

`POST /api/sessions/{sessionId}/complete`

```json
{
  "actualSessionType": "TwoToOne",
  "correctionReason": "Anna nie była obecna",
  "participants": [
    { "clientId": 12, "attendanceStatus": "Present", "countsAgainstPackage": true, "sessionsCharged": 1, "note": null }
  ]
}
```

- Wysyłaj **pełną docelową listę** uczestników. Kogo nie ma na liście, ten zostaje usunięty z sesji i odzyskuje wejście.
- Wejście jest zdejmowane **tylko** przy `attendanceStatus: "Present"` i `countsAgainstPackage: true`. Inne statusy obecności są zapisywane bez zdejmowania wejścia.
- `actualSessionType`: `OneToOne`, `TwoToOne`, `ThreeToOne`, `FourToOne`, `Group`.
- W UI wymagaj `correctionReason` (1–1000 znaków).
- Formularz: lista uczestników z przełącznikiem obecności i naliczania, „dodaj klienta”, wybór typu i pole powodu.

### 4.2 Sesja się nie odbyła — „Cofnij realizację”

`PUT /api/sessions/{sessionId}` z pełnym `UpdateSessionDto` oraz:
- `"status": "Cancelled"` → sesja anulowana; wszystkie wejścia wracają, a wydarzenie znika z Outlooka;
- `"status": "Planned"` → sesja wraca do grafiku jako niezrealizowana;
- oba warianty wymagają `"correctionReason": "…"`.

### 4.3 Zmiana danych bez przeliczania

`PUT /api/sessions/{sessionId}` ze `"status": "Completed"` zmienia termin, trenera, lokalizację i tytuł. Rozliczeni uczestnicy zostają bez zmian. Do zmiany osób używaj 4.1.

### 4.4 Historia korekt

`GET /api/sessions/{sessionId}/corrections` zwraca listę `{ changeType, reason, beforeState, afterState, changedByUserId, createdAt }`, od najnowszej.

`changeType`:
- `SessionRecalculated`: ponowne rozliczenie (4.1);
- `SessionStatusCorrected`: cofnięcie realizacji (4.2);
- `CompletedSessionUpdated`: zmiana danych (4.3).

Pokaż ją jako zakładkę „Historia” w szczegółach sesji.

### 4.5 Blokady (400 z `message`)

Backend nie zapisze częściowej korekty, gdy:
- rozliczenie trenera za ten miesiąc jest wypłacone (przy zmianie terminu dotyczy starego i nowego miesiąca). W UI dodaj podpowiedź „Najpierw otwórz ponownie rozliczenie trenera”;
- korygowana była ostatnia sesja pakietu, a z kolejnego pakietu klient już korzystał, ma do niego oczekującą płatność albo istnieje dalszy cykl;
- pakiet jest zamknięty albo klient jest w archiwum.

Pokaż `message`, zachowaj dane w formularzu i nie oznaczaj zapisu jako udanego.

### 4.6 Po udanej korekcie

Odśwież:
- szczegóły sesji i historię korekt;
- pakiety i saldo każdego zmienionego klienta;
- rozliczenie trenera, jeśli jest otwarte.

Nie przeliczaj wejść lokalnie: odpowiedzi backendu są źródłem prawdy. Korekta dotyczy jednego wystąpienia serii i nie przenosi się na pozostałe.

---

## 5. Co Outlook robi sam (do podpowiedzi i pomocy w UI)

| Zmiana w Outlooku | Efekt w CRM |
| --- | --- |
| Zmiana terminu, lokalizacji lub uczestników **zaplanowanej** sesji | aktualizacja sesji, jak dotąd |
| Usunięcie **przyszłej** sesji | sesja anulowana; uczestnicy zajęć grupowych dostają powiadomienie |
| Usunięcie sesji, która **już się zaczęła** | bez zmian i zgłoszenie dla ownera |
| Usunięcie **zrealizowanej** sesji albo klienta z niej | bez zmian w rozliczeniu i zgłoszenie dla ownera |
| Nowe **przyszłe** wydarzenie w lokalizacji studia | nowa zaplanowana sesja, jak dotąd |
| Nowe wydarzenie **z przeszłości** | zgłoszenie „Zamień na sesję”; nic nie jest rozliczane automatycznie |

Zasada dla UI: rozliczenia (wejścia, pakiety, saldo) zmieniają się tylko przez akcje w CRM. Outlook może je co najwyżej zgłosić do weryfikacji.
