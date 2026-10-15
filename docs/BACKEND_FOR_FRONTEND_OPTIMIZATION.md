# Backend pod optymalizację panelu Ownera — instrukcja integracji (2026-10-15)

Dokument dla frontendu (Codex). Opisuje, z czego panel Ownera może korzystać od teraz, w jakiej kolejności to podpinać i co zostało do decyzji. Wszystkie zmiany są **rozszerzeniami**: istniejące wywołania bez nowych parametrów zwracają to samo co wcześniej.

Konwencje wspólne:

- JSON w camelCase. Enumy w JSON są **liczbami** (mapowanie w `docs/FRONT-listy-klientow-i-wplat.md` i w Swaggerze); pola `*PaymentStatus` w nowych miejscach są tekstem (`"Paid"`, `"PartiallyPaid"`...), tak jak w `activePackages`.
- Daty: UTC w ISO 8601, chyba że pole opisuje inaczej (rozliczenia trenerów zwracają `paidAt` w czasie studia, jak dotąd).
- Parametry w query string, nazwy bez rozróżniania wielkości liter.
- Paginacja: `page` 1..100000, `pageSize` 1..100, domyślnie `1` i `25`. Koperta: `{ items, page, pageSize, totalCount, totalPages }`; `totalPages = 0` przy pustym wyniku.
- Wszystkie endpointy niżej wymagają roli **Owner** i działają w organizacji z tokenu (izolacja przez filtry EF). Lokalizacja jest filtrem w obrębie organizacji; Owner widzi wszystkie lokalizacje swojej organizacji.

## 0. Wymagania A–F w zestawieniu z API

| Wym. | Już było | Zmiana | Endpoint | Kontrakt | Migracja |
| --- | --- | --- | --- | --- | --- |
| A Płatności | `GET /api/billing/payments`: paginacja (max 100), filtry przed paginacją, w tym `search`, `clientId`, `locationId`, `status`, `source`, `method`, `from`, `to`, `amountMin`, `amountMax`, `hasOverpayment`, oraz `sort` | Dodane: `order`, alias `overpayment`, `locationMatch=effective`. Do każdej pozycji dodany stan pakietu, nadpłata i lokalizacja efektywna | to samo | §1 | nie |
| B Rozliczenia trenerów | tylko pojedynczo: `GET /api/trainers/{id}/settlement` | nowy endpoint zbiorczy | `GET /api/owner/trainer-settlements` | §2 | nie |
| C Klienci | `GET /api/clients` (pełna tablica), `GET /api/clients/filter` (bez paginacji) | tryb paginowany w `GET /api/clients`, nowy `filter-options` | `GET /api/clients?page=…`, `GET /api/owner/filter-options` | §3, §4 | nie |
| D Historia sesji | `GET /api/sessions/filter` z filtrami, bez paginacji | tryb paginowany po podaniu `page`/`pageSize`; Grafik bez zmian | to samo | §5 | nie |
| E Zapisy grupowe | lista paginowana, ale tylko status i kwota | dane klienta, pakietu, uczestnika i `allowedActions`; pełne pola oferty w `package-options` | to samo | §6 | nie |
| F Idempotencja | `idempotencyKey` w body wpłaty personelu i klienta (fingerprint, blokada, 409) | nagłówek `Idempotency-Key`, klucz powiązany z użytkownikiem (nie klientem), sprawdzenie stanu | `POST /api/billing/payments`, `GET /api/owner/operations/{key}` | §7 | nie (dla wpłat); **tak** dla pozostałych operacji, do decyzji (§9) |

## 1. Płatności — `GET /api/billing/payments`

