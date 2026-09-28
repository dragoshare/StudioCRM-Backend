# FRONT — obsługa cyklu życia klienta, kontrakt v2

Stan: 28.09.2026. Zmiany przygotowane lokalnie w backendzie; nie zostały wdrożone na serwer ani zastosowane do bazy. Ten dokument uzupełnia i w razie sprzeczności zastępuje część kliencką `FRONT-client-accounts.md`. Techniczne adresy klientów i dodatek Outlook pozostają wstrzymane.

## 1. Ekrany do zmiany

1. **Dodaj klienta**: kartoteka bez konta i bez wymaganego e-maila; zaproszenie do panelu jako osobna decyzja. Kontrakt tworzenia/zaproszeń w poprzedniej notatce pozostaje aktualny.
2. **Kartoteka klienta**: osobne dane kontaktowe, dostęp do panelu, lokalizacje, sesje, pakiety, historia zmian. Pole `loginEmail` jest tylko do odczytu. Edycja `email` zmienia kontakt, nie login.
3. **Zakończ współpracę**: owner sprawdza przeszkody i wybiera dla każdego aktywnego pakietu zachowanie wejść albo zwrot.
4. **Zwroty**: lista decyzji oczekujących i potwierdzonych; jawne potwierdzenie wykonania zwrotu poza CRM.
5. **Archiwum**: odczyt kartoteki, historii, wpłat i pakietów; przywrócenie klienta. Przycisk trwałego usunięcia wyłącznie dla pustej kartoteki.
6. **Powrót klienta**: przywrócenie kartoteki oraz odrębne wznowienie zachowanego pakietu z nową datą ważności.
7. **Wnioski o zmianę loginu**: lista ownera, akceptacja/odrzucenie, ekran potwierdzenia z linku e-mail.
8. **Dodawanie/edycja**: opcjonalne ostrzeżenie o potencjalnym duplikacie. Nie blokować rodzin korzystających ze wspólnego adresu lub telefonu.

W UI rozdzielić trzy operacje: zakończenie współpracy, archiwizację i blokadę panelu. Zamknięcie współpracy nie archiwizuje klienta i nie blokuje logowania. Archiwizacja blokuje logowanie i usuwa klienta z aktywnych list, ale zachowuje historię.

## 2. Role i dane klienta

Wszystkie nowe operacje zarządcze pod `/api/clients` są dla **Owner**. Trener nie może archiwizować klienta przez stary endpoint `/api/trainer-portal/clients/{id}/deactivate` — teraz wymaga on Owner.

Owner odczyta zarchiwizowanego klienta przez istniejące endpointy profilu, workspace, billing, subscription, training-plan i historii. Mutacje profilu, pakietów i płatności wymagają aktywnej kartoteki. Historia ukończonej sesji nadal pokazuje zarchiwizowanego uczestnika; lista aktywnych klientów go nie pokazuje. Raport wpłat nadal uwzględnia jego potwierdzone wpłaty.

`ClientDto` ma `userId`, `loginEmail`, `portalAccessStatus`, `isDeleted`. Statusy dostępu: `NoAccount`, `Invited`, `Active`, `Blocked`; dla archiwum zwracane jest `Blocked`. Lista w panelu trenera ma `userId` i `portalAccessStatus`. Status treningowy `Active/Inactive` nie jest statusem konta.

Trener musi pracować w głównej lokalizacji klienta. Przy zmianie lokalizacji należy wybrać właściwego trenera albo `trainerId: null`. Backend zachowuje wcześniejsze członkostwo lokalizacyjne do świadomego usunięcia; nie przenosi historycznych sesji ani pakietów.

## 3. Zakończenie współpracy — kolejność

### Podgląd

`GET /api/clients/{id}/closure-preview`

Przykładowy kształt odpowiedzi:

```json
{
  "clientId": 101,
  "blockers": [],
  "futureSessionIds": [],
  "balance": 0,
  "packages": [{
    "clientPackageId": 501,
    "name": "Pakiet 10 wejść",
    "requiresDecision": true,
    "remainingSessions": 7,
    "amountPaid": 1000,
    "amountDue": 0,
    "currency": "PLN",
    "disposition": null,
    "refundAmount": 0,
    "refundConfirmedAt": null
  }]
}
```

