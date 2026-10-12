# Frontend: SuperAdmin i organizacje — stan na 12.10.2026

Branch backendu: `codex/platform-organizations`. Dokument opisuje zaimplementowany zakres, nie pełną wieloorganizacyjność. Zastępuje sprzeczne wcześniejsze ustalenia. Backend ma 206 poprawnych testów; migracje sprawdzono na lokalnej kopii. Wdrożenie na Render i migracja zdalnej bazy wymagają osobnego wykonania.

## Co frontend może teraz zbudować

1. Listę i szczegóły organizacji.
2. Formularz utworzenia organizacji testowej Atlas Demo.
3. Edycję nazwy, slugu i uiVariant.
4. Listę Ownerów/Trenerów wybranej organizacji.
5. Wyszukanie istniejącego konta i przypisanie go jako Ownera organizacji Draft.
6. Obsługę organizationId w logowaniu, odświeżaniu i Auth/me.

Nowa organizacja ma zawsze `Draft` i `crmAccessEnabled=false`. Utworzenie jej ani przypisanie Ownera nie uruchamia odrębnego CRM. Nie ma jeszcze endpointu aktywacji, zmiany organizacji, usuwania Ownera, przypisywania Trenera ani impersonacji.

BSworkout pozostaje `LegacyActive`. Jego kadra korzysta z dotychczasowego dostępu. Zmiana Ownerów BSworkout w SuperAdminie jest na razie blokowana odpowiedzią 409. Nie oferować tego przycisku jako działającej funkcji.

## Logowanie i organizacja

- POST `/api/auth/login`: dotychczasowy email/password; odpowiedź dodatkowo zawiera `organizationId`.
- POST `/api/auth/refresh`: dotychczasowy refreshToken; odpowiedź również zawiera `organizationId`. Rotacja musi zastąpić oba tokeny po stronie frontu.
- GET `/api/auth/me`: Bearer token; zawiera `role`, `roles` i `organizationId` obok dotychczasowych danych profilu.
- GET `/api/organization`: zwraca `organizationId`, `name`, `uiVariant`, `branding`. Nadal opisuje skonfigurowane BSworkout, a nie organizację wybraną przez SuperAdmina.

Dla Ownera/Trenera/Klienta BSworkout `organizationId = b5100000-0000-4000-8000-000000000002`. Dla SuperAdmina `organizationId = null`; oznacza panel platformy, nie błąd ani domyślny wybór BSworkout. SuperAdmin otrzymuje wyłącznie rolę platformową, nawet jeśli konto ma historycznie również rolę Owner.

Nie dodawać własnego nagłówka organizationId ani nie traktować ID z adresu panelu SuperAdmina jako zmiany kontekstu sesji CRM. Backend obecnie odrzuca tokeny innych studiów. `crmAccessEnabled` opisuje gotowość organizacji, nie uprawnienie SuperAdmina do zalogowania się jako Owner.

Przykładowy fragment odpowiedzi Auth/me:
```json
{"userId":123,"role":"SuperAdmin","roles":["SuperAdmin"],"organizationId":null}
```
To fragment, pozostałe dotychczasowe pola nadal są zwracane. Login/refresh zwracają pojedyncze `role`; listę `roles` należy pobierać z Auth/me.

## API panelu SuperAdmin

Wszystkie poniższe endpointy wymagają `Authorization: Bearer <token>` aktywnego SuperAdmina.

| Metoda | Endpoint | Zastosowanie |
|---|---|---|
| GET | `/api/super-admin/organizations?page=1&pageSize=25` | Lista organizacji |
| GET | `/api/super-admin/organizations/{organizationId}` | Szczegóły organizacji |
| POST | `/api/super-admin/organizations` | Nowa organizacja Draft |
| PATCH | `/api/super-admin/organizations/{organizationId}` | Dane organizacji |
| GET | `/api/super-admin/organizations/{organizationId}/users?role=Owner&page=1&pageSize=25` | Kadra; role można pominąć lub ustawić Owner/Trainer |
| GET | `/api/super-admin/users?query=anna&page=1&pageSize=25` | Globalne wyszukiwanie kont |
| PUT | `/api/super-admin/organizations/{organizationId}/owners/{userId}` | Dodanie Ownera do Draft |

Listy: `{items,total,page,pageSize}`; `page` od 1 do 100000, `pageSize` od 1 do 100. Wyszukiwarka kont: query 3–100 znaków. Lista organizacji nie ma parametru wyszukiwania po stronie serwera — filtr lokalny obejmuje tylko pobraną stronę.