| Parametr | Wartości | Uwagi |
| --- | --- | --- |
| `page`, `pageSize` | jak wyżej | wartości spoza zakresu są **przycinane** (zachowanie istniejące) |
| `search` | ≤100 znaków | imię i nazwisko lub email klienta |
| `clientId`, `status`, `source`, `method`, `receiptStatus`, `legalEntityId`, `paymentProvider` | — | bez zmian |
| `locationId` | int | domyślnie dopasowuje `ClientPayment.LocationId` |
| `locationMatch` | `payment` (domyślnie) / `effective` | **nowy**. `effective` = lokalizacja wpłaty, a gdy jej brak (stare/importowane wpłaty), główna lokalizacja klienta |
| `from`, `to` | ISO 8601 | data wpłaty (`paymentDate`), włącznie |
| `amountMin`, `amountMax` | ≥0 | kwota brutto `amount` |
| `hasOverpayment` / `overpayment` | bool | **`overpayment` nowy alias**; nadpłata = `balanceCreditAmount > 0` |
| `sort` | `paymentDateDesc` (domyślnie), `paymentDate`, `amountDesc`, `amount` | stabilne (dogrywka po `paymentDate`, potem `id`) |
| `order` | `asc` / `desc` | **nowy**; nadpisuje kierunek z `sort` (`sort=amount&order=desc` = `amountDesc`) |

Nowe pola w każdej pozycji (wszystkie liczone przez backend):

```json
{
  "id": 812, "clientId": 41, "clientName": "Anna Nowak",
  "locationId": null, "locationName": null,
  "effectiveLocationId": 2, "effectiveLocationName": "Niepołomice", "effectiveLocationSource": "ClientHome",
  "amount": 250.00, "appliedToPackageAmount": 200.00, "balanceCreditAmount": 50.00,
  "overpaymentAmount": 50.00, "hasOverpayment": true,
  "clientPackageId": 133, "packageName": "8 treningów",
  "packageTotalPrice": 500.00, "packageAmountPaid": 200.00, "packageAmountDue": 300.00, "packagePaymentStatus": "PartiallyPaid",
  "status": 2, "source": 2, "method": 3, "paymentDate": "2026-10-03T10:00:00Z",
  "canConfirm": false
}
```

- `overpaymentAmount` to dokładnie `balanceCreditAmount`, czyli część wpłaty zaksięgowana jako saldo klienta. Znaczenie nie zmieniło się.
- `packageAmountDue = max(0, totalPrice − amountPaid)` liczy się tak samo jak w `GET /api/billing/clients/{id}`. To **bieżący** stan pakietu, a nie stan z chwili wpłaty. Gdy wpłata nie ma pakietu, pola `package*` mają wartość `null`.
- Przykład: `GET /api/billing/payments?locationId=2&locationMatch=effective&overpayment=true&sort=amount&order=desc&page=1&pageSize=50`

## 2. Rozliczenia trenerów — `GET /api/owner/trainer-settlements`

| Parametr | Wartości | Uwagi |
| --- | --- | --- |
| `year`, `month` | 2000..2100, 1..12 | wymagane |
| `locationId` | int | trenerzy przypisani do lokalizacji **lub** mający w tym miesiącu zakończone sesje w tej lokalizacji. Kwoty liczone tylko z sesji tej lokalizacji, jak w `GET /api/trainers/{id}/settlement?locationId=` |
| `search` | ≤100 | imię i nazwisko lub email trenera |
| `page`, `pageSize` | 1..100000, 1..100 | spoza zakresu: **400** |

Kolejność: nazwisko, imię, id. Zbiór: aktywni (niezarchiwizowani) trenerzy organizacji, tak samo jak w `GET /api/trainers`.

```json
{
  "year": 2026, "month": 10, "locationId": 2,
  "items": [
    { "trainerId": 7, "trainerFullName": "Anna Kowalska", "totalHours": 31.5, "totalSessions": 28, "totalAmount": 3150.00,
      "nonContractedTotalHours": 2.0, "nonContractedTotalSessions": 2, "isPaid": false, "paidAt": null }
  ],
  "page": 1, "pageSize": 25, "totalCount": 6, "totalPages": 1,
  "summary": { "totalAmount": 15420.00, "paidAmount": 6200.00, "unpaidAmount": 9220.00, "trainersCount": 6, "paidTrainersCount": 2 },
  "asOf": "2026-10-15T12:31:08Z", "timeZone": "Europe/Warsaw"
}
```