Podgląd zawiera również wcześniej zamknięte pakiety. Decyzję trzeba wysłać tylko dla pozycji `requiresDecision: true`, dokładnie raz na każdy aktywny pakiet. Używać **clientPackageId** konkretnego zakupionego cyklu, nie katalogowego `packageId`.

### Blokady

| Kod | Komunikat/akcja UI |
|---|---|
| `FutureSessions` | Klient ma przyszłe lub trwające sesje. Otwórz wskazane treningi. |
| `UnfinishedSessions` | Zakończ rozliczanie wcześniejszych treningów. |
| `PendingPayments` | Wyjaśnij płatności oczekujące na potwierdzenie. |
| `UnpaidPackages` | Klient ma nieopłacony w całości pakiet. |
| `OutstandingBalance` | Pozostało nierozliczone saldo korekt/nadpłat. |
| `PendingRefunds` | Potwierdź wykonanie uzgodnionego zwrotu. |
| `ActivePackages` | Występuje w `archive-check`; najpierw zamknij pakiety przez zakończenie współpracy. |

Nie zerować salda na froncie ani nie udawać wpłaty w celu przejścia blokady. Obecny zakres nie wprowadza umarzania należności ani wypłaty salda korekt. Takie przypadki wymagają osobnego rozliczenia; backend celowo blokuje zamknięcie przy niezerowym saldzie.

`futureSessionIds` służy do otwarcia istniejącego edytora sesji. Zamknięcie nie odwołuje automatycznie wspólnych treningów. Usunięcie jednego uczestnika i odwołanie całej sesji to różne decyzje.

### Zapis

`POST /api/clients/{id}/close-cooperation`

```json
{
  "reason": "Zakończenie współpracy na prośbę klienta",
  "packages": [
    { "clientPackageId": 501, "disposition": "Retain", "refundAmount": 0 },
    { "clientPackageId": 502, "disposition": "Refund", "refundAmount": 240 }
  ]
}
```

`reason`: wymagane 1–1000 znaków. Gdy brak aktywnych pakietów, wysłać pustą tablicę. `Retain` wymaga kwoty 0. `Refund` wymaga dodatniej kwoty z maksymalnie 2 miejscami dziesiętnymi, nie większej niż `amountPaid` tego cyklu. Kwotę ustala owner — backend nie wylicza automatycznie proporcji według liczby wejść. UI może podpowiedzieć kwotę, ale musi pozwolić ownerowi ją świadomie zatwierdzić.

Sukces: `200` z odświeżonym podglądem. Pakiety stają się nieaktywne, odnowienie zostaje wyłączone, następny pakiet wyczyszczony, status klienta `Inactive`. Zużyte wejścia, poprzednie wpłaty i historia pozostają. Stan decyzji: `Retained` albo `RefundPending`.

Wyłączyć przycisk w trakcie zapisu. Nie ponawiać automatycznie POST po utracie połączenia — najpierw odczytać podgląd. Jeśli stan zmienił się od otwarcia modala, odświeżyć i poprosić ownera o ponowny wybór.

## 4. Zwroty

`GET /api/clients/refunds?clientId=101&page=1&pageSize=25`

`clientId` opcjonalny; bez niego lista wszystkich decyzji zwrotu, w tym dla archiwum. Odpowiedź stronicowana z polami `clientId`, `clientPackageId`, `packageName`, `disposition`, `amount`, `currency`, `confirmedAt`, `reference`.

`POST /api/clients/{id}/packages/{clientPackageId}/confirm-refund`

```json
{ "amount": 240, "reference": "Przelew bankowy 28.09.2026, nr operacji XYZ" }
```

Kwota musi zgadzać się z decyzją. Numer/opis potwierdzenia jest wymagany, 1–1000 znaków. Sukces `204`, stan `Refunded`; ponowienie identycznego potwierdzenia jest bezpieczne i nie tworzy kolejnej operacji. Odmienna kwota lub referencja po potwierdzeniu jest odrzucana.

