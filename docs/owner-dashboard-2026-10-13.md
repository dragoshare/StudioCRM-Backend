# Dashboard Ownera — kontrakt backendu, 13.10.2026

Nowe endpointy pod `/api/owner/dashboard`, wszystkie wyłącznie dla zalogowanego Ownera. Stary `GET /api/Dashboard/owner` pozostaje bez zmian na czas przełączenia frontu. Te endpointy dotyczą studia obsługiwanego przez obecną bazę backendu; nie wprowadzają nowego mechanizmu izolacji organizacji.

## 1. Cztery kafelki

`GET /api/owner/dashboard/summary?period=2026-10`

`period` opcjonalny: dokładnie `YYYY-MM`, od 2000-01 do bieżącego miesiąca, domyślnie bieżący miesiąc w Europe/Warsaw.

```ts
type MoneyMetric = {
  current: number;
  previous: number;
  changeAmount: number;
  changePercent: number | null;
};
type CountMetric = {
  current: number;
  previous: number;
  change: number;
  changePercent: number | null;
};
type Summary = {
  period: string;
  previousPeriod: string;
  timeZone: "Europe/Warsaw";
  asOf: string; // UTC, ISO 8601
  isPartial: boolean;
  currency: "PLN";
  revenue: MoneyMetric;
  activeClients: CountMetric;
  newClients: number;
  completedSessions: CountMetric;
  clientsWithoutJoinedAt: number;
};
```

Mapowanie wizualizacji:

- Przychód: revenue.current i revenue.changePercent.
- Aktywni klienci: activeClients.current i activeClients.change (np. „+8 względem poprzedniego miesiąca”).
- Nowi klienci: newClients.
- Wykonane treningi: completedSessions.current i completedSessions.changePercent.

**Przychód** to operacyjny wskaźnik wpływów: suma potwierdzonych ClientPayments w PLN według PaymentDate, pomniejszona o zarejestrowane potwierdzone zwroty pakietów według RefundConfirmedAt. Nie używamy ceny sprzedanych/nieopłaconych pakietów, salda, oczekujących wpłat ani daty utworzenia wpisu. Wpłaty Rejected/Cancelled/Reversed nie są wliczane. Korekta daty wpłaty przenosi ją między miesiącami. Historyczne wpłaty zarchiwizowanego klienta pozostają w przychodzie. Kwoty nie są pomniejszane o prowizję operatora, koszty ani podatki; to nie zysk studia. Inne waluty są wykluczone, bez domyślnego przeliczania kursów. Zwrot może dać ujemny wynik miesiąca.

**Aktywny klient** — zgodnie z decyzją Ownera — co najmniej jeden ukończony trening z AttendanceStatus=Present w danym miesiącu LUB rzeczywiście rozpoczęta usługa okresowa obejmująca część miesiąca. Liczymy unikalne ID; kilka treningów i usług nie zwielokrotnia klienta. Nie stosujemy dzisiejszego Client.Status do odtwarzania aktywności w poprzednim miesiącu. Usługa zamknięta przed rozpoczęciem nie daje aktywności; zamknięcie lub archiwizacja ogranicza zakres jej działania. NoShow nie oznacza odbytego treningu.

**Nowy klient** — TrainingStartDate przypada w wybranym miesiącu (do dzisiaj, gdy miesiąc trwa). To data biznesowa rozpoczęcia, nie CreatedAt. Brak daty nie jest zastępowany datą importu/rejestracji; nie wlicza się do newClients. clientsWithoutJoinedAt informuje o liczbie obecnych niezarchiwizowanych profili bez tej daty, aby ujawnić niekompletność danych. Historyczne statystyki nowych klientów obejmują także później zarchiwizowane profile.

**Wykonane treningi** — liczba nieusuniętych sesji ze statusem Completed według StartAt. Grupa to jedna sesja, nie liczba uczestników. Późniejsze oznaczenie wykonania aktualizuje miesiąc, w którym trening się odbył.

Porównanie m/m używa wybranego miesiąca i poprzedniego pełnego miesiąca. Dla bieżącego miesiąca wartości są liczone tylko do asOf i isPartial=true — frontend powinien zaznaczyć „miesiąc w toku”, aby nie sugerować porównania dwóch pełnych miesięcy. Gdy previous=0 i current≠0, changePercent=null; gdy obie wartości są 0, zmiana wynosi 0. W pozostałych przypadkach `(current-previous)/abs(previous)*100`, zaokrąglone do dwóch miejsc. Nie wyświetlaj null jako 0% ani 100%.

## 2. Wykres przychodów

`GET /api/owner/dashboard/revenue?months=12&period=2026-10`

- months: wyłącznie 6 lub 12, domyślnie 12.
- period: opcjonalny miesiąc kończący wykres; reguły jak w summary. Pozwala podłączyć górny selektor miesiąca do wykresu.

```ts
type Revenue = {
  period: string;
  months: 6 | 12;
  currency: "PLN";
  timeZone: "Europe/Warsaw";
  asOf: string;
  items: {
    period: string;
    received: number;
    refunded: number;
    revenue: number;
    isPartial: boolean;
  }[];
  total: number;
  average: number;
  previousYearTotal: number;
  yearOverYearChangePercent: number | null;
};
```