- `totalHours`, `totalSessions` i `totalAmount` mają tę samą definicję co w rozliczeniu pojedynczym: liczą się tylko sesje objęte umową. Sesje bez umowy są w polach `nonContracted*` i nie wchodzą do kwot.
- `summary` obejmuje **cały przefiltrowany zbiór**, a nie tylko bieżącą stronę.
- Backend nie zwraca częściowych wyników. Błąd przy odczycie danych kończy się kodem 5xx, więc front nie może pokazywać wtedy „0 zł”: musi wyświetlić stan błędu.
- `isPaid` i `paidAt` są zapisywane **dla trenera i miesiąca, bez podziału na lokalizacje** (patrz §9).
- Oznaczanie jako opłacone i ponowne otwarcie działa bez zmian: `POST /api/trainers/{id}/settlement/mark-as-paid|reopen?year=&month=`.

## 3. Klienci — `GET /api/clients`

Bez parametrów `page` i `pageSize` endpoint zwraca **tę samą pełną tablicę co dotąd**. Jeśli w query jest `page` albo `pageSize`, zwraca kopertę paginowaną.

| Parametr | Wartości | Uwagi |
| --- | --- | --- |
| `page`, `pageSize` | 1..100000, 1..100 | spoza zakresu: **400** |
| `search` | ≤100 | imię i nazwisko lub email |
| `trainerId` | int | |
| `locationId` | int | główna lokalizacja klienta (`Client.LocationId`), jak w `/api/clients/filter` |
| `isArchived` | `false` (domyślnie) / `true` | `true` = tylko zarchiwizowani |
| `status` | `New`, `Active`, `Inactive` | dokładne dopasowanie `Client.Status` |
| `packageStatus` | `active` / `none` | `active` = co najmniej jeden aktywny, niezamknięty pakiet (ten sam zbiór co `activePackages`) |
| `sort` | `createdDesc` (domyślnie), `created`, `name`, `nameDesc` | `name` = nazwisko, imię, id; dogrywka zawsze po id |
| `order` | `asc` / `desc` | nadpisuje kierunek z `sort` |

```json
{
  "items": [ { "id": 41, "fullName": "Anna Nowak", "locationId": 2, "trainerId": 7, "status": "Active", "isArchived": false,
               "activeClientPackageId": 133, "activePackageRemainingSessions": 3, "currentBalance": 0, "activePackages": [ ] } ],
  "page": 1, "pageSize": 25, "totalCount": 212, "totalPages": 9,
  "summary": { "activeClientsCount": 148, "withActivePackageCount": 151 }
}
```

- Pozycje mają ten sam kształt `ClientDto` co tablica legacy, łącznie z podsumowaniem pakietów i salda.
- `summary` liczy się z całego przefiltrowanego zbioru. `activeClientsCount` to klienci z `status == "Active"`, a `withActivePackageCount` to klienci z aktywnym pakietem (patrz §9).

## 4. Opcje do dropdownów — `GET /api/owner/filter-options`

| Parametr | Wartości | Uwagi |
| --- | --- | --- |
| `locationId` | int | klienci: lokalizacja główna **lub** członkostwo grupowe w tej lokalizacji; trenerzy: przypisani do lokalizacji |
| `search` | ≤100 | imię i nazwisko lub email |
| `limit` | 1..100, domyślnie 20 | limit osobno dla klientów i trenerów |

```json
{ "clients": [ { "id": 41, "name": "Anna Nowak" } ], "trainers": [ { "id": 7, "name": "Anna Kowalska" } ] }
```

Zwracani są tylko aktywni (niezarchiwizowani) klienci i trenerzy. Sortowanie: nazwisko, imię, id. Endpoint służy do autouzupełniania: należy wysyłać `search` z debounce, a nie pobierać całej listy.

## 5. Historia sesji — `GET /api/sessions/filter`

