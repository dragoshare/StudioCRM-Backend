# Kategorie powiadomień dla frontu

Każde `NotificationDto` ma nowe pole `category` (string). `type` pozostaje szczegółowym
typem zdarzenia, a `severity` określa jego ważność. Nie grupuj po tytule ani treści.
Kategorie są wyliczane z typu także dla historycznych powiadomień; nie ma migracji bazy.

| category | Etykieta | Obecne typy |
| --- | --- | --- |
| payments | Płatności | PaymentPendingConfirmation, PackagePaymentRequired, PackageEndedPaymentRequired |
| packages | Pakiety | ClientWithoutActivePackage, PackageEndingSoon, RenewalCancellationRequested |
| schedule | Grafik | SessionNotSyncedToOutlook |
| invitations | Zaproszenia | InvitationPending, InvitationExpired, ClientInvitationAccepted |
| trainers | Trenerzy | TrainerContractEndingSoon, TrainerContractNeedsRenewal, TrainerSettlementReminder |
| system | System | LocationLimitExceeded oraz nieznane typy |

Każde powiadomienie należy do jednej kategorii. Wezwanie do opłacenia zakończonego
pakietu trafia do `payments`; ostrzeżenie o kończących się wejściach do `packages`.
To klasyfikacja istniejących zdarzeń, nie dodanie nowych źródeł powiadomień.

## Endpointy

Wszystkie wymagają obecnego tokenu zalogowanego użytkownika. Lista, liczniki i zmiany
stanu dotyczą wyłącznie jego powiadomień, niezależnie od roli.

### Słownik kategorii

`GET /api/Notifications/categories`

Zwraca tablicę `{ "key": "payments", "label": "Płatności" }` dla sześciu kategorii.
Zakładkę „Wszystkie” dodaje front; jej wybór oznacza pominięcie parametru `category`.

### Lista

`GET /api/Notifications?limit=50&category=payments&isRead=false`

- `category`: opcjonalne; bez niego wszystkie kategorie.
- `isRead`: opcjonalne; `false` nieprzeczytane, `true` przeczytane, bez parametru oba stany.
- `limit`: dotychczasowe zachowanie, domyślnie 50, zakres 1–200; poza zakresem 50.
- Odpowiedź pozostaje tablicą NotificationDto, z dodatkowym `category`.
- Filtry działają w bazie przed limitem. Najpierw nieprzeczytane, potem najnowsze.
- Nieznana kategoria zwraca 400 z `message`; wielkość liter i skrajne spacje są normalizowane.

### Liczniki

`GET /api/Notifications/unread-count`

Przykładowa odpowiedź:

```json
{
  "unreadCount": 5,
  "unreadByCategory": {
    "payments": 2,
    "packages": 1,
    "schedule": 0,
    "invitations": 1,
    "trainers": 0,
    "system": 1
  }
}
```

Do liczb przy wszystkich zakładkach użyj jednego wywołania bez filtra. Liczniki nie są
ograniczone limitem listy. Opcjonalne `?category=payments` ogranicza zarówno sumę,
jak i liczniki do tej kategorii (pozostałe klucze mają 0).

### Oznaczanie jako przeczytane

`POST /api/Notifications/{id}/read` — bez zmian; zwraca DTO także z `category`.

`POST /api/Notifications/read-all?category=payments` — oznacza nieprzeczytane w wybranej kategorii.
Bez `category` zachowuje dotychczasowe działanie dla wszystkich kategorii.
Odpowiedź nadal ma postać `{ "markedAsRead": 2 }`. Nie wysyłaj body.

Po operacji odśwież listę i liczniki. Czytelny opis przycisku powinien odzwierciedlać
zakres: wybrana kategoria albo wszystkie powiadomienia.

## Zgodność i istniejące zachowanie

Dotychczasowe żądania bez nowych parametrów nadal działają. Nowe pola są dodatkiem.
Pozostaje obecna synchronizacja alertów operacyjnych ownera/trenera, w tym automatyczne
oznaczanie rozwiązanych alertów jako przeczytane. Sam filtr nie zmienia tej synchronizacji.
Uprawnienia, treści powiadomień i `actionUrl` pozostają bez zmian.