### Utworzenie i edycja

POST body:
```json
{"name":"Atlas Demo","slug":"atlas-demo","uiVariant":"default","initialOwnerUserId":null,"reason":"Środowisko testowe"}
```
Odpowiedź 201 oraz nagłówek Location; body:
```json
{"organizationId":"<uuid>","name":"Atlas Demo","slug":"atlas-demo","uiVariant":"default","status":"Draft","revision":0,"crmAccessEnabled":false}
```
GET szczegółów, element listy, PATCH i PUT Ownera zwracają ten sam kształt podsumowania.

PATCH body (pełny formularz, nie JSON Patch):
```json
{"name":"Atlas Demo","slug":"atlas-demo","uiVariant":"default","expectedRevision":0,"reason":"Korekta danych"}
```
Wysyłać aktualne revision jako expectedRevision. Po sukcesie zastąpić dane odpowiedzią; po 409 odświeżyć rekord i pozwolić użytkownikowi ponowić edycję. Nie ponawiać automatycznie ze zmienioną rewizją.

Walidacja: name 1–100 znaków, slug do 100 znaków (małe litery, cyfry, pojedyncze myślniki), uiVariant `default` lub `bsworkout`, reason 1–1000 znaków, nie same spacje. Slug unikalny. Zachować fallback wyglądu dla starszego wariantu `standard`.

### Kadra i Ownerzy

GET users:
```json
{"items":[{"userId":123,"fullName":"Anna Nowak","email":"anna@example.test","roles":["Owner"],"accountStatus":"Active","membershipStatus":"Registered"}],"total":1,"page":1,"pageSize":25}
```
`accountStatus`: Active/Inactive. `membershipStatus`: Legacy dla BSworkout, Registered dla pozostałych. Registered nie oznacza dostępu do CRM organizacji Draft. Nie liczyć klientów na podstawie tej listy.

Wyszukiwarka kont zwraca `userId,fullName,email,accountStatus,emailVerified` w standardowej otoczce listy. Nie zwraca haseł, tokenów ani kont SuperAdmina.

PUT Owner body:
```json
{"expectedRevision":1,"reason":"Przypisanie Ownera do testów"}
```
Konto musi już istnieć, być aktywne, mieć potwierdzony email i nie mieć roli SuperAdmin. Ten endpoint nie tworzy konta i nie wysyła zaproszenia. Po sukcesie odświeżyć szczegóły organizacji i listę Ownerów. Nie dodaje globalnej roli Owner ani dostępu do BSworkout. Kolejny PUT tego samego Ownera z aktualną rewizją nie dubluje przypisania.

## Obsługa błędów i zakres ekranów

- 400: walidacja formularza/nieodpowiednie konto.
- 401: brak ważnej sesji; użyć istniejącej obsługi odświeżania/logowania.
- 403: brak uprawnień SuperAdmina.
- 404: organizacja nie istnieje.
- 409: konflikt revision, zajęty slug lub niedostępna zmiana Ownera w LegacyActive.

Ukryć lub oznaczyć jako niedostępne: aktywację/dezaktywację, „Zaloguj jako”, „Wejdź do CRM” dla Draft, odbieranie ról, przypisywanie trenerów, tworzenie/zapraszanie użytkowników i przegląd audytu. Nie dopisywać wymyślonych endpointów.

Branding docelowo edytuje Owner w ustawieniach studia. Istniejące endpointy brandingu SuperAdmina są zgodnościowe; nie rozbudowywać ich UI. Przeniesienie uprawnień do Ownera nie jest jeszcze zakończone. `uiVariant` pozostaje dostępny w danych organizacji; logo/kolory nadal pochodzą z API brandingu.

## Testy akceptacyjne frontu

1. SuperAdmin loguje się, ma null organizationId i widzi listę organizacji.
2. Owner BSworkout nie ma dostępu do endpointów SuperAdmina.
3. Utworzenie Atlas Demo daje Draft, revision 0 i brak możliwości wejścia do CRM.
4. Edycja zwiększa revision; zapis z nieaktualną revision daje 409.
5. Wyszukanie konta i przypisanie Ownera aktualizuje listę kadry Demo.
6. Przypisanie do Demo nie daje temu kontu dostępu do BSworkout.
7. Próba zmiany Ownera BSworkout jest niedostępna w UI; backend zwraca 409.
8. Dotychczasowe logowanie i odświeżanie sesji Ownera/Trenera/Klienta BSworkout pozostają sprawne.

