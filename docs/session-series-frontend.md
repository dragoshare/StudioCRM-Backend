# Serie treningow - integracja frontu

## Tworzenie serii

```http
POST /api/sessions/series
Authorization: Bearer {token ownera}
Content-Type: application/json
```

Endpoint tworzy osobna sesje CRM dla kazdego terminu i jedna prawdziwa serie
cykliczna w Outlooku. Dzieki temu kazde wystapienie ma wlasnych uczestnikow,
status, rozliczenie i rezerwacje, a Outlook nadal pokazuje operacje na pojedynczym
wydarzeniu lub calej serii.

Przyklad zajec grupowych co poniedzialek i srode, lacznie 12 terminow:

```json
{
  "session": {
    "title": "Kalistenika",
    "note": "",
    "startAt": "2026-09-14T18:00:00",
    "endAt": "2026-09-14T19:00:00",
    "trainerId": 3,
    "locationId": 1,
    "status": "Planned",
    "isPubliclyBookable": true,
    "publicSlug": "kalistenika",
    "publicCapacity": 10,
    "plannedSessionType": "Group",
    "outlookCategories": [],
    "participants": []
  },
  "recurrence": {
    "frequency": "Weekly",
    "interval": 1,
    "daysOfWeek": ["Monday", "Wednesday"],
    "occurrencesCount": 12,
    "endDate": null
  }
}
```

Dla treningu personalnego lub semi `session` jest taki sam jak w zwyklym
`POST /api/sessions`. Nalezy przekazac uczestnikow i odpowiedni
`plannedSessionType`, na przyklad `TwoToOne`.

## Ustawienia powtarzania

- `frequency`: `Daily` albo `Weekly`.
- `interval`: liczba od 1 do 12, np. `2` przy `Weekly` oznacza co dwa tygodnie.
- `daysOfWeek`: dla `Weekly` angielskie nazwy dni. Pusta lista oznacza dzien daty poczatkowej.
- `occurrencesCount`: od 2 do 104. Bez liczby i daty backend tworzy 12 terminow.
- `endDate`: opcjonalna data graniczna, liczona wlacznie.
- Gdy ustawiono liczbe i date, seria konczy sie po osiagnieciu pierwszego limitu.

Backend generuje godziny w strefie `Europe/Warsaw`, wiec przejscie miedzy czasem
letnim i zimowym nie przesuwa treningu o godzine.

## Odpowiedz

```json
{
  "recurringGroupId": "d3fb4d76070441cab6cf9d51cbddc148",
  "frequency": "Weekly",
  "interval": 1,
  "occurrencesCount": 12,
  "outlookSeriesSynced": true,
  "outlookSyncWarning": null,
  "sessions": []
}
```

Kazda sesja ma dodatkowo:

- `isRecurring`,
- `recurringGroupId`,
- `recurrenceInstanceNumber`.

Tworzenie nowej serii jest operacja typu wszystko albo nic. Odpowiedz sukcesu
oznacza, ze seria istnieje w CRM i zostala powiazana z Outlookiem. Jezeli
Outlook albo zapis powiazan zawiedzie, backend usuwa swiezo utworzone sesje CRM
i zwraca blad `400`. Front nie powinien dopisyac kafelkow lokalnie ani ponawiac
calego `POST /api/sessions/series` automatycznie.

Synchronizacje istniejacej serii mozna bezpiecznie ponowic:

```http
POST /api/sessions/series/{recurringGroupId}/sync-outlook
```

Body moze zawierac ten sam obiekt `recurrence`, ktory zostal uzyty przy tworzeniu
serii. Dla starszej serii mozna wyslac pusty body; backend odtworzy regule z
istniejacych terminow CRM.
Endpoint nie tworzy kolejnych sesji CRM. Jesli cala seria jest juz polaczona,
zwraca sukces bez duplikowania wydarzenia. Front powinien pokazac zwrocone
`outlookSyncWarning`, gdy `outlookSeriesSynced` nadal ma wartosc `false`.
Ta akcja sluzy do naprawy starszych serii lub pozniejszych rozjazdow. Nie jest
sciezka awaryjna po nieudanym tworzeniu nowej serii.

## Usuwanie calej blednej serii

```http
DELETE /api/sessions/series/{recurringGroupId}
```

Endpoint usuwa wszystkie wystapienia wskazanej serii z CRM oraz powiazana serie
Outlook. Nie wolno wywolywac osobnego `DELETE /api/sessions/{id}` dla kazdego
wystapienia. Backend blokuje usuniecie calej serii, jesli zawiera sesje
zrealizowane albo naliczone z pakietu.

```json
{
  "recurringGroupId": "d3fb4d76070441cab6cf9d51cbddc148",
  "deletedSessionsCount": 12,
  "outlookSeriesDeleted": true
}
```

Przed usunieciem front powinien pokazac potwierdzenie z liczba wystapien i nazwa
serii. Po sukcesie nalezy odswiezyc kalendarz, a nie usuwac kafelki tylko lokalnie.

## Widocznosc i blokada sali

Zajecia grupowe w Outlooku otrzymuja:

- tytul `ZAJECIA GRUPOWE: {nazwa} | {lokalizacja}`,
- kategorie `Zajecia grupowe` z wyrozniajacym kolorem,
- status dostepnosci `busy`,
- lokalizacje dodana jako zasob kalendarza.

Backend blokuje utworzenie zajec grupowych, jesli w tej lokalizacji trwa juz
jakakolwiek sesja. Blokuje tez zwykly trening nakladajacy sie na zajecia grupowe.
Front powinien wyswietlic komunikat `400` zwrocony przez API i pozostawic dane
formularza do poprawy.

Usuniecie jednego wystapienia w Outlooku anuluje jedna sesje CRM. Usuniecie calej
serii anuluje wszystkie niezrealizowane wystapienia powiazane z jej masterem.
