# Korekty zrealizowanych sesji - instrukcja dla frontu

## Cel

Zmiana zrealizowanej sesji jest korektą rozliczenia. Front nie cofa samodzielnie wejść z pakietu ani salda. Wysyła docelowy stan sesji, a backend atomowo:

- cofa poprzednie naliczenie,
- aktualizuje uczestników i status,
- ponownie nalicza wejścia, jeśli sesja nadal ma status `Completed`,
- aktualizuje wykorzystanie pakietów i korekty salda,
- zapisuje historię przed i po zmianie.

## Ponowne rozliczenie zrealizowanej sesji

`POST /api/sessions/{sessionId}/complete`

Endpoint przyjmuje pełną, docelową listę uczestników. Nie należy wysyłać tylko dodanej albo usuniętej osoby.

```json
{
  "actualSessionType": "TwoToOne",
  "correctionReason": "Dodano drugiego uczestnika po weryfikacji listy obecności",
  "participants": [
    {
      "clientId": 12,
      "attendanceStatus": "Present",
      "countsAgainstPackage": true,
      "sessionsCharged": 1,
      "note": null
    },
    {
      "clientId": 18,
      "attendanceStatus": "Present",
      "countsAgainstPackage": true,
      "sessionsCharged": 1,
      "note": null
    }
  ]
}
```

Ten sam endpoint służy do pierwszego zakończenia sesji i do późniejszej korekty. Pole `correctionReason` jest opcjonalne po stronie API dla zgodności, ale UI powinien wymagać go przy ponownym zapisie sesji ze statusem `Completed`.

## Cofnięcie statusu Completed

`PUT /api/sessions/{sessionId}`

W istniejącym body `UpdateSessionDto` należy wysłać cały wymagany stan sesji oraz:

```json
{
  "status": "Planned",
  "correctionReason": "Sesja omyłkowo oznaczona jako zrealizowana"
}
```

Możliwe statusy docelowe w tym scenariuszu:

- `Planned` - sesja wraca do grafiku, a wydarzenie Outlook jest synchronizowane ponownie,
- `Cancelled` - rozliczenie jest cofane, a wydarzenie Outlook jest usuwane,
- `Completed` - zwykła edycja danych bez zmiany listy rozliczonych osób.

Do zmiany uczestników zrealizowanej sesji należy używać endpointu `/complete`, nie zwykłego `PUT`.

## Historia korekt

`GET /api/sessions/{sessionId}/corrections`

Dostęp: `Owner`.

Odpowiedź jest uporządkowana od najnowszej korekty:

```json
[
  {
    "id": 41,
    "sessionId": 123,
    "originalSessionId": 123,
    "changeType": "SessionRecalculated",
    "reason": "Dodano drugiego uczestnika",
    "beforeState": {},
    "afterState": {},
    "changedByUserId": 7,
    "createdAt": "2026-09-13T14:30:00Z"
  }
]
```

Wartości `changeType`:

- `SessionRecalculated` - ponowne rozliczenie uczestników przez `/complete`,
- `SessionStatusCorrected` - cofnięcie `Completed` do innego statusu,
- `CompletedSessionUpdated` - edycja danych zrealizowanej sesji bez ponownego naliczania uczestników.

`beforeState` i `afterState` zawierają status, termin, trenera, lokalizację, typ sesji oraz rozliczenie każdego uczestnika. Pole `originalSessionId` pozostaje stałe także wtedy, gdy powiązanie z sesją zostanie kiedyś usunięte.

## Zachowanie pakietów

Jeżeli korygowana była ostatnia sesja pakietu:

- poprzedni pakiet zostaje ponownie aktywowany,
- automatycznie utworzony, ale jeszcze niewykorzystany kolejny cykl zostaje zachowany jako nieaktywny,
- po ponownym wykorzystaniu ostatniego wejścia ten sam kolejny cykl zostanie aktywowany, bez tworzenia duplikatu,
- opłaconego kolejnego cyklu backend nie usuwa i nie zeruje.

Backend zwróci `400 { "message": "..." }` i nie zapisze częściowej korekty, gdy automatyczne cofnięcie nie jest bezpieczne, np. gdy:

- kolejny pakiet został już użyty,
- kolejny pakiet ma oczekującą płatność,
- istnieje dalszy cykl odnowienia,
- klient ma inny aktywny pakiet wymagający ręcznego uzgodnienia,
- rozliczenie trenera za dany miesiąc zostało już oznaczone jako wypłacone.

Komunikat `message` należy pokazać użytkownikowi bez zamiany na ogólne "Nie udało się zapisać".

## Odświeżenie UI po sukcesie

Po udanej korekcie front powinien ponownie pobrać:

- szczegóły sesji i uczestników,
- aktywny pakiet oraz wykorzystanie pakietu każdego zmienionego klienta,
- saldo klienta,
- miesięczne rozliczenie trenera, jeśli ten widok jest otwarty,
- historię korekt, jeśli panel historii jest widoczny.

Nie należy lokalnie odejmować ani dodawać wejść. Odpowiedzi backendu są źródłem prawdy.

## Serie

Korekta rozliczenia dotyczy pojedynczego wystąpienia serii. Front nie powinien automatycznie przenosić zmiany statusu lub listy obecności na pozostałe zrealizowane wystąpienia. Edycja reguły serii i korekta rozliczenia konkretnej sesji to dwa osobne działania w UI.
