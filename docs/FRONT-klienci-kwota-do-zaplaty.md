# Lista klientów Ownera — kwota do zapłaty (2026-10-18)

Rozszerzenie `GET /api/clients`. Istniejące parametry, wartości domyślne i pola są bez zmian.

## Nowe pola wiersza `ClientDto`

| Pole | Typ | Opis |
| --- | --- | --- |
| `totalAmountDue` | decimal ≥ 0 | Ta sama wartość co `ClientBillingSummaryDto.totalAmountDue` (`GET /api/billing/clients/{id}`): suma `max(0, totalPrice − amountPaid)` po wszystkich pakietach klienta, także nieaktywnych. Nadpłata jednego pakietu nie pomniejsza długu innego. |
| `amountDueCurrency` | string | Waluta pakietów z należnością; `"PLN"`, gdy klient nie ma należności. |

Pola są w obu trybach: pełnej tablicy (bez `page`/`pageSize`) i stronicowanym (`ClientPageDto.items`).

## Nowy filtr (tryb stronicowany)

| Parametr | Wartości | Opis |
| --- | --- | --- |
| `hasAmountDue` | `true` / `false` | `true`: tylko klienci z `totalAmountDue > 0`. `false` lub brak: bez filtra. Łączy się z pozostałymi filtrami (AND). |

Filtr działa przed stronicowaniem: `totalCount`, `totalPages` i `summary` są liczone po filtrze.

## Nowe wartości `sort`

| Wartość | Kolejność |
| --- | --- |
| `amountDue` | `totalAmountDue` rosnąco, dogrywka `clientId` rosnąco |
| `amountDueDesc` | `totalAmountDue` malejąco, dogrywka `clientId` malejąco |

`order=asc|desc` działa jak dla innych wartości (`sort=amountDue&order=desc` = `amountDueDesc`).

## Uprawnienia

Bez zmian: lista zwraca tylko klientów, do których użytkownik ma dostęp w swojej organizacji (Owner — klienci organizacji, Trainer — własni klienci).

## Przykłady

```http
GET /api/clients?page=1&pageSize=25&hasAmountDue=true&sort=amountDueDesc
GET /api/clients?page=1&pageSize=25&locationId=3&hasAmountDue=true
```
