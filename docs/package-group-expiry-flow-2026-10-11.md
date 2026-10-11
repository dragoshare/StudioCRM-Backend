# Pakiety, ważność i zapisy grupowe — 11.10.2026

## Zasady

- Grupa jest rozpoznawana po IsPubliclyBookable, PlannedSessionType=Group lub ActualSessionType=Group. Liczba obecnych nie zmienia grupy w 1:1/semi. Automat zachowuje Group także dla jednej osoby.
- Pakiet grupowy i indywidualny/semi nie są zamienne. Zabezpieczenie obejmuje ręczne wskazanie pakietu, zakończenie sesji, zakładanie pakietu ze zmienionym typem oraz dobór pakietu przy dodawaniu uczestników.
- Nowy zapis grupowy wymaga aktywnego, opłaconego, niezamkniętego pakietu grupowego ważnego na termin zajęć, właściwej lokalizacji i dostępnego wejścia. Uwzględniane są już zarezerwowane wejścia. countsAgainstPackage=false nie omija rezerwacji przy dodawaniu uczestnika.
- Ważność jest porównywana z StartAt sesji. Spóźnione rozliczenie treningu odbytego przed upływem ważności jest dozwolone. Nie można rozliczyć treningu odbytego po ValidUntil bez decyzji Ownera. Daty przekazujemy jako ISO 8601 UTC; ValidUntil oznacza konkretną chwilę, nie automatycznie koniec dnia.
- Wygaśnięcie z niewykorzystanymi wejściami samo nie tworzy kolejnego pakietu. Owner przedłuża albo kończy pakiet. Dotychczasowe odnowienie po wykorzystaniu wszystkich wejść pozostaje.
- Zgłoszenie oczekujące na płatność nie jest SessionParticipant, nie rezerwuje miejsca i nie zużywa wejścia. Po płatności wymaga osobnego potwierdzenia zapisu; brak wolnych miejsc nie unieważnia opłaconego pakietu.

## Decyzja o wygasłym pakiecie

Owner: `POST /api/client-packages/clients/{clientId}/packages/{clientPackageId}/expiry-decision`.

Najpierw pobierz istniejący `GET /api/client-packages/clients/{clientId}/packages/{clientPackageId}/management-preview` i jego `version`. Dla decyzji o wygaśnięciu nie używaj canCorrect/canClose jako jedynego warunku: dedykowana operacja dopuszcza przedłużenie także przy zaplanowanych sesjach. Sprawdza ważność, aktywność, zamknięcie i wersję.

```json
{"action":"Extend","validUntil":"2026-11-15T22:59:59Z","expectedVersion":"...","reason":"Decyzja Ownera"}
```

Albo:

```json
{"action":"End","expectedVersion":"...","reason":"Nie przedłużamy pozostałych wejść"}
```

Odpowiedź 200: `{ "replacementClientPackageId": null }` lub ID nowego pakietu.

Extend zmienia wyłącznie ważność i zapisuje audyt. End zamyka stary pakiet bez nadpisywania wykorzystania/wpłat/długu. Dla indywidualnego/semi tworzy kolejny tylko przy włączonym auto-odnowieniu i braku żądania jego anulowania, z NextPackageId albo dotychczasowej oferty. Grup nie odnawia automatycznie. Bez auto-odnowienia Owner może następnie użyć zwykłego tworzenia pakietu. Niewykorzystane wejścia nie są przenoszone, pieniądze nie są automatycznie zwracane.

400: pakiet nie jest aktywny/wygasły/otwarty, niepoprawna decyzja/data/powód lub niedostępny następny pakiet. 403: brak Ownera. 409: nieaktualna wersja. Całość jest transakcyjna.

## Owner dodaje osobę do grupy

1. Wyszukaj klienta z GroupAccessEnabled dla lokalizacji i wybierz ofertę pakietu grupowego. Samo zaznaczenie osoby nie może wywoływać płatnego zakupu bez pokazania oferty/ceny.
2. `POST /api/group-enrollments/sessions/{sessionId}` (Owner), body:

```json
{"clientId":66,"packageId":123}
```

Tworzy zgłoszenie i nieopłacony pakiet lub wykorzystuje istniejący pasujący pakiet. Nie modyfikuje indywidualnego/semi ani jego wejść/salda. Powtórzenie tej samej pary klient-zajęcia i oferty zwraca to samo zgłoszenie. Inna oferta dla istniejącego zgłoszenia zwraca 409. Klient bez konta może mieć zgłoszenie; płatność musi wtedy potwierdzić obsługa.

3. Odpowiedź `GroupEnrollmentDto`:

```json
{"id":1,"sessionId":607,"clientId":66,"clientPackageId":200,"status":"AwaitingPayment","amountDue":40.00,"reservesSeat":false}
```

Pola: id/sessionId/clientId/clientPackageId — int; status — string; amountDue — decimal; reservesSeat — bool. Nie ma automatycznej rezerwacji miejsca ani automatycznego pobrania pieniędzy. Korzystaj z istniejącej płatności dla otrzymanego clientPackageId. Samo zgłoszenie przelewu lub częściowa wpłata nie wystarczy: wymagany PaymentStatus=Paid.

4. `GET /api/group-enrollments/{id}` — Owner lub klient będący właścicielem zgłoszenia; obcy ID zwraca 404.
5. `GET /api/group-enrollments/sessions/{sessionId}?page=1&pageSize=25` — Owner, lista zgłoszeń dla zajęć. Page 1..100000, pageSize 1..100. PagedResultDto: items,totalCount,page,pageSize,totalPages. Dwa zapytania SQL, bez odczytu osobno dla każdego wpisu.
6. Po płatności `POST /api/group-enrollments/{id}/confirm` (Owner, bez body). Ponownie sprawdza pakiet, limit zapisów, termin zapisów oraz dostępność wejścia; dopiero wtedy tworzy uczestnika. Zwraca ten sam DTO ze statusem Booked. Klient może też skorzystać ze zwykłego publicznego zapisu po opłaceniu pakietu.

Statusy: AwaitingPayment, ReadyToBook, Booked, PackageUnavailable, Unavailable. ReadyToBook nie gwarantuje miejsca: ostateczne warunki sprawdza confirm w transakcji. ReservesSeat=true tylko gdy uczestnik rzeczywiście jest zapisany. Brak miejsc/ważności/płatności: 400; brak uprawnień: 403; obcy/nieistniejący wpis: 404; sprzeczny ponowny wybór pakietu: 409.

Dotychczasowy AddSessionParticipantDto dostał opcjonalne clientPackageId do wskazania konkretnego zakupionego pakietu. Bez tego pola backend dobiera kwalifikujący się pakiet. Stary frontend dodający klientów bez opłaconego pakietu otrzyma teraz 400 — należy podłączyć opisane etapy.

## Wdrożenie i potwierdzony przypadek

Nowa migracja: `20261011101040_AddGroupEnrollmentRequests`. Wymagana przed wdrożeniem tej wersji API. Nie została jeszcze zastosowana do działającej bazy.

Potwierdzono: klient 66, sesja 607, uczestnictwo 906. Grupa miała jedną osobę i ActualSessionType=OneToOne; pakiet indywidualny 166 obciążono wejściem oraz dopłatą 50 PLN. Na wyraźne polecenie Ownera skorygowano TestConnection przez usługę korekty: UsedSessions 2→1, dopłata usunięta, ActualSessionType=Group, CountsAgainstPackage=false. Udział pozostawiono do osobnego rozliczenia. SessionCorrections zachowuje historię, wykonawca wsparcia nie został przypisany do konta użytkownika.
