# Listy klientów (portal trenera) i wpłat — kontrakt dla frontu (2026-10-15)

Zmiany są wyłącznie rozszerzeniem: istniejące parametry, wartości domyślne, pola odpowiedzi i koperta `PagedResultDto` są bez zmian. Wszystkie parametry są w query string; nazwy nie rozróżniają wielkości liter (`sort` = `Sort`).

## Enumy w JSON

JSON zwraca enumy jako **liczby** (bez zmian). W query można podać liczbę (zalecane) lub nazwę. Swagger (`/swagger`) opisuje teraz w schemacie każdego enuma mapowanie `liczba = nazwa` (`description` oraz `x-enum-varnames`).

Wyjątek: `PackageBillingMode` (np. `activePackages[].billingMode`) ma własny konwerter i jest serializowany jako **tekst**: `"SessionBased"` lub `"PeriodBased"` (bez zmian). Swagger opisuje go teraz jako `string` z tymi wartościami.

| Enum | Wartości |
| --- | --- |
| `PaymentMethod` (`method`) | 1 = Blik, 2 = BankTransfer, 3 = Cash, 4 = PaymentGateway |
| `ClientPaymentStatus` (`status`) | 1 = PendingConfirmation, 2 = Confirmed, 3 = Rejected, 4 = Cancelled, 5 = Reversed |
| `ClientPaymentSource` (`source`) | 1 = ClientRequest, 2 = StaffEntry, 3 = PaymentGateway |
| `ReceiptStatus` (`receiptStatus`) | 0 = None, 1 = Issued, 2 = Cancelled, 3 = Pending, 4 = ManualRequired, 5 = Failed |

## 1. Klienci trenera — `GET /api/trainer-portal/clients/page`

Role: Owner, Trainer. Zwraca **wyłącznie klientów przypisanych do profilu trenera zalogowanego użytkownika** (`Client.TrainerId`), w jego organizacji; dotyczy to również Ownera, który ma profil trenera. Lista wszystkich klientów organizacji dla Ownera to `GET /api/clients`. Klienci zarchiwizowani (usunięci) nie są zwracani.

`GET /api/trainer-portal/clients` (bez `/page`) pozostaje bez zmian: pełna tablica klientów trenera bez parametrów, sortowana po `createdAt` malejąco. Filtry i sortowanie opisane niżej działają tylko w `/clients/page`.

| Parametr | Typ / wartości | Domyślnie | Uwagi |
| --- | --- | --- | --- |
| `page` | int 1..100000 | 1 | poza zakresem: 400 |
| `pageSize` | int 1..100 | 25 | poza zakresem: 400 |
| `search` | string ≤100 | — | imię+nazwisko lub email, bez rozróżniania wielkości liter |
| `locationId` | int | — | główna lokalizacja klienta (`locationId` w wierszu) |
| `status` | `New`, `Active`, `Inactive` | — | dokładne dopasowanie (wielkość liter ma znaczenie), max 30 znaków; nieznana wartość daje pustą listę |
| `hasActivePackage` | `true` / `false` | — (wszyscy) | **nowy**; `true` = klient ma co najmniej jeden aktywny, niezamknięty pakiet (dokładnie te, które trafiają do `activePackages`, także okresowy i grupowy); `false` = `activePackages` jest puste |
| `sort` | patrz niżej | `name` | nieznana wartość: 400 |

`status=Active` to nie to samo co `hasActivePackage=true`: `Client.Status` jest zapisanym stanem klienta, a `hasActivePackage` liczy się z bieżących pakietów. Do filtrów „z aktywnym pakietem / bez aktywnego pakietu” używaj `hasActivePackage`.

### Sortowanie (`sort`)

Każde sortowanie ma dogrywkę po `clientId`, więc stronicowanie jest stabilne. Sortowanie i liczniki wykonuje jedno zapytanie SQL na stronę (bez zapytań per wiersz).

| Wartość | Kolejność |
| --- | --- |
| `name` (domyślne) | nazwisko, imię rosnąco, `clientId` |
| `nameDesc` | nazwisko, imię malejąco, `clientId` |
| `created` | `createdAt` rosnąco, `clientId` |
| `createdDesc` | `createdAt` malejąco, `clientId` malejąco |
| `balance` | `currentBalance` rosnąco (najpierw dopłaty), `clientId` |
| `balanceDesc` | `currentBalance` malejąco (najpierw nadpłaty), `clientId` |
| `packageUsage` | wykorzystanie rosnąco, `clientId` |
| `packageUsageDesc` | wykorzystanie malejąco, `clientId` |

