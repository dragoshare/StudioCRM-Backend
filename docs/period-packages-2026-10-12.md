# Pakiety okresowe — backend i kontrakt frontendu

## Zakres

`billingMode`: `SessionBased` (dotychczasowe wejścia) albo `PeriodBased` (prowadzenie przez określony czas). Tryb jest niezależny od typu treningu. Po utworzeniu oferty nie można go zmieniać; dla innego sposobu rozliczenia należy stworzyć osobną ofertę. Dietetykę i prowadzenie zdalne rozróżnia nazwa oferty.

Pakiet okresowy współistnieje z treningowym oraz z innymi usługami okresowymi. Nie zastępuje ActivePackageId ani NextPackageId klienta. Nie daje wejść na zajęcia i nie uczestniczy w rozliczaniu treningów/importach Outlook. Nie ma automatycznego rozliczenia nadpłaty/salda przy jego zakupie; cena usługi jest osobnym zobowiązaniem, płaconym istniejącymi endpointami płatności.

## Oferta

Owner: `POST /api/Packages`

```json
{
  "name": "Prowadzenie dietetyczne — 30 dni",
  "billingMode": "PeriodBased",
  "price": 200,
  "currency": "PLN",
  "durationDays": 30,
  "locationId": 2,
  "isActive": true
}
```

Tryb domyślny w starych requestach to SessionBased. Cena okresowa >=0; durationDays 1..3660; oferta nie może być publiczna (isPubliclyAvailable=false), przypisuje ją Owner. Dla trybu okresowego backend ustawia sessionsLimit, sessionsPerWeek i participantsCount na 0. To techniczne wartości zachowujące kompatybilność DTO; frontend nie pokazuje 0/0 ani typu treningu.

`GET /api/Packages`, `GET /api/Packages/{id}` oraz `GET /api/Packages/options` zwracają dodatkowo billingMode. packageType ma dla usługi wartość `PeriodBased`. W pełnym starym PackageDto billingType pozostaje polem enum dla kompatybilności, lecz nie ma znaczenia dla PeriodBased; nie należy budować z niego etykiety usługi. W lekkim options billingType jest pustym stringiem. Przy formularzach treningowych filtruj billingMode=SessionBased po stronie UI; backend odrzuci próbę rozliczenia sesji pakietem okresowym.

`PUT /api/Packages/{id}` przyjmuje opcjonalny billingMode; brak zachowuje bieżący tryb. Nie można przekształcić istniejącej oferty treningowej w okresową lub odwrotnie.

## Przypisanie klientowi

Owner: `POST /api/client-packages`

```json
{
  "clientId": 70,
  "packageId": 42,
  "startsOn": "2026-10-12",
  "autoRenew": false
}
```

Odpowiedź 201: `{ "id": 345 }` — ID konkretnego ClientPackage.

- startsOn: opcjonalne DateOnly (`YYYY-MM-DD`), domyślnie dzisiaj w Europe/Warsaw; dozwolona data przeszła/przyszła.
- autoRenew: opcjonalny bool, domyślnie false; osobny od odnowienia pakietu treningowego.
- name, totalPrice, purchaseDate, paymentDueDate: istniejące opcjonalne ustawienia przypisania. TotalPrice domyślnie z oferty; PurchaseDate domyślnie teraz; termin płatności domyślnie według ustawień studia.
- totalSessions można pominąć lub przekazać 0; expectedBillingType i validUntil należy pominąć. Koniec wynika z daty rozpoczęcia i durationDays oferty.

Okres to `[periodStartsAt, validUntil)` — początek włącznie, koniec wyłącznie. Liczymy lokalne dni kalendarzowe, nie wielokrotność 24 godzin: 30 dni od 12.10 obejmuje 12.10–10.11, koniec o północy 11.11 czasu polskiego. API zwraca obie granice w UTC. UI powinien pokazać zakres lub ostatni dzień usługi, a nie potraktować północy końca jako całego kolejnego dnia.

Zapłata nie przesuwa dat, nie przedłuża i nie otwiera zakończonego okresu. Aktywność czasowa i status płatności są osobne. Oferta z przypisaną lokalizacją wymaga tej samej głównej lokalizacji klienta. Dwa otwarte przypisania tej samej oferty nie mogą nakładać się datami; różne usługi mogą działać równolegle.

## Lista usług klienta

`GET /api/clients/{clientId}/period-packages?page=1&pageSize=25`

Owner albo Client wyłącznie dla własnego profilu. Paginacja: page 1..100000, pageSize 1..100; domyślnie 1/25. Odpowiedź 200:

