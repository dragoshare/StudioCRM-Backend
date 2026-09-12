# Powiadomienia zajec grupowych - integracja frontu

Modul korzysta z istniejacych endpointow powiadomien. Front nie tworzy
powiadomien i nie wylicza ich kategorii samodzielnie.

## Endpointy

```http
GET  /api/Notifications?limit=50&category={key}&isRead={true|false}
GET  /api/Notifications/unread-count?category={key}
GET  /api/Notifications/categories
POST /api/Notifications/{id}/read
POST /api/Notifications/read-all?category={key}
```

Kategorie zawsze nalezy pobierac z `GET /api/Notifications/categories`.
Doszly:

- `group_classes` - zapisy, rezygnacje, zmiany i przypomnienia,
- `registrations` - publiczna rejestracja klienta.

Platnosci za pakiety grupowe pozostaja w `payments`, a aktywacja darmowego
pakietu w `packages`.

## Typy zdarzen

| type | category | odbiorca |
| --- | --- | --- |
| `PublicGroupClientRegistered` | `registrations` | nowy klient i owner |
| `GroupPackagePaymentRequired` | `payments` | klient |
| `GroupPackagePaymentConfirmed` | `payments` | klient i owner |
| `GroupPackageActivated` | `packages` | klient |
| `GroupClassBooked` | `group_classes` | klient i trener |
| `GroupClassBookingCancelled` | `group_classes` | klient i trener |
| `GroupClassCancelled` | `group_classes` | zapisani klienci i trener |
| `GroupClassRescheduled` | `group_classes` | zapisani klienci i trener |
| `GroupClassReminder` | `group_classes` | klient, do 24 godzin przed zajeciami |

## Renderowanie

Front powinien korzystac bezposrednio z pol odpowiedzi:

- `category` wybiera zakladke i styl ikony,
- `type` pozwala rozroznic zdarzenia w ramach kategorii,
- `severity` przyjmuje `Info`, `Warning` lub `Critical`,
- `isRead` steruje stanem przeczytania,
- `actionUrl` jest docelowa trasa po kliknieciu,
- `relatedEntityType` i `relatedEntityId` identyfikuja obiekt domenowy.

Nie nalezy budowac `actionUrl` na froncie. Gdy pole jest ustawione, front
nawiguje pod zwrocona wartosc. Liczniki trzeba odswiezyc po oznaczeniu jednego
lub wszystkich powiadomien jako przeczytane.

Powiadomienia sa idempotentne. Ponowiony webhook Tpay albo ponowiona operacja
nie tworzy drugiego wpisu dla tego samego odbiorcy i zdarzenia.