**CRM zapisuje potwierdzenie wykonania zwrotu poza systemem. Ten endpoint nie uruchamia Tpay ani przelewu.** Przycisk nazwać „Potwierdź wykonany zwrot”, nie „Wyślij pieniądze”. Nie oferować wznowienia pakietu `RefundPending/Refunded`.

Raport przychodów (`grossAmount`, `netAmount`) zachowuje dotychczasową semantykę wpłat i opłat operatora; nie jest wynikiem pomniejszonym o te zwroty. Zwroty pokazywać osobno. Nie nazywać istniejącego `netAmount` wynikiem „po zwrotach”. Nie sumować różnych walut. Lista ma paginację — pierwsza strona nie stanowi sumy wszystkich zwrotów.

## 5. Archiwizacja i powrót

Istniejący kontrakt:

- `GET /api/clients/{id}/archive-check` — `canArchive`, `blockers`.
- `POST /api/clients/{id}/archive` — archiwizacja (`204`).
- `GET /api/clients/archived` — lista archiwum.
- `POST /api/clients/{id}/restore` — przywrócenie (`204`).
- `PUT /api/clients/{id}/portal-access` z `{ "blocked": true }` — osobna blokada panelu.
- `DELETE /api/clients/{id}/permanent` — tylko zarchiwizowana pusta kartoteka bez konta i historii.

Archiwizacja i blokada panelu unieważniają refresh tokeny i oczekujące zmiany e-maila oraz anulują aktywne zaproszenia. Przywrócenie nie wznawia pakietów, odnowień ani anulowanych zaproszeń. Wcześniejsza jawna blokada panelu pozostaje.

Aby wykorzystać zachowane wejścia: najpierw przywrócić klienta, następnie:

`POST /api/clients/{id}/packages/{clientPackageId}/resume-retained`

```json
{ "validUntil": "2026-11-30T23:00:00Z", "reason": "Powrót klienta" }
```

Data przyszła w UTC jest wymagana. Sukces `204`. Liczba wykorzystanych wejść i wpłaty pozostają bez zmian. Odnowienie nadal jest wyłączone. Pakiet musi mieć zachowane wejścia, pasować do lokalizacji i nie kolidować z innym aktywnym pakietem indywidualnym. Decyzja wcześniejszego zamknięcia pozostaje w audycie.

W archiwalnym workspace pola `quickActions.canDeactivate/canChangeTrainer/canChangePackage/canAddPayment` są false. Ukryć edycję, tworzenie płatności i zaproszeń; udostępnić historię oraz przywrócenie.

Korekta starej sesji rozliczonej z zamkniętego pakietu albo pakietu zarchiwizowanego klienta jest blokowana. UI ma pokazać komunikat o konieczności rozliczenia tej sytuacji, nie próbować ponownie automatycznie.

## 6. Historia i audyt

`GET /api/clients/{id}/sessions?scope=All&page=1&pageSize=25`

Zakresy: `All`, `Upcoming`, `Past`, `Cancelled` (wielkość liter ma znaczenie). Daty UTC; pokazywać lokalnie. Zwraca `sessionId`, tytuł, daty, status, trenera i lokalizację, `attendanceStatus`, `isCountedFromPackage`, `clientPackageId`. Nie obejmuje sesji oznaczonych jako usunięte. `Past` obejmuje czas przeszły, nie jest równoznaczne z „rozliczona”.

`GET /api/clients/{id}/packages/history?page=1&pageSize=25`

Zwraca `ClientPackageBillingDto`: pełne pola cyklu oraz `closureDisposition`, `closedAt`, `refundAmount`, `refundConfirmedAt`, `refundReference`. Nie ograniczać historii do aktywnego pakietu.

Odpowiedniki dla trenera przypisanego do aktywnego klienta:

- `GET /api/trainer-portal/clients/{id}/sessions`
- `GET /api/trainer-portal/clients/{id}/packages/history`