## Render — wyłącznie backend testowy

Przełączyć testowy Web Service na `codex/platform-organizations`, pozostawiając backend produkcyjny na dotychczasowym wydaniu. Wybrana baza musi być bazą testową, nie produkcyjną: sprawdzić rzeczywistą wartość connection string, nie tylko jego nazwę.

Dla dotychczasowej konfiguracji testowej: `Database__ConnectionName=TestConnection` i odpowiedni `ConnectionStrings__TestConnection`. Kod wymaga migracji. Jeżeli stosowane są migracje przy starcie, `Database__ApplyMigrationsOnStartup=true`; alternatywnie wykonać je osobnym kontrolowanym krokiem. Przed migracją zachować aktualną kopię bazy testowej. Nie uruchamiać importu/resetu danych produkcyjnych.

Ostatnie migracje: AddOrganizationAdministration, AddOrganizationDataBoundaries, BindRefreshTokensToOrganization. EF zastosuje również brakujące wcześniejsze migracje brancha. Sprawdzić log poprawnego startu i migracji, następnie logowanie oraz powyższe testy. Front testowy powinien wskazywać testowy URL API. Atlas Demo tworzyć dopiero po udanym wdrożeniu w bazie testowej, przez POST powyżej.

Izolacja danych i logowanie wielu studiów nie są jeszcze gotowe do produkcyjnego uruchomienia drugiego studia. Pozostają przełączanie organizacji, obsługa członkostw podczas dodawania kadry/zaproszeń, profile per organizacja oraz kontekst integracji, zadań w tle i publicznych endpointów.

## Uzupełnienie: automatyczne członkostwa kadry

Dotychczasowe tworzenie trenera, ręczna rejestracja trenera oraz przyjęcie zaproszenia zapisują teraz także członkostwo BSworkout. Dodanie profilu trenera Ownerowi zachowuje rolę Ownera. Formularze i kontrakty API pozostają bez zmian. Zapis konta, profilu i członkostwa jest transakcyjny. Nie wymaga to nowej migracji ponad już wymienione. Ścieżki te nadal są przeznaczone wyłącznie dla BSworkout w trybie LegacyActive; nie uruchamiają kadry Atlas Demo.

## Nowe: lista organizacji własnego konta

`GET /api/auth/organizations?page=1&pageSize=25`, wymagany Bearer. Endpoint pobiera userId wyłącznie z sesji, nie przyjmuje userId innej osoby. Paginacja jak pozostałe listy: page 1..100000, pageSize 1..100. Niepoprawna paginacja daje 400.

Przykład odpowiedzi (ID Demo symboliczne):
```json
{
  "items": [
    {"organizationId":"b5100000-0000-4000-8000-000000000002","name":"BSworkout","uiVariant":"bsworkout","status":"LegacyActive","roles":["Owner"],"isCurrent":true,"canEnter":true,"unavailableReason":null},
    {"organizationId":"<uuid-demo>","name":"Atlas Demo","uiVariant":"default","status":"Draft","roles":["Trainer"],"isCurrent":false,"canEnter":false,"unavailableReason":"OrganizationNotActive"}
  ],
  "total":2,"page":1,"pageSize":25
}
```

Role pokazane dla Draft są zarejestrowanym członkostwem, nie aktywnymi uprawnieniami sesji. Organizacje bez członkostwa nie są ujawniane. BSworkout uwzględnia dotychczasowe globalne role w trybie LegacyActive, również dla klientów. SuperAdmin dostaje pustą listę osobistych organizacji i korzysta z `/api/super-admin/organizations`.

Frontend może zbudować widok „Moje studia”, pokazać bieżące studio i niedostępne przypisania. `canEnter=true` jest obecnie możliwe wyłącznie dla już bieżącego BSworkout. NIE oznacza istnienia endpointu przełączenia. `unavailableReason`: `OrganizationNotActive` dla Draft, `OrganizationSwitchingNotAvailable` dla pozostałych niedostępnych przypadków. W interfejsie tłumaczyć te kody na tekst; nie wyświetlać surowych nazw. Endpoint nie zmienia tokenów ani aktywnej organizacji i nie wymaga dodatkowej migracji.

Konto mające wyłącznie członkostwo Draft nie otrzymuje przez ten endpoint nowego sposobu logowania. Pełna sesja wyboru organizacji pozostaje do wdrożenia.