Grafik działa bez zmian. Bez `page` i `pageSize` endpoint zwraca pełną tablicę rosnąco po `startAt`. Jeśli w query jest `page` albo `pageSize`, zwraca kopertę `{ items, page, pageSize, totalCount, totalPages }`.

| Parametr | Uwagi |
| --- | --- |
| `clientId`, `trainerId`, `locationId`, `status`, `from`, `to` | bez zmian, działają przed paginacją |
| `page`, `pageSize` | 1..100000, 1..100, domyślnie 1 i 25; spoza zakresu: **400** |
| `order` | `desc` (domyślnie: najnowsze pierwsze) / `asc`; tylko w trybie paginowanym |

Pozycje mają kształt `SessionDto` taki sam jak w Grafiku. Przykład historii klienta: `GET /api/sessions/filter?clientId=41&page=1&pageSize=20`.

## 6. Zapisy grupowe

### `GET /api/group-enrollments/sessions/{sessionId}?page=&pageSize=`

Koperta i dotychczasowe pola zostają bez zmian. Każda pozycja ma teraz dodatkowe pola:

```json
{
  "id": 55, "sessionId": 900, "clientId": 41, "clientPackageId": 140,
  "status": "AwaitingPayment", "amountDue": 120.00, "reservesSeat": false,
  "clientName": "Anna Nowak", "clientEmail": "anna@example.com", "clientPhone": "+48…", "clientIsArchived": false,
  "packageName": "Karnet grupowy 4", "packageTotalPrice": 120.00, "packageAmountPaid": 0.00, "packagePaymentStatus": "Unpaid",
  "packageValidUntil": null, "packageIsActive": false, "packageTotalEntries": 4, "packageUsedEntries": 0,
  "sessionParticipantId": null, "attendanceStatus": null,
  "allowedActions": ["RecordPayment"]
}
```

- `status`: `Booked`, `ReadyToBook`, `AwaitingPayment`, `PackageUnavailable`, `Unavailable`. Reguła jest ta sama co dotąd.
- `allowedActions`:
  - `Confirm` pojawia się tylko przy `ReadyToBook` i oznacza `POST /api/group-enrollments/{id}/confirm`.
  - `RecordPayment` pojawia się przy `AwaitingPayment` i `amountDue > 0`. Oznacza `POST /api/billing/payments` z `clientPackageId`.
  - Dla klienta zarchiwizowanego lista jest pusta.
- `sessionParticipantId` i `attendanceStatus` opisują ostatni wiersz uczestnika tego klienta na zajęciach, także odwołany.
- `GET /api/group-enrollments/{id}` i odpowiedzi `POST` nie zmieniły się: nowe pola mają tam wartość `null`.

### `GET /api/group-enrollments/sessions/{sessionId}/package-options?clientId=`

Każda pozycja `offers[]` ma teraz komplet pól oferty, więc nie trzeba pobierać katalogu pakietów:

```json
{ "packageId": 12, "name": "Karnet grupowy 4", "price": 120.00, "currency": "PLN", "sessionsLimit": 4, "durationDays": 30,
  "canSelect": true, "reasons": [],
  "description": "4 wejścia, 30 dni", "sessionsPerWeek": 1, "participantsCount": 1,
  "locationId": 2, "locationName": "Niepołomice", "isPubliclyAvailable": true, "unitPrice": 30.00 }
```

`unitPrice = round(price / sessionsLimit, 2)` jest liczony tak samo jak `expectedUnitPrice` pakietu klienta tworzonego z tej oferty.

## 7. Idempotencja operacji finansowych

### `POST /api/billing/payments` (wpłata wprowadzana przez personel)

- Wyślij nagłówek `Idempotency-Key: <UUID>`. Jest równoważny polu `idempotencyKey` w body. Jeśli wyślesz oba, muszą być równe, inaczej backend zwraca **400**.
- Ponowienie z tym samym kluczem i identycznym payloadem zwraca **pierwotną wpłatę** (200/201 z tym samym `id`) i nie tworzy nowej.
- Ten sam klucz z innym payloadem (kwota, metoda, data, notatka, pakiet) albo **dla innego klienta** daje **409**.
- Klucz jest powiązany z organizacją, użytkownikiem, typem operacji (`StaffEntry`) i fingerprintem SHA-256 payloadu. Zapis odbywa się w transakcji z blokadą advisory, więc dwa równoległe ponowienia tworzą jedną wpłatę.
- Klucz generuj **raz na intencję użytkownika**, czyli przy otwarciu formularza lub pierwszym kliknięciu. Nie generuj nowego klucza przy ponowieniu.