`GET /api/clients/{id}/audit?page=1&pageSize=25` — Owner, również archiwum. Zwraca `id`, `action`, `actorUserId`, `beforeJson`, `afterJson`, `reason`, `createdAt`. Pola JSON są tekstem zawierającym obiekt. Renderować jako bezpieczny tekst lub sparsowane pola, nigdy jako HTML. Audyt obejmuje nowe operacje profilu, przypisania trenera/lokalizacji, blokady, archiwizacji, pakietu i e-maila; nie rekonstruuje zmian sprzed wdrożenia ani nie zastępuje istniejącej historii wpłat i sesji.

Wspólny format stron:

```json
{ "items": [], "totalCount": 0, "page": 1, "pageSize": 25, "totalPages": 0 }
```

`page` 1–100000, `pageSize` 1–100. Domyślnie 1/25.

## 7. Lokalizacje i duplikaty

`GET /api/clients/{id}/locations`

Lista: `locationId`, `name`, `isHomeLocation`, `groupAccessEnabled`. Główną lokalizację zmienia istniejący PATCH profilu; nie ten endpoint.

`PUT /api/clients/{id}/locations/{locationId}`

```json
{ "groupAccessEnabled": true, "reason": "Klient korzysta z zajęć w drugiej lokalizacji" }
```

Dodaje członkostwo albo aktualizuje dostęp grupowy, `204`. `false` wyłącza dostęp grupowy, ale nie usuwa członkostwa ani zapisanych sesji. Nie zmienia to dotychczasowej integracji Outlook; jej przebudowa pozostaje poza tym wydaniem.

`DELETE /api/clients/{id}/locations/{locationId}` — usuwa dodatkowe członkostwo (`204`). Nie usuwa sesji, pakietów ani wpłat. Głównej lokalizacji nie można tak usunąć. Aktywny pakiet lub przyszła/nierozliczona sesja w tej lokalizacji blokuje usunięcie. Samo wyłączenie dostępu grupowego nie odwołuje istniejących rezerwacji.

`GET /api/clients/duplicates?email=...&phone=...&name=...&excludeClientId=101`

Owner. Co najmniej jedno kryterium: niepusty e-mail, telefon minimum 6 znaków po usunięciu separatorów albo pełna nazwa minimum 3 znaki. E-mail i pełne imię z nazwiskiem są porównywane bez wielkości liter; telefon bez spacji, `+`, `-`, nawiasów i kropek (bez automatycznego dodawania kodu kraju). Wartości kodować jako parametry URL.

Maksymalnie 50 pasujących wyników: `clientId`, `fullName`, `isArchived`, `matches` (`Email`, `Phone`, `Name`). Jest to wskazówka, nie automatyczne łączenie kartotek. Przy duplikacie z archiwum proponować otwarcie i przywrócenie.

## 8. Zweryfikowana zmiana e-maila do logowania

1. Klient składa wniosek przez istniejący `POST /api/client-portal/email-change-requests` z `requestedEmail`.
2. Owner pobiera `GET /api/client-email-changes?clientId=101` (filtr opcjonalny). Rola Client widzi tylko własne wnioski. Odpowiedź: lista maksymalnie 200 ostatnich wpisów, nie strona; `id`, `clientId`, `currentEmail`, `requestedEmail`, `status`, `createdAt`, `verificationExpiresAt`, `reviewReason`.
3. Owner wysyła `POST /api/client-email-changes/{requestId}/review` z `{ "approve": true, "reason": "Potwierdzono z klientem" }` albo `approve: false`. Odpowiedź `204`. Akceptacja wysyła link na nowy adres i dopiero wtedy ustawia `AwaitingVerification`; login pozostaje stary do weryfikacji. Jeśli transport poczty zgłosi błąd, status to `DeliveryFailed`; ponowne zatwierdzenie wysyła nowy token.
4. Front musi obsłużyć publiczną stronę `/verify-email-change?requestId=123&token=...`. Pokazać przycisk „Potwierdź zmianę adresu” i dopiero po kliknięciu wykonać `POST /api/client-email-changes/verify` z `{ "requestId": 123, "token": "..." }`. Nie wykonywać mutacji samym GET — skanery linków nie powinny potwierdzać za klienta.
5. Sukces `200`: wyczyścić lokalną sesję i skierować do logowania nowym adresem. Backend zmienia `User.Email`, unieważnia stare refresh tokeny oraz linki resetu/weryfikacji. Dotychczasowe JWT przestają działać, ponieważ e-mail w tokenie nie odpowiada bieżącemu loginowi. `Client.Email` kontaktowy pozostaje bez zmian.
6. Klient może anulować własny wniosek przez `POST /api/client-email-changes/{requestId}/cancel` (`204`).

