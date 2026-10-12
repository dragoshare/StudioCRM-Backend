# Wybór klienta i pakietu do grupy — kontrakt 12.10.2026

## 1. Klienci z dostępem grupowym

Owner, uwierzytelnienie Bearer:

`GET /api/clients/filter?locationId=2&groupAccessEnabled=true&search=anna`

Parametry istniejące: `trainerId: int?`, `status: string?`, `search: string?`, `locationId: int?`. Nowy: `groupAccessEnabled: bool?`.

Gdy podano `groupAccessEnabled`, wymagany jest dodatni `locationId`. Filtr sprawdza wpis ClientLocationMembership w tej lokalizacji; nie wymaga, aby była ona główną lokalizacją klienta. `false` wybiera istniejące przypisania z wyłączonym dostępem, nie klientów bez przypisania. Bez tego parametru dotychczasowe znaczenie locationId pozostaje: lokalizacja główna.

Odpowiedź `200`: dotychczasowa tablica `ClientDto[]`, bez zmiany jej kontraktu. Do wyboru klienta użyj `id`, `firstName`, `lastName`, `fullName`. Wyszukiwanie obejmuje imię, nazwisko i email, bez rozróżniania wielkości liter. To istniejący endpoint bez paginacji; nie przekazuj page/pageSize oczekując ograniczenia wyników. Zarchiwizowani klienci są wykluczeni; status klienta można filtrować osobno. Zapis ponownie sprawdza dostęp i blokadę klienta.

## 2. Oferty i posiadane pakiety

Owner, Bearer:

`GET /api/group-enrollments/sessions/{sessionId}/package-options?clientId=70`

Wymaga przyszłej sesji grupowej ze statusem Planned, istniejącego niezablokowanego klienta i włączonego dostępu grupowego w lokalizacji tej sesji. Odczyt niczego nie tworzy ani nie rezerwuje.

Pełny DTO odpowiedzi 200 (nazwy JSON camelCase):

```ts
type GroupPackageOptionsDto = {
  sessionId: number;
  clientId: number;
  locationId: number;
  sessionStartAt: string; // ISO 8601 UTC
  offers: GroupPackageOfferDto[];
  ownedPackages: OwnedGroupPackageDto[];
};
type GroupPackageOfferDto = {
  packageId: number;
  name: string;
  price: number; // cena całego nowego pakietu
  currency: string;
  sessionsLimit: number;
  durationDays: number;
  canSelect: boolean;
  reasons: string[];
};
type OwnedGroupPackageDto = {
  clientPackageId: number; // konkretny pakiet klienta
  packageId: number; // powiązana oferta
  name: string;
  price: number; // zapisana cena całego posiadanego pakietu
  currency: string;
  amountDue: number; // max(0, totalPrice - amountPaid)
  locationId: number | null; // null = dowolna lokalizacja
  validUntil: string | null; // ISO 8601 UTC, null = bez terminu
  paymentStatus: "Unpaid" | "Paid" | "Overdue" | "PartiallyPaid" | "PendingConfirmation";
  isActive: boolean;
  totalEntries: number;
  usedEntries: number;
  reservedEntries: number;
  availableEntries: number;
  isEligible: boolean;
  canSelect: boolean;
  reasons: string[];
};
```

`offers` zawiera aktywne, nieusunięte oferty grupowe dla tej lokalizacji lub bez ograniczenia lokalizacji, z poprawną konfiguracją ceny/liczby wejść/ważności. Nie jest filtrowane po IsPubliclyAvailable: to panel Ownera. `ownedPackages` zawiera posiadane pakiety grupowe klienta, także niekwalifikujące się (np. wygasłe, inna lokalizacja). Pakietów indywidualnych/semi nie zwracamy. Listy nie są paginowane.

Połącz listy po `packageId`, żeby pokazać, które oferty klient już posiada. Nie utożsamiaj `packageId` z `clientPackageId`. Nazwa/cena posiadanego pakietu są snapshotem zakupu; mogą różnić się od obecnej oferty.

`isEligible` oznacza, że posiadany pakiet spełnia warunki wykorzystania wejścia: aktywny, niezamknięty, opłacony, właściwa lokalizacja, ważny na dokładny termin zajęć i ma co najmniej jedno dostępne wejście. Nie gwarantuje wolnego miejsca na zajęciach.

`canSelect` oznacza możliwość wybrania pakietu w prepare. Nieopłacony pakiet może mieć canSelect=true i isEligible=false: zostanie wybrany do opłacenia, bez zapisu uczestnika. Nieaktywna/usunięta oferta blokuje prepare także przy istniejącym pakiecie (OfferUnavailable); taki pakiet może nadal spełniać samą kwalifikację wejścia.