### `GET /api/owner/operations/{key}`

Służy do sprawdzenia wyniku, gdy po timeoucie nie wiadomo, czy wpłata została zapisana.

```json
{ "key": "6f1c…", "operation": "CreateStaffPayment", "state": "Completed", "result": { "id": 812, "status": 2, "amount": 250.00 }, "updatedAt": "2026-10-15T12:40:11Z" }
```

- **404** `{ "state": "NotFound", "message": "…" }` oznacza, że pod tym kluczem nic nie zostało zatwierdzone. Bezpiecznie jest wtedy ponowić **to samo** żądanie z tym samym kluczem: jeśli pierwsze właśnie się kończy, blokada zwróci jego wynik.
- **400** zwraca niepoprawny UUID.
- Widoczne są tylko operacje bieżącego użytkownika w bieżącej organizacji.
- `result` pokazuje **bieżący** stan wpłaty: mogła zostać później potwierdzona lub cofnięta.

Na razie obsługiwane jest **tylko tworzenie wpłaty przez personel**. Potwierdzenie, odrzucenie, cofnięcie i paragon są z natury idempotentne albo zwracają błąd stanu (np. „już potwierdzona”). Nie mają jednak magazynu kluczy (patrz §9).

## 8. Format błędów

Wszystkie błędy mają postać `{ "message": "…" }`. Wyjątkiem jest 404 z `operations`, które dodaje pole `state`.

| Kod | Kiedy |
| --- | --- |
| 400 | niepoprawny parametr: zakres `page`/`pageSize`, nieznany `sort`, `order`, `packageStatus` czy `locationMatch`, `amountMin > amountMax`, zły UUID, błąd reguły biznesowej. Walidacja `[FromQuery]` może też zwrócić standardowy `ValidationProblemDetails` z polem `errors` |
| 401 | brak lub wygaśnięcie tokenu |
| 403 | rola inna niż Owner lub brak uprawnień do zasobu |
| 404 | brak zasobu albo operacji |
| 409 | ten sam klucz idempotencji z innym payloadem lub dla innego klienta; konflikt współbieżności lub bazy |
| 5xx | błąd serwera. **Nie wolno pokazywać go jako zera ani pustej listy** |

## 9. Co wdrożono, a co zostało do decyzji

Wdrożone, bez migracji bazy: §1–§7. Build i testy jednostkowe przechodzą (254). Nowe testy integracyjne PostgreSQL w `OwnerPanelReadTests` zostały **pominięte** lokalnie, bo nie ustawiono `TPAY_TEST_DB`. Trzeba je uruchomić na bazie testowej przed wdrożeniem.

Do decyzji (nic z tego nie zostało zmienione):

