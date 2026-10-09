# Panel pakietów i rozliczeń klienta — kontrakt dla frontendu

Zmiany backendu są lokalne. Nie wymagają migracji bazy. Wdrożenie musi objąć backend przed frontendem. Nie wykonano automatycznej naprawy istniejących danych. Przed produkcją należy uruchomić testy integracyjne na osobnej bazie i poniższe scenariusze UI.

## 1. Układ panelu

Na górze cztery informacje: „Łącznie do zapłaty”, „Saldo do wykorzystania”, „Bieżący pakiet indywidualny”, „Automatyczne przedłużanie”. Pakiety grupowe pokaż w osobnej sekcji, każdy z własnymi wejściami i rozliczeniem. Nie wybieraj ostatniego pakietu jako domyślnego pakietu całego klienta.

Owner: `GET /api/billing/clients/{clientId}`. Trener: `GET /api/trainer-portal/clients/{clientId}/billing`.

- `totalAmountDue` — nowe pole: suma dodatnich należności wszystkich pakietów, również nieaktywnych. Nie odejmuj samodzielnie `currentBalance`.
- `currentBalance` — saldo; nie jest sumą wpłat ani opłaceniem bieżącego pakietu.
- `activeClientPackageId` i dotychczasowe `activePackage*` — wyłącznie bieżący pakiet indywidualny/semipersonalny. Mogą być null/0 przy samych pakietach grupowych.
- `packages` — wszystkie pakiety; tabela i karty pokazują dane każdego pakietu. Suma zakłada obecną konfigurację kwot w PLN; nie dodajemy wyboru waluty.
- `payments` — historia wpłat, także cofniętych. Cofniętej wpłaty nie pokazuj jako ostatniej skutecznej wpłaty.

Karta pakietu: nazwa, lokalizacja, typ, użyte/łączne/pozostałe wejścia, cena, opłacono, do zapłaty, data zakupu, ważność, termin zapłaty. Rozdziel status pakietu (`isActive`, `closureDisposition`, wykorzystanie, ważność) od `paymentStatus` i od przedłużania.

Nowe `origin: "OpeningBalance"` oznacza importowany pakiet — pokaż oznaczenie „Stan początkowy”. `closureReason` zawiera powód zamknięcia. Nie interpretuj `isActive: false` jako spłaty lub umorzenia długu.

## 2. Subskrypcja i pakiety grupowe

`GET /api/clients/{clientId}/subscription` (owner) lub odpowiednik pod `/api/trainer-portal/clients/{clientId}/subscription`.

`currentCycle` obejmuje tylko indywidualne/semipersonalne. Wyłączenie `autoRenewEnabled` nie oznacza zakończenia bieżącego pakietu. `nextPackage: null` oznacza brak zaplanowanego następnego pakietu. Pakiety grupowe nie są wyborem w selektorze „Następny pakiet”. Ich wyczerpanie nie wyłącza pakietu indywidualnego klienta.

Dotychczasowe operacje `subscription/next-package`, `subscription/cancel`, `subscription/resume` pozostają. Grupowe przypisuj przez dodanie pakietu, nie przez subskrypcję.

## 3. Akcje ownera: najpierw podgląd

`GET /api/client-packages/clients/{clientId}/packages/{clientPackageId}/management-preview`

Odpowiedź zawiera `version`, `amountDue`, `remainingSessions`, `canDelete`, `canEdit`, `canCorrect`, `canClose`, `deleteBlockReason`, `blockers`.

Akcje finansowych korekt, importu, zamknięcia i zmiany są dostępne tylko ownerowi. Trener zachowuje istniejące operacje przypisania pakietu, sesji i wpłat. Nie pokazuj mu przycisków, których API nie pozwoli wykonać.

Powody blokad tłumacz na polski:

| Kod | Komunikat |
| --- | --- |
| PackageClosed | Pakiet został zamknięty. Historia pozostaje dostępna. |
| PaymentHistory | Pakiet ma historię wpłat. Użyj zamknięcia zamiast usuwania. |
| BalanceHistory | Pakiet ma operacje na saldzie. Nie można go usunąć. |
| UsedSessions | Z pakietu odliczono treningi. Nie można go usunąć. |
| OpeningBalance | Pakiet został zaimportowany. Zachowujemy jego historię. |
| PendingPayment | Najpierw rozstrzygnij oczekującą wpłatę lub płatność Tpay. |
| UnfinishedSessions | Najpierw odwołaj, zakończ lub przepnij niezakończone treningi powiązane z pakietem. |
| HasRenewal | Pakiet ma kolejny cykl. Wymaga rozliczenia zależnego pakietu. |

