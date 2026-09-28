# FRONT — kartoteka klienta i zaproszenia

Kontrakt po rozdzieleniu Outlooka, 28.09.2026. Uzupełnienie: [cykl życia klienta v2](FRONT-client-lifecycle-v2.md). Ten zakres nie zmienia integracji Outlook ani powiadomień kalendarza.

## 2. Modal „Dodaj klienta”

Dwa tryby, domyślnie „Bez dostępu do panelu”.

### A. Bez dostępu do panelu

Pola: wymagane imię, nazwisko, lokalizacja; opcjonalny trener, telefon, email kontaktowy. Nie pokazywać hasła. Usunąć komunikat o automatycznym wysłaniu zaproszenia.

`POST /api/clients` — rola Owner.

```json
{
  "firstName": "Jan",
  "lastName": "Kowalski",
  "locationId": 1,
  "trainerId": null,
  "email": null,
  "phoneNumber": "500600700"
}
```

Odpowiedź: `201`, `ClientDto`. Nie wysyła maila ani nie tworzy konta. Email można pominąć albo przesłać `null`/pusty ciąg. Backend zapisuje brak maila jako `""`. Imię/nazwisko nie mogą być puste, email podany musi mieć poprawny format. Backend ustala `createdBy` z zalogowanego użytkownika, `source=StaffCreation`, a początkowy status treningowy to `Inactive`.

Przycisk: „Dodaj klienta”. Po sukcesie odświeżyć listę i otworzyć kartotekę.

### B. Zaproś do panelu — nowa osoba

Pozostaje istniejący tryb email + lokalizacja + opcjonalny trener. Kartoteka powstaje po przyjęciu zaproszenia, kiedy osoba poda imię i nazwisko.

`POST /api/invitations` — Owner lub Trainer, zgodnie z dotychczasowymi ograniczeniami przypisania trenera.

```json
{
  "email": "jan@example.com",
  "role": "Client",
  "locationId": 1,
  "trainerId": 2
}
```

Przycisk: „Wyślij zaproszenie”. Nie wywoływać równolegle `POST /api/clients` w tym wariancie.

Jeżeli email należy do istniejącej kartoteki, backend odrzuci zaproszenie bez `clientId`. UI powinien skierować do istniejącego klienta; nie tworzyć kolejnej kartoteki i nie scalać automatycznie po mailu. Dotyczy to również kartotek archiwalnych — najpierw przywrócenie przez Ownera.

Lista oczekujących zaproszeń może pozostać w tym trybie. Odpowiedź zaproszenia zawiera `lastSentAt`, `lastSendError`, `status`; utworzenie rekordu nie gwarantuje dostarczenia maila. Pokazać błąd wysyłki i istniejącą akcję ponowienia.

## 3. Zaproś istniejącego klienta

Na profilu klienta bez konta akcja „Zaproś do panelu”. Formularz: prawdziwy email, wstępnie uzupełniony kontaktem klienta, ale możliwy do wpisania, gdy go brak.

```http
POST /api/invitations
```

```json
{
  "clientId": 123,
  "email": "jan@example.com",
  "role": "Client"
}
```

Lokalizację i przypisanie trenera backend pobiera z kartoteki. Nie trzeba ich przesyłać. Odpowiedź `201 InvitationDto` zawiera nowe `clientId`.

Akceptacja przypisze `UserId` do klienta 123. Nie powstaje druga kartoteka, nie zmieniają się pakiety, sesje, przypisania ani zapisane imię i nazwisko. Jeżeli kontaktowy email był pusty, zostaje uzupełniony adresem zaproszenia. Istniejący niepusty email kontaktowy zostaje zachowany.

Warunki odmowy: istniejące konto, blokada dostępu, archiwalna kartoteka, aktywne zaproszenie do tego klienta, zajęty email konta, brak uprawnień.

`GET /api/invitations/validate?token=...` zwraca dodatkowo:

```json
{
  "clientId": 123,
  "firstName": "Jan",
  "lastName": "Kowalski"
}
```

Dla zaproszenia nowej osoby te trzy pola są `null`. Dla istniejącej kartoteki wypełnić nimi formularz akceptacji; nie sugerować, że ten formularz zmienia dane kartoteki. Dotychczasowy kontrakt akceptacji nadal wymaga imienia, nazwiska, hasła oraz wymaganych zgód.

Pozostają: `POST /api/invitations/{id}/resend`, `POST /api/invitations/{id}/cancel` i alias `DELETE /api/invitations/{id}`. Anulowanie zwraca `204`.

## 4. Stan konta na profilu/listach

Nowe pola `ClientDto` (także `workspace.profile` i lista archiwalna):

```json
{
  "userId": null,
  "portalAccessStatus": "NoAccount",
  "isArchived": false,
  "archivedAt": null,
  "email": "",
  "emailContactUrl": ""
}
```

| portalAccessStatus | Etykieta | Akcja |
|---|---|---|
| NoAccount | Bez dostępu do panelu | Zaproś do panelu |
| Invited | Zaproszony | Szczegóły / ponów / anuluj zaproszenie |
| Active | Konto aktywne | Zablokuj dostęp |
| Blocked | Dostęp zablokowany | Odblokuj dostęp |