Items zawiera dokładnie 6/12 kolejnych miesięcy, rosnąco. Miesiące bez zarejestrowanych transakcji mają 0. Front wyświetla period jako nazwę miesiąca i rysuje revenue, bez obliczania kwot. Average to średnia ze wszystkich zwróconych miesięcy, również zerowych i miesiąca w toku.

YearOverYearChangePercent porównuje sumę wybranego okna z analogicznym oknem rok wcześniej; dla okresu bieżącego poprzedni rok kończy się w analogicznym dniu i godzinie lokalnej. Służy do etykiety „zmiana przychodu r/r”. Nie jest miarą „stabilności” z wizualizacji. Zasada null przy zerowej podstawie jak w summary. Nie ma estymacji przyszłych wpływów.

## 3. Lista ostatnio dołączonych klientów

`GET /api/owner/dashboard/recent-clients?limit=4`

Limit 1..20, domyślnie 4. Odpowiedź 200 — tablica:

```ts
type RecentClient = {
  id: number;
  firstName: string;
  lastName: string;
  fullName: string;
  joinedAt: string | null; // TrainingStartDate, data biznesowa
  createdAt: string; // techniczna data utworzenia, nie zastępuje joinedAt
};
```

Sortowanie od najnowszej znanej daty dołączenia; profile z brakiem daty trafiają na koniec, między sobą od najnowszego CreatedAt. Wykluczeni są klienci zarchiwizowani i z przyszłą datą rozpoczęcia. Gdy joinedAt=null, pokaż „Brak daty”. Lista jest bieżąca, niezależna od period kafelków. Inicjały awatara i format daty to prezentacja po stronie frontu.

Na wizualizacji znajduje się również „Średni czas wdrożenia nowego klienta: 2 dni”. Backend nie ma dat początku/końca procesu wdrożenia; tej wartości nie zwracamy. Usuń tę etykietę lub ukryj ją do czasu dodania rzeczywistych danych procesu.

## 4. Warto sprawdzić

`GET /api/owner/dashboard/attention`

```ts
type Attention = {
  asOf: string;
  inactivityDays: 30;
  clientsWithoutTraining30Days: number;
  clientsWithOneRemainingSession: number;
  clientsWithoutAttendanceBaseline: number;
};
```

Stan bieżący, niezależny od period. Wykluczamy zarchiwizowane i zablokowane profile.

- clientsWithoutTraining30Days: ostatni ukończony trening z obecnością był co najmniej 30 lokalnych dni temu. Dla osoby bez odbytego treningu używamy znanej TrainingStartDate. Bierzemy klientów mających historię treningową albo pakiet na wejścia; osoby korzystające wyłącznie z dietetyki/zdalnej usługi okresowej nie trafiają do tego alertu. Nieobecność nie odświeża daty ostatniego treningu.
- clientsWithoutAttendanceBaseline: klienci z tej grupy bez odbytego treningu i bez TrainingStartDate. Nie zgadujemy ich czasu nieaktywności i nie dodajemy do pierwszego licznika.
- clientsWithOneRemainingSession: unikalni klienci z przynajmniej jednym aktywnym, niezamkniętym i niewygasłym pakietem na wejścia, w którym TotalSessions−UsedSessions=1. Dotyczy także grup. Kilka takich pakietów u jednej osoby daje 1 klienta. Pakiety okresowe są wykluczone. Nie odejmujemy przyszłych rezerwacji — to alert ostatniego pozostałego do rozliczenia wejścia. Wartość nie jest liczbą zaległych płatności i sama nie gwarantuje, że klient nie opłacił już następnego pakietu.

## Dostęp, błędy i wdrożenie

Bearer JWT + rola Owner na kontrolerze i kontrola Ownera w usłudze. 401 bez poprawnej sesji, 403 dla innych ról; 400 przy niepoprawnym period/months/limit. Puste wyniki to 200 z rzeczywistymi zerami/pustą listą. Standardowe błędy biznesowe: `{ "message": "..." }`.

Obliczenia są po stronie SQL/backendu; nie pobieramy całego grafiku ani historii wpłat do frontu. Wykres wykonuje dwa agregujące zapytania niezależnie od wyboru 6/12 miesięcy. Summary wykonuje siedem zapytań agregujących, recent-clients jedno z limitem, attention trzy. Kwoty/daty procenty i różnice są gotowe do wyświetlenia.

Nowe endpointy nie dodają własnej migracji. Wymagają schematu z przygotowanej migracji pakietów okresowych `20261012192652_AddPeriodBasedPackages`. Kod jest na gałęzi `codex/bsworkout-personalization`; dostępność na live trzeba potwierdzić po migracji i wdrożeniu. Frontend może wdrożyć widok po publikacji tych endpointów.

Weryfikacja: kompilacja bez błędów/ostrzeżeń; 4/4 testy integracyjne na izolowanym PostgreSQL. Testy obejmują polską granicę miesiąca, wpłaty/refundy/statusy/waluty, archiwizowanego płatnika, unikalną aktywność trening+usługa, zera na wykresie, brak daty dołączenia, alerty i dostęp innych ról.