Nie wnioskuj o usuwaniu wyłącznie z `usedSessions == 0`. Odśwież podgląd po każdej operacji. Front nie zastępuje walidacji backendu.

## 4. Edycja i korekta

Jeśli `canEdit`, pokaż „Edytuj”. Jeśli tylko `canCorrect`, pokaż „Korekta pakietu” z obowiązkowym powodem i zestawieniem przed/po. Oba formularze zapisują:

`POST /api/client-packages/clients/{clientId}/packages/{clientPackageId}/correct`

```json
{
  "expectedVersion": "wartość version z podglądu",
  "reason": "Uzgodniona korekta ceny",
  "totalSessions": 10,
  "totalPrice": 900,
  "validUntil": "2026-12-31T22:59:59Z",
  "paymentDueDate": "2026-10-15T00:00:00Z"
}
```

Wysyłaj tylko zmienione pola. Null/pominięcie zachowuje wartość; obecna operacja nie kasuje dat. Daty wysyłaj z UTC `Z`. `totalPrice` oznacza należność za pakiet po zastosowaniu salda, nie cenę katalogową. Nie może spaść poniżej `amountPaid`. Liczba wejść nie może spaść poniżej wykorzystania; wyzerowanie pozostałych wejść aktywnego pakietu wymaga zamknięcia.

Nie edytujemy licznika wykorzystania tą operacją. Cofaj konkretny trening. Wycena już wykorzystanych sesji pozostaje historyczna; korekta nie przelicza dawnych treningów. Powód ma 1–1000 znaków.

## 5. Zakończenie albo zmiana pakietu

Formularz w trzech krokach: 1) podsumowanie starego pakietu, 2) decyzje o pieniądzach, 3) opcjonalny nowy pakiet i potwierdzenie. Pokaż, ile pozostaje długu, jaka kwota przechodzi na saldo lub czeka na zwrot. Nie wybieraj domyślnie umorzenia długu ani zwrotu.

`POST /api/client-packages/clients/{clientId}/packages/{clientPackageId}/close`

```json
{
  "expectedVersion": "wartość version z podglądu",
  "reason": "Zmiana pakietu na prośbę klienta",
  "debtDisposition": "WaiveDue",
  "fundsDisposition": "Balance",
  "settlementAmount": 200,
  "replacement": {
    "clientId": 134,
    "packageId": 56,
    "purchaseDate": "2026-10-02T10:00:00Z"
  }
}
```

- `KeepDue` — pozostały dług pozostaje do zapłaty. Zamknięty pakiet ma `ClosedWithDebt`; wpłaty można przypisywać jawnie do jego `clientPackageId`. Opłacenie nie uruchamia ponownie pakietu.
- `WaiveDue` — umorzenie niezapłaconej części; wpłaty i pierwotna cena pozostają w historii audytu.
- `KeepFunds` — brak operacji na wpłaconych środkach; `settlementAmount` musi wynosić 0.
- `Balance` — wskazana kwota trafia na saldo klienta (`TransferredToBalance`).
- `Refund` — zapisuje `RefundPending`; niczego nie wysyła do banku/Tpay. Użyj istniejącego potwierdzenia zwrotu dopiero po rzeczywistym zwrocie.
- Dla Balance/Refund wymagane jest WaiveDue i dodatnia kwota nieprzekraczająca `amountPaid`. Środki wcześniej zastosowane z salda nie są ponownie traktowane jako wpłata do zwrotu w tym formularzu.
- `replacement` jest opcjonalne. Musi dotyczyć tego samego klienta. Zamknięcie i utworzenie nowego pakietu to jedna transakcja: przy błędzie nowego pakietu stary nie zostaje zamknięty.
- Nowy pakiet korzysta z dostępnego salda zgodnie z istniejącą logiką. Nie twórz go drugim żądaniem, jeżeli użytkownik wybrał „Zmień pakiet”.
- Odpowiedź: `{ "replacementClientPackageId": 123 }` lub null.

Potwierdzenie zwrotu: `POST /api/clients/{clientId}/packages/{clientPackageId}/confirm-refund`, body `amount` i `reference` zgodne z decyzją. Historia zwrotów: `GET /api/clients/refunds?clientId=...`.

Zamknięcie pojedynczego pakietu nie kończy współpracy z całym klientem. Zachowaj odrębny istniejący proces „Zakończ współpracę”.

## 6. Import trwającego pakietu

Osobny przycisk ownera „Wprowadź trwający pakiet”. Nie używaj formularza dodania wpłaty do pieniędzy otrzymanych przed migracją.

`POST /api/client-packages/import`