`availableEntries = max(0, totalEntries - usedEntries - reservedEntries)`. Rezerwacje to suma SessionsCharged na innych sesjach, z CountsAgainstPackage=true, jeszcze nierozliczonych, nieanulowanych i niezakończonych. Anulowane uczestnictwa nie rezerwują. Bieżąca sesja jest wyłączona, aby nie liczyć jej powtórnie. Oczekiwanie na płatność nie rezerwuje wejścia ani miejsca. Agregacja wykonywana w SQL bez osobnego zapytania dla każdego pakietu.

Kody reasons:

| Kod | Znaczenie |
|---|---|
| LocationMismatch | Pakiet dotyczy innej lokalizacji |
| Closed | Pakiet został zamknięty |
| ExpiredOnSessionDate | Ważność kończy się przed rozpoczęciem sesji |
| NoAvailableEntries | Brak wejść po uwzględnieniu rezerwacji |
| Inactive | Pakiet nieaktywny, także oczekujący na płatność |
| PaymentRequired | Status płatności różny od Paid |
| OfferUnavailable | Brak aktywnej, poprawnej oferty grupowej dla lokalizacji |
| ValidityTooShortFromToday | Nowy pakiet aktywowany dzisiaj nie objąłby terminu sesji |

Dla nowej płatnej oferty ważność szacujemy zgodnie z aktywacją przy płatności: dzisiejsza data UTC + DurationDays. Dla darmowej: bieżąca chwila + DurationDays. To konserwatywna ocena dla zakupu/opłacenia teraz; późniejsza płatność wymaga ponownego odczytu. Istniejący pakiet z validUntil jest oceniany po tej dacie. Null w opłaconym pakiecie nie blokuje ważności; w oczekującym bez terminu stosujemy szacunek aktywacji.

## 3. Wybór i dalszy zapis

`POST /api/group-enrollments/sessions/{sessionId}`

```json
{ "clientId": 70, "packageId": 12, "clientPackageId": 345 }
```

`clientPackageId: int?` to nowe opcjonalne pole. Pozwala wskazać dokładnie posiadany pakiet wybrany na ekranie. Backend sprawdza właściciela, zgodność oferty i kwalifikację; nie podstawia innego pakietu przy błędnym ID.

Bez clientPackageId zachowana jest kompatybilność: backend wybiera kwalifikujący się pakiet tej oferty (najpierw opłacony, następnie oczekujący), a gdy brak — tworzy nowy, jeśli oferta pozwala objąć termin zajęć. Brak clientPackageId nie oznacza wymuszenia nowego zakupu.

Odpowiedź 200:

```ts
type GroupEnrollmentDto = {
  id: number;
  sessionId: number;
  clientId: number;
  clientPackageId: number;
  status: "AwaitingPayment" | "ReadyToBook" | "Booked" | "PackageUnavailable" | "Unavailable";
  amountDue: number;
  reservesSeat: boolean;
};
```

Prepare nie zapisuje uczestnika nawet dla opłaconego pakietu. Po płatności odśwież `GET /api/group-enrollments/{id}` i wywołaj `POST /api/group-enrollments/{id}/confirm`. Dopiero confirm sprawdza miejsce oraz pakiet i zapisuje uczestnika. Listę odczytuj ponownie po zmianie klienta/sesji/płatności. Ten odczyt nie jest blokadą zasobów; mutacje ponawiają walidację pod blokadą zapisu sesji/klienta.

## Błędy

- 400: niepoprawne parametry, brak locationId przy filtrze dostępu, sesja nie jest przyszłą planowaną grupą, klient zablokowany, brak dostępu grupowego lub niedostępny wskazany pakiet/oferta.
- 401: brak poprawnego uwierzytelnienia.
- 403: rola inna niż Owner.
- 404: nieistniejąca lub usunięta sesja/klient.
- 409 przy prepare: istniejące zgłoszenie dla tej pary klient–sesja wskazuje inną ofertę/konkretny pakiet; konflikt zapisu. Powtórzenie tego samego wyboru zwraca istniejące zgłoszenie.

Aktualne statusy zgłoszenia są informacyjne; confirm zawsze ponownie sprawdza dostępność. Brak wolnych miejsc nie cofa płatności.

## Walidacja i wdrożenie

Pełny zestaw lokalnych testów: 198/198, bez pominiętych. Nowy endpoint sam nie wymaga migracji; cały przygotowany flow wymaga wcześniejszej migracji `20261011101040_AddGroupEnrollmentRequests`. Gałąź wdrożenia: `codex/bsworkout-personalization`, baza działającego backendu: TestConnection. Stan wdrożenia należy potwierdzić po publikacji przez Swagger; nie zakładać dostępności na podstawie samego pliku kontraktu.