Nie wyprowadzać tego stanu z `status`, `trainerId` ani `portalAccessMode`. `portalAccessMode=FullCrm/GroupOnly` nadal opisuje zakres funkcji panelu. Wygaśnięcie lub anulowanie zaproszenia przy braku konta daje `NoAccount`.

`isArchived` wyświetlać oddzielnie. Konto klienta archiwalnego nie może się uwierzytelnić, nawet jeśli pole `portalAccessStatus` opisuje istniejące konto jako `Active`. Przywrócenie archiwalnego klienta nie usuwa ręcznej blokady panelu.

Brak maila wyświetlać jako „Brak adresu email”. Nie tworzyć linku `mailto:` z pustego pola. Zmienić walidację formularza edycji `PATCH /api/clients/{id}` — mail jest opcjonalny. Ten PATCH nadal przyjmuje pełny model edycji, a nie dowolny fragment; przesyłać pozostałe aktualne pola, zwłaszcza lokalizację, trenera i nazwy.

Istniejący mechanizm awatarów jest powiązany z kontem `User`. Przy `userId=null` pokazywać inicjały i ukryć upload/usuwanie awatara; te operacje nie zostały rozszerzone na kartoteki bez konta.

## 5. Blokada panelu

`PUT /api/clients/{id}/portal-access` — Owner.

```json
{ "blocked": true }
```

`false` odblokowuje. Sukces `204`, nieznany klient `404`.

Blokada nie archiwizuje klienta, nie zmienia sesji i nie usuwa kontaktu Outlooka. Anuluje nieprzyjęte zaproszenia do tej kartoteki i unieważnia refresh tokeny. Kontrola JWT sprawdza stan klienta w bazie, więc wcześniej wydany access token również przestaje dawać dostęp. Front panelu klienta powinien obsłużyć `401` standardowym wylogowaniem, bez nieskończonego ponawiania odświeżenia.

Odblokowanie nie aktywuje konta wyłączonego odrębnie przez `User.IsActive=false`.

## 6. Archiwizacja

Lista bieżąca: dotychczasowe `GET /api/clients` i `/api/clients/filter`.

Lista archiwalna: `GET /api/clients/archived` (alias istniejącego `/api/clients/deleted`). Rola Owner. Filtrowanie/search na liście archiwalnej można zrobić lokalnie; endpoint nie przyjmuje parametrów filtra. Szczegółowy workspace nadal jest endpointem bieżącej kartoteki; lista archiwalna zwraca dane do przywrócenia.

Przed pokazaniem potwierdzenia:

```http
GET /api/clients/123/archive-check
```

```json
{
  "clientId": 123,
  "canArchive": false,
  "blockers": ["FutureSessions", "ActivePackages"]
}
```

| blocker | Komunikat w UI |
|---|---|
| FutureSessions | Klient ma przyszłe lub trwające sesje. Uporządkuj grafik. |
| UnfinishedSessions | Klient ma wcześniejsze sesje, które nie zostały zakończone ani anulowane. |
| ActivePackages | Klient ma aktywny pakiet. |
| PendingPayments | Są płatności oczekujące na potwierdzenie. |
| UnpaidPackages | Pozostały nieopłacone kwoty pakietów. |
| OutstandingBalance | Saldo klienta wymaga rozliczenia. |

Zatwierdzenie: `POST /api/clients/{id}/archive`, sukces `204`. Istniejący `/deactivate` pozostaje aliasem tej samej, teraz chronionej operacji. Backend ponownie sprawdza blokady; wynik wcześniejszego `archive-check` nie jest gwarancją sukcesu.

Przywrócenie: `POST /api/clients/{id}/restore`, sukces `204`. Historia zostaje. Anulowanych zaproszeń nie wznawiamy automatycznie.

Archiwizacja jest lokalną operacją CRM. Ten zakres nie dodaje automatycznego usuwania kontaktów Outlooka.

### Operacja zbiorcza

```http
POST /api/clients/archive-batch
```

```json
{ "clientIds": [123, 124] }
```

Maksymalnie 100 identyfikatorów. `200` z wynikiem per klient, np.:

```json
[
  { "clientId": 123, "archived": true, "error": null },
  { "clientId": 124, "archived": false, "error": "FutureSessions; ActivePackages" }
]
```

`NotFoundOrArchived` oznacza brak bieżącej kartoteki. Nie traktować `200` jako sukcesu wszystkich pozycji. Po odpowiedzi odświeżyć listę, a odrzucone pozycje zostawić z czytelnym wynikiem.

### Usunięcie trwałe

`DELETE /api/clients/{id}/permanent` — Owner, sukces `204`, brak `404`, niedozwolona operacja `400`.

Najpierw klient musi być archiwalny. Operacja dopuszcza wyłącznie kartotekę bez konta, zaproszeń, sesji, pakietów, płatności i pozostałych powiązań historycznych. Nie pokazywać obietnicy, że każdą kartotekę da się trwale usunąć.