1. **Lokalizacja płatności.** Domyślny `locationId` dopasowuje nadal tylko lokalizację wpłaty. Czy panel ma domyślnie używać `locationMatch=effective`, czyli brać lokalizację klienta dla wpłat bez lokalizacji? Ewentualnie: czy uzupełnić `LocationId` w starych wpłatach skryptem danych?
2. **Opłacenie rozliczenia trenera per lokalizacja.** `isPaid` jest dziś zapisywane dla trenera i miesiąca. Przy filtrze `locationId` trener opłacony w innej lokalizacji też ma `isPaid=true`. Rozliczenie per lokalizacja wymaga migracji.
3. **Kwota opłaconego rozliczenia.** Po oznaczeniu jako opłacone backend zapisuje migawkę (`TrainerMonthlySettlement.TotalAmount`). Odczyt, zarówno pojedynczy, jak i zbiorczy, liczy jednak kwotę na bieżąco. `paidAmount` w podsumowaniu to bieżąca kwota rozliczeń opłaconych. Czy po opłaceniu pokazywać migawkę?
4. **„Aktywny klient”.** `activeClientsCount` to `Client.Status == "Active"`. Obok zwracany jest `withActivePackageCount`. Którą definicję panel ma pokazywać? `packageStatus` obsługuje dziś tylko `active` i `none`. Czy potrzebne są dodatkowe wartości, np. nieopłacony albo wygasający pakiet, i z jaką definicją?
5. **Idempotencja pozostałych operacji** (potwierdzenie, odrzucenie, cofnięcie wpłaty, oznaczenie rozliczenia trenera jako opłaconego, zapis grupowy). Potrzebna jest nowa tabela `IdempotencyRecords`: organizacja, użytkownik, typ operacji, hash payloadu, stan, zserializowany wynik i znaczniki czasu, z unikalnym indeksem. Wymaga to migracji i decyzji, które operacje objąć oraz jak długo przechowywać klucze. Do tego czasu `GET /api/owner/operations/{key}` zna tylko wpłaty.

Zmiana zachowania wynikająca z wymagania F: ponowne użycie tego samego `idempotencyKey` przez tego samego użytkownika **dla innego klienta** daje teraz 409. Wcześniej tworzyło drugą wpłatę.

## 10. Zmiany wymagane na frontendzie

1. Płatności:
   - Usuń pobieranie do 1000 wpłat oraz dociąganie klientów i rozliczeń.
   - Lista ma używać `page`, `pageSize` i filtrów serwerowych.
   - Lokalizację wyświetlaj z `effectiveLocationName`, nadpłatę z `overpaymentAmount`, stan pakietu z `package*`.
2. Rozliczenia trenerów:
   - Jedno wywołanie `GET /api/owner/trainer-settlements` zamiast pętli po trenerach.
   - Sumy bierz z `summary`, a błąd odpowiedzi pokazuj jako błąd, nie jako 0 zł.
   - Szczegóły trenera nadal pobieraj z `GET /api/trainers/{id}/settlement`.
3. Klienci:
   - Lista przez `GET /api/clients?page=…&…` z filtrami i sortowaniem serwerowym; licznik aktywnych bierz z `summary`.
   - Dropdowny klientów i trenerów przez `GET /api/owner/filter-options` z `search` i `limit`.
4. Historia sesji klienta i trenera: `GET /api/sessions/filter?clientId|trainerId=…&page=…`. Grafik zostaje bez zmian.
5. Zapisy grupowe:
   - Lista uczestników z jednego wywołania. Usuń dociąganie klientów i pakietów.
   - Przyciski steruj `allowedActions`.
   - W wyborze pakietu korzystaj z pól `offers[]` zamiast katalogu.
6. Wpłata personelu:
   - Generuj `Idempotency-Key` raz na intencję użytkownika.
   - Po timeoucie wywołaj `GET /api/owner/operations/{key}`: 200 oznacza gotowe, 404 oznacza ponowienie tym samym kluczem. Obsłuż też 409.

## 11. Kolejność integracji (frontendowy Codex)

1. `GET /api/billing/payments`: największy zysk. Podmień listę, filtry i kolumny lokalizacji, nadpłaty i stanu pakietu.
2. `GET /api/owner/trainer-settlements`: zastąp N+1 zapytań i podepnij `summary`.
3. `GET /api/owner/filter-options`: dropdowny używane w kolejnych ekranach.
4. `GET /api/clients?page=…`: lista klientów i `summary`.
5. `GET /api/sessions/filter?…&page=…`: historia w karcie klienta i trenera.
6. Zapisy grupowe: lista uczestników i `package-options`.
7. `Idempotency-Key` i `GET /api/owner/operations/{key}` w formularzu wpłaty.

Po każdym kroku sprawdź, że widok bez nowych parametrów (np. Grafik albo stare wywołania) działa jak wcześniej.
