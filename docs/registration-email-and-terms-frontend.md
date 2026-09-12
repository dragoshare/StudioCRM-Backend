# Rejestracja, weryfikacja e-mail i regulaminy - integracja frontu

## Ustawienia firmy

Istniejace endpointy firm otrzymaly pola:

```json
{
  "termsVersion": "2026-09",
  "termsUrl": "https://studio.example/regulamin-2026-09.pdf",
  "termsPublishedAt": "2026-09-12T12:00:00Z"
}
```

Owner ustawia `termsVersion` i `termsUrl` przez:

```http
PUT /api/billing/payment-configuration/legal-entities/{id}
```

Oba pola musza byc ustawione razem, a URL musi korzystac z HTTPS. Przy publikacji
innego dokumentu nalezy zmienic rowniez wersje. `termsPublishedAt` ustawia backend.
Endpoint `PUT` zastępuje dane firmy, wiec front musi wyslac rowniez pozostale pola
formularza firmy.

Brak wersji i URL oznacza, ze regulamin nie jest jeszcze wymagany. Pozwala to
wdrozyc mechanizm przed otrzymaniem finalnych dokumentow od prawnika.

## Pobranie wymagan dla lokalizacji

```http
GET /api/public/group-classes/legal-requirements?locationId={id}
```

Endpoint jest publiczny. Dla zalogowanego klienta warto wyslac token, wtedy
`isAccepted` uwzglednia jego dotychczasowe zgody.

```json
{
  "locationId": 2,
  "legalEntityId": 4,
  "legalEntityName": "BS Workout Niepolomice",
  "acceptanceRequired": true,
  "isAccepted": false,
  "termsVersion": "2026-09",
  "termsUrl": "https://studio.example/regulamin-2026-09.pdf"
}
```

## Publiczna rejestracja

Przed pokazaniem checkboxa front pobiera wymagania dla wybranej lokalizacji.

```http
POST /api/public/group-classes/register
```

```json
{
  "email": "anna@example.com",
  "password": "...",
  "firstName": "Anna",
  "lastName": "Nowak",
  "phoneNumber": "+48123123123",
  "locationId": 2,
  "acceptTerms": true,
  "termsVersion": "2026-09"
}
```

Odpowiedz autoryzacyjna zawiera:

- `emailVerified` - czy adres jest potwierdzony,
- `emailVerificationRequired` - czy trzeba pokazac ekran oczekiwania na e-mail.

Publicznie zarejestrowane konto dostaje token logowania, ale do potwierdzenia
e-maila nie moze kupic pakietu, uruchomic checkoutu Tpay ani zapisac sie na
zajecia. Pozostale istniejace konta po migracji pozostaja zweryfikowane.

## Potwierdzenie adresu e-mail

Resend wysyla link na trase frontu:

```text
/verify-email?token={token}
```

Front odczytuje token i wysyla:

```http
POST /api/auth/verify-email
Content-Type: application/json

{ "token": "wartosc-z-linku" }
```

Po sukcesie nalezy odswiezyc `GET /api/auth/me`. Odpowiedz zawiera
`emailVerified: true`.

Ponowna wysylka:

```http
POST /api/auth/resend-email-verification
Content-Type: application/json

{ "email": "anna@example.com" }
```

Endpoint zawsze zwraca neutralna odpowiedz i nie ujawnia, czy konto istnieje.
Obowiazuje co najmniej minuta przerwy miedzy utworzeniem kolejnych linkow.

## Zakup w drugiej lokalizacji

Klient z Klaja moze korzystac z Niepolomic. Przed pierwszym zakupem front pobiera
`legal-requirements` dla lokalizacji pakietu. Gdy `acceptanceRequired=true` oraz
`isAccepted=false`, pokazuje dokument i checkbox, a nastepnie wysyla:

```http
POST /api/public/group-classes/legal-consents/me
Authorization: Bearer {token}
Content-Type: application/json

{
  "locationId": 2,
  "acceptTerms": true,
  "termsVersion": "2026-09"
}
```

Dopiero potem front tworzy zakup i checkout. Backend niezaleznie sprawdza zgode
ponownie, wiec nie da sie ominac kroku przez bezposrednie wywolanie API.

## Akceptacja zaproszenia

`GET /api/invitations/validate?token={token}` zwraca dodatkowo dane firmy oraz:

- `termsAcceptanceRequired`,
- `termsVersion`,
- `termsUrl`.

Gdy regulamin jest wymagany, `POST /api/invitations/accept` musi zawierac:

```json
{
  "token": "...",
  "firstName": "Anna",
  "lastName": "Nowak",
  "password": "...",
  "acceptTerms": true,
  "termsVersion": "2026-09"
}
```

Sam dostep do linku zaproszenia wyslanego na konkretny adres potwierdza e-mail,
dlatego po akceptacji zaproszenia nie ma drugiego maila weryfikacyjnego.

## Audyt ownera

```http
GET /api/clients/{clientId}/legal-consents
Authorization: Bearer {token ownera}
```

Endpoint zwraca firme, typ i wersje dokumentu, zachowany URL, zrodlo, date
akceptacji oraz `isCurrent`. Historyczne wpisy nie sa nadpisywane po publikacji
nowej wersji regulaminu.