```ts
type PeriodPackagesPage = {
  items: ClientPackageBillingDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};
```

Używany jest istniejący ClientPackageBillingDto, rozszerzony o:

```ts
billingMode: "SessionBased" | "PeriodBased";
periodStartsAt: string | null; // UTC
periodDurationDays: number;
periodAutoRenewEnabled: boolean;
periodStatus: null | "Scheduled" | "Active" | "Expired" | "Closed" | "Inactive";
```

Istniejące pola obejmują m.in. clientPackageId, packageId, packageName, totalPrice, amountPaid, amountDue, currency, paymentStatus, validUntil i paymentDueDate. Te same nowe pola dostępne są w istniejących listach rozliczeń używających ClientPackageBillingDto. Dla usługi expectedBillingType jest pusty, packageType=PeriodBased, liczniki sesji są zerowe. Nie pokazuj liczników ani pasków wykorzystania.

Do etykiety stanu używaj periodStatus, a nie samego isActive. Scheduled oznacza przyszły początek; Expired działa od chwili końca, nawet zanim wykona się worker. Statusy płatności zachowują dotychczasowy słownik. Płatność wskazuje konkretny clientPackageId przez dotychczasowy flow płatności.

## Odnowienie i zakończenie

Owner: `PATCH /api/clients/{clientId}/period-packages/{clientPackageId}/renewal`

```json
{ "enabled": true }
```

Odpowiedź 204. Ustawienie można zmienić dla otwartego, niezakończonego okresu. Zmiana trafia do historii klienta i Activity Log. Wyłączenie zatrzymuje kolejne odnowienie, nie skraca obecnego okresu.

Worker co 5 minut zamyka czasowo zakończone okresy. Gdy odnowienie jest włączone, klient nie jest zablokowany/zarchiwizowany i oferta nadal jest aktywna, tworzy kolejny okres od poprzedniej granicy końca. Zachowuje cenę, walutę, nazwę i długość z przypisania klienta; późniejsza edycja cennika nie zmienia uzgodnionej ceny odnowienia. Nowy okres płatny otrzymuje Unpaid, darmowy Paid. Termin płatności wynika z początku nowego okresu i ustawienia studia. Odnowienie nie pobiera automatycznie pieniędzy.

Worker przetwarza do 50 okresów na przebieg. Po przerwie nadrabia zaległe okresy, zachowując ciągłość dat. Dlatego włączenie autoRenew dla przypisania z odległą datą przeszłą może utworzyć także należności za minione okresy. Nie tworzy ponownie tego samego następcy; zabezpieczają to blokady klienta/pakietu i unikalny indeks. Nie tworzy następcy, jeśli Owner już przypisał tę samą usługę na nakładający się kolejny okres.

Wcześniejsze zakończenie i rozliczenie pieniędzy: istniejący management-preview + close pakietu (decyzja o należności/zwrocie). Nie stosuj activate, importu wykorzystania wejść, correct ani expiry-decision przeznaczonych dla pakietów treningowych. Zmiana dat/ceny istniejącego przypisania wymaga zamknięcia i przypisania nowego okresu. Zamknięcie całej współpracy obejmuje również pakiety okresowe.

## Błędy i kompatybilność

400 — nieprawidłowy tryb/okres/cena, nakładające się przypisanie, nieodpowiednia lokalizacja, użycie pól wejść dla usługi lub próba zmiany odnowienia zakończonego okresu. 401 — brak uwierzytelnienia. 403 — brak roli do operacji. 404 — brak pakietu/klienta lub cudzy profil dla Client. 409 — konflikt współbieżnego zapisu; odświeżyć stan.

Migracja `20261012192652_AddPeriodBasedPackages` dodaje pola i zmienia indeks aktywnego pakietu tak, aby osobno traktować treningi. Stare dane zachowują SessionBased; bez przeliczania historii. Migrację należy zastosować przed uruchomieniem nowego API. Nie została zastosowana na TestConnection ani na live w ramach tego zadania.

## Weryfikacja

Pełny zestaw testów: 203/203. Lokalna próba migracji od poprzedniego schematu do nowego zakończona powodzeniem; brak rozbieżności snapshotu. Testy obejmują współistnienie z treningami, datę Ownera/domysł dzisiaj, zmianę czasu, płatność bez zmiany okresu, blokadę rozliczenia treningu, dostęp do własnego profilu, nakładanie okresów i równoległe odnowienie bez duplikatu.