- `currentBalance` jest liczone tak samo jak w polu wiersza: suma transakcji salda bez `PaymentCredit` i `PaymentReversal`. Dodatnie = kredyt klienta, ujemne = dopłata.
- Wykorzystanie = `activePackageUsedSessions / activePackageTotalSessions`, czyli pakiet osobisty lub semi pokazywany w płaskich polach `activePackage*`. Klienci bez takiego pakietu (brak pakietu, tylko okresowy albo tylko grupowy) są **na końcu w obu kierunkach**. Pakiet z `totalSessions = 0` ma wykorzystanie 0.
- Sortowanie „trener” nie jest dostępne: w tym endpoincie wszyscy klienci mają tego samego trenera (zalogowanego), więc nie zmieniałoby kolejności. Sortowanie po trenerze ma sens na liście Ownera (`/api/clients`), która nie ma dziś parametrów sortowania.

### Przykłady

```http
GET /api/trainer-portal/clients/page?page=1&pageSize=25
GET /api/trainer-portal/clients/page?hasActivePackage=false&sort=name
GET /api/trainer-portal/clients/page?status=Active&hasActivePackage=true&sort=packageUsageDesc
GET /api/trainer-portal/clients/page?search=kowal&locationId=2&sort=balance&page=2&pageSize=50
```

Odpowiedź (bez zmian): `{ "page": 1, "pageSize": 25, "totalCount": 132, "totalPages": 6, "items": [TrainerPortalClientDto] }`.

## 2. Wpłaty — `GET /api/billing/payments`

Rola: Owner; wszystkie wpłaty organizacji. Te same parametry (wspólny `ClientPaymentFilterDto`) działają też w `GET /api/billing/clients/{clientId}/payments`, `GET /api/trainer-portal/clients/{id}/payments` i `GET /api/trainer-portal/payments/pending/page` (tam trener widzi tylko swoich klientów).

| Parametr | Typ / wartości | Domyślnie | Uwagi |
| --- | --- | --- | --- |
| `page` | int | 1 | wartość spoza 1..100000 jest przycinana (bez 400, jak dotąd) |
| `pageSize` | int | 25 | wartość spoza 1..100 jest przycinana |
| `search` | string ≤100 | — | imię+nazwisko lub email klienta |
| `clientId`, `locationId`, `legalEntityId` | int | — | |
| `method` | `PaymentMethod` 1..4 | — | **nowy**; nieznana liczba: 400 |
| `status` | `ClientPaymentStatus` 1..5 | — | |
| `source` | `ClientPaymentSource` 1..3 | — | |
| `receiptStatus` | `ReceiptStatus` 0..5 | — | |
| `paymentProvider` | string | — | dokładne dopasowanie |
| `from`, `to` | ISO 8601 | — | zakres **daty wpłaty** (`paymentDate`), oba włącznie; bez strefy = UTC; `from > to`: 400 |
| `amountMin`, `amountMax` | decimal ≥ 0 | — | zakres kwoty brutto (`amount`), oba włącznie; wartość ujemna lub `amountMin > amountMax`: 400 |
| `hasOverpayment` | bool | — | `balanceCreditAmount > 0` |
| `sort` | patrz niżej | `paymentDateDesc` | **nowy**; nieznana wartość: 400 |

| `sort` | Kolejność |
| --- | --- |
| `paymentDateDesc` (domyślne, jak dotąd) | `paymentDate` malejąco, `id` malejąco |
| `paymentDate` | `paymentDate` rosnąco, `id` rosnąco |
| `amountDesc` | `amount` malejąco, potem `paymentDate` i `id` malejąco |
| `amount` | `amount` rosnąco, potem `paymentDate` i `id` malejąco |

Sortowanie i filtr dat używają daty wpłaty (`paymentDate`, korygowanej przez `PATCH .../payments/{id}/date`), nie daty utworzenia rekordu (`createdAt`).

Błędy walidacji: 400. Dla walidacji modelu (zakres, `sort`, `method`) odpowiedź ma format `ValidationProblemDetails` (`errors` per pole), np. `errors.AmountMin = ["AmountMin cannot be greater than AmountMax."]`.

### Przykłady

```http
GET /api/billing/payments?method=3&sort=amountDesc
GET /api/billing/payments?amountMin=100&amountMax=500&from=2026-10-01T00:00:00Z&to=2026-10-31T23:59:59Z
GET /api/billing/payments?status=1&method=2&sort=paymentDate&page=1&pageSize=50
GET /api/billing/clients/42/payments?sort=amount
```

## Swagger

`/swagger` zawiera dla tych endpointów: opis operacji z domyślnym sortowaniem i limitami paginacji, opisy parametrów, listę dozwolonych wartości `sort` (`enum` + `default`), przykładowe wartości parametrów oraz mapowanie liczb na nazwy we wszystkich enumach. Format odpowiedzi JSON nie zmienił się.