```json
{
  "requestId": "2c3b2e60-e34d-467f-b9cf-7719e894fb39",
  "reason": "Stan na dzień rozpoczęcia pracy w CRM",
  "usedSessions": 4,
  "amountPaid": 1000,
  "package": {
    "clientId": 134,
    "packageId": 56,
    "totalSessions": 10,
    "totalPrice": 1000,
    "purchaseDate": "2026-09-15T10:00:00Z",
    "validUntil": "2026-12-15T10:00:00Z"
  }
}
```

RequestId generuj raz dla danego zatwierdzenia i zachowaj przy ponowieniu identycznego żądania. Powtórzenie nie tworzy drugiego pakietu. Zmiana danych z tym samym RequestId jest odrzucana. Odpowiedź: `{ "id": 123 }`.

Wykorzystanie: od 0 do `totalSessions - 1`; wcześniejsze opłacenie: od 0 do ceny. Import nie zużywa salda CRM i nie tworzy wpłaty ani sztucznych treningów. Kwota będzie widoczna w opłaceniu pakietu, a szczegóły importu w audycie. Nie będzie jej w historii nowych wpłat i przychodach CRM.

## 7. Cofanie, historia i blokady

- Cofnięcie wpłaty: istniejące `POST /api/billing/payments/{paymentId}/reverse` z `reason`. Nie usuwaj wiersza — pokaż „Cofnięta”.
- Cofnięcie wpłaty nie jest zwrotem bankowym. Tpay wymaga osobnego uzgodnienia; automatycznych zwrotów nie dodano.
- Jeżeli nadpłata została już wykorzystana i saldo nie wystarcza do jej cofnięcia, backend blokuje operację. Najpierw rozlicz pakiet, który wykorzystał saldo.
- Zamknięte pakiety nie pozwalają cofać starych wpłat. Nie proponuj obejścia przez ręczne wyzerowanie wartości.
- Trening poprawiaj istniejącą edycją sesji. Backend blokuje zmianę w wypłaconym rozliczeniu trenera; owner musi świadomie ponownie otworzyć rozliczenie.
- Przy cofnięciu ostatniego treningu backend sprawdza kolejny cykl. Jeśli był wykorzystany albo ma oczekującą płatność, wymaga najpierw rozliczenia zależności.
- Historia audytu ownera: `GET /api/clients/{clientId}/audit`. Korekty i zamknięcia zawierają przed/po oraz powód. Import ma akcję z prefiksem `PackageOpeningBalance:`.

## 8. Błędy i odświeżanie

Nowe akcje zwracają błąd walidacji 400 z `message`. Jeśli wersja się zmieniła, zachowaj wpisane dane, pobierz nowy podgląd i poproś o ponowne sprawdzenie; nie zatwierdzaj automatycznie na nowej wersji. Brak uprawnień obsłuż jako 401/403. Konflikty równoległych transakcji wymagają odświeżenia; nie pokazuj sukcesu po błędzie sieciowym.

Blokuj przycisk podczas żądania. Po sukcesie odśwież jednocześnie rozliczenie, pakiety, subskrypcję, saldo i audyt. Dla zamknięcia po przerwanym połączeniu sprawdź stan pakietu zamiast ślepo ponawiać akcję.

## 9. Scenariusze akceptacyjne przed live

1. Indywidualny + dwa grupowe: wszystkie należności w sumie, indywidualny pozostaje bieżącym cyklem.
2. Wyłącz przedłużanie: bieżący pakiet działa, następny znika.
3. Cofnij wpłatę nieużywanego pakietu: wpłata pozostaje w historii; usuwanie jest zablokowane, zamknięcie dostępne.
4. Zmień nieopłacony pakiet z WaiveDue: znika jego dług, nowy ma własną należność.
5. Zmień opłacony pakiet z Balance: saldo jest naliczone raz, następnie wykorzystane w nowym pakiecie.
6. Zamknij z KeepDue: dług zostaje, można go opłacić bez aktywacji pakietu.
7. Refund: status oczekujący aż do zewnętrznego potwierdzenia, bez pozornego przelewu.
8. Import 4/10 i opłacone 1000: pozostaje 6 wejść, brak nowej wpłaty; ponowienie z tym samym RequestId nie duplikuje danych.
9. Dwa otwarte okna: stare expectedVersion blokuje zatwierdzenie po zmianie pakietu.
10. Błąd przy tworzeniu replacement: stary pakiet i saldo pozostają bez zmian.
11. Oczekujący Tpay lub zaplanowane treningi: korekta/zamknięcie zablokowane z wyjaśnieniem.
12. Cofnięcie treningu przy wypłaconym trenerze, użytym kolejnym pakiecie oraz cofnięcie wykorzystanej nadpłaty: kontrolowana blokada, brak częściowych zmian.

Funkcja stawki grupowej trenera jest opisana osobno w `FRONT-trainer-group-rate.md`.
