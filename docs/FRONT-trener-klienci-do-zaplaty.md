# Portal trenera — kwota do zapłaty na liście klientów (2026-11-13)

Okrojona wersja tego, co ma lista klientów Ownera (`FRONT-klienci-kwota-do-zaplaty.md`). Dotyczy `GET /api/trainer-portal/clients/page`; istniejące parametry i pola są bez zmian (`currentBalance` zostaje).

## Nowe pola wiersza `TrainerPortalClientDto`

| Pole | Typ | Opis |
| --- | --- | --- |
| `totalAmountDue` | decimal ≥ 0 | Ta sama wartość co na liście Ownera i w `ClientBillingSummaryDto.totalAmountDue`: suma `max(0, totalPrice − amountPaid)` po wszystkich pakietach klienta, także nieaktywnych. Nadpłata jednego pakietu nie pomniejsza długu innego. |
| `amountDueCurrency` | string | Waluta pakietów z należnością; `"PLN"`, gdy klient nie ma należności. |

Pola są też w `GET /api/trainer-portal/clients` (pełna tablica) i w `recentClients` dashboardu trenera, bo korzystają z tego samego DTO.

**Co pokazać:** w kolumnie „Do zapłaty” wyświetlaj `totalAmountDue` (np. `0,00 zł` albo „—” przy 0). `currentBalance` to saldo rozliczeń (kredyt/dopłata z treningów), a nie należność za pakiety — nie używaj go jako „do zapłaty”.

## Nowy filtr

| Parametr | Wartości | Opis |
| --- | --- | --- |
| `hasAmountDue` | `true` / `false` | `true`: tylko klienci z `totalAmountDue > 0`. `false` lub brak: bez filtra. Działa przed stronicowaniem (`totalCount` po filtrze). |

## Nowe wartości `sort`

| Wartość | Kolejność |
| --- | --- |
| `amountDue` | `totalAmountDue` rosnąco, dogrywka `clientId` rosnąco |
| `amountDueDesc` | `totalAmountDue` malejąco, dogrywka `clientId` malejąco |

Pełna lista: `name` (domyślne), `nameDesc`, `created`, `createdDesc`, `balance`, `balanceDesc`, `packageUsage`, `packageUsageDesc`, `amountDue`, `amountDueDesc`. Nieznana wartość: 400. Portal nie ma parametru `order` ani sortowań `trainer`/`archivedAt` z listy Ownera.

## Uprawnienia

Bez zmian: tylko klienci przypisani do zalogowanego trenera, w jego organizacji, bez archiwalnych. Sortowanie i filtr nie ujawniają klientów innych trenerów.

## Przykład

```http
GET /api/trainer-portal/clients/page?page=1&pageSize=25&hasAmountDue=true&sort=amountDueDesc
```

```json
{
  "page": 1, "pageSize": 25, "totalCount": 2, "totalPages": 1,
  "items": [
    { "clientId": 41, "fullName": "Anna Big", "currentBalance": 0, "totalAmountDue": 700.00, "amountDueCurrency": "PLN", "...": "..." },
    { "clientId": 17, "fullName": "Jan Small", "currentBalance": -25.00, "totalAmountDue": 50.00, "amountDueCurrency": "PLN", "...": "..." }
  ]
}
```