Statusy: `Pending`, `AwaitingVerification`, `Expired` (wyliczany przy odczycie), `DeliveryFailed`, `Rejected`, `Cancelled`, `Completed`. Token jest jednorazowy i ważny 24 godziny. Ponowne zatwierdzenie unieważnia wcześniejszy token. Tokenów nie logować i nie przesyłać do analityki; usunąć parametry z paska adresu po wykorzystaniu. Odrzucenie nie wysyła wiadomości. `reason` w recenzji opcjonalny, maks. 1000 znaków.

Backend potrzebuje poprawnego HTTPS `App:FrontendBaseUrl` oraz konfiguracji istniejącej usługi e-mail. Nie uruchamiać przycisku zatwierdzania, zanim publiczna strona weryfikacyjna nie będzie wdrożona.

## 9. Błędy i odświeżanie

Błędy biznesowe: `400` z `{ "message": "..." }`; brak uprawnień `403`; brak zasobu `404`; konflikt bazy `409`. Trener może dostać `404` zamiast danych klienta spoza swojego zakresu. Dla blokad archiwizacji korzystać przede wszystkim z kodów z podglądu.

Po zamknięciu, potwierdzeniu zwrotu, przywróceniu i wznowieniu odświeżyć profil, workspace, historię pakietów, podgląd zamknięcia i dostępne akcje. Przy braku konta i e-maila nie pokazywać aktywnego przycisku „Napisz e-mail”. Nie zmieniać po stronie frontu sald i zużycia wejść na podstawie samego kliknięcia.

## 10. Wdrożenie i weryfikacja

Nowa, rozdzielona migracja: `20260928181656_AddClientLifecycleClosureAndEmailVerification`. Zastępuje dwie wcześniejsze, niewdrożone migracje robocze. Nie zawiera adresów kalendarzowych ani tabel Outlooka. Migracja tylko dodaje pola i tabelę audytu; nie dokonują automatycznego zamykania klientów ani zwrotów. Nie zostały zastosowane w tej sesji.

Build i testy: 37 testów zaliczonych; 13 integracyjnych pominiętych z powodu braku `TPAY_TEST_DB`/skonfigurowanego `TestConnection`. Test zgodności modelu i snapshotu migracji przeszedł. Scenariusze PostgreSQL obejmują zachowanie/wznowienie wejść, zwrot i jego idempotencję, wspólną sesję, historię archiwum i przychody, lokalizacje, duplikaty, weryfikację e-maila i błąd wysyłki. Ich uruchomienie jest warunkiem zakończenia weryfikacji przed produkcją.

Testy integracyjne tworzą losowe schematy `crm_test_*` i sprzątają wyłącznie własne schematy. Wymagają testowej bazy PostgreSQL. Nie podłączać testów do produkcyjnej bazy. Nie wykonano rzeczywistych przelewów, wysyłek e-mail ani testów Microsoft Graph.

Minimalny test frontu po wdrożeniu backendu: klient bez e-maila → pakiet i wpłata testowa → zakończenie z zachowaniem → archiwizacja → odczyt historii → przywrócenie → wznowienie. Oddzielnie przetestować zwrot oczekujący/potwierdzony, błąd kwoty, wspólną sesję blokującą zamknięcie oraz zmianę loginu ze zużytym/wygasłym linkiem.

Poza tym zakresem pozostają: automatyczny zwrot operatora, umarzanie długu i wypłata salda korekt, scalanie duplikatów, zamrażanie pakietów, avatar/zgody dla kartoteki bez konta oraz nowy model pracy w Outlooku. Kod nie wymaga ich do uruchomienia podstawowej kartoteki klienta.
