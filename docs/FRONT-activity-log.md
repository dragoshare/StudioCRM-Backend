# Historia zmian — Activity Log

## Dostęp i endpointy

Wyłącznie zalogowany `Owner`. Trainer/Client otrzymuje 403; brak autoryzacji 401.

`GET /api/activity-log` — paginowana historia, najnowsze wpisy pierwsze (`createdAt DESC, id DESC`).

`GET /api/activity-log/metadata` — słowniki `entityTypes`, `operations`, `sources` do filtrów.

### Query parameters

| Parametr | Znaczenie |
| --- | --- |
| `page` | Domyślnie 1, zakres 1–100000 |
| `pageSize` | Domyślnie 25, zakres 1–100 |
| `from` | Data ISO 8601 z offsetem; dolna granica włącznie |
| `to` | Data ISO 8601 z offsetem; górna granica wyłącznie |
| `actorUserId` | ID użytkownika wykonującego zmianę |
| `source` | `User` lub `System` |
| `operation` | `Created`, `Updated`, `Deleted`, `Archived`, `Restored` |
| `entityType` | Typ obiektu ze słownika poniżej |
| `entityId` | ID jako string; wymaga `entityType` |
| `changeSetId` | UUID grupy wpisów z jednego SaveChanges |
| `search` | Fragment nazwy obiektu lub użytkownika, bez rozróżniania wielkości liter; maks. 200 znaków |

Filtry łączą się AND. Niepoprawne parametry zwracają 400. Puste filtry należy pominąć.
Daty są zwracane w UTC; front wyświetla je w Europe/Warsaw.
Dla całego dnia przekazuj początek dnia i początek kolejnego dnia z odpowiednim offsetem.

Przykład: `/api/activity-log?entityType=ClientPackage&entityId=123&pageSize=25`.

## DTO

```ts
interface ActivityLogResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: ActivityLogEntry[];
}

interface ActivityLogEntry {
  id: number; // C# long
  changeSetId: string;
  createdAt: string;
  actorUserId: number | null;
  actorName: string; // nazwa zapamiętana w momencie zapisu
  source: "User" | "System";
  operation: "Created" | "Updated" | "Deleted" | "Archived" | "Restored";
  entityType: string;
  entityId: string;
  entityLabel: string;
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
  changedFields: string[];
}
```

`before` i `after` są obiektami JSON, nie stringami do ponownego parsowania.
Klucze mają camelCase, enumy są stringami. Kwoty są liczbami, daty stringami ISO.
Dla utworzenia `before = null`, dla fizycznego usunięcia `after = null`.
UI może iterować po `changedFields`, wyświetlając `before?.[field] → after?.[field]`.
Nie traktuj `0` lub `false` jak pustej wartości. Dane wyświetlaj jako tekst, bez interpretowania HTML.

Przykład fragmentu wpisu po zmianie ceny:

```json
{
  "operation": "Updated",
  "entityType": "Package",
  "entityId": "12",
  "entityLabel": "Pakiet 8 treningów",
  "before": { "id": 12, "name": "Pakiet 8 treningów", "price": 800 },
  "after": { "id": 12, "name": "Pakiet 8 treningów", "price": 880 },
  "changedFields": ["price"]
}
```

Snapshoty mogą zawierać także pozostałe dozwolone pola danego obiektu.
`entityId` dla relacji z kluczem złożonym ma format np. `TrainerId=2;LocationId=3`.
`entityLabel` jest gotową etykietą zapisaną w momencie operacji, a nie nazwą
obliczaną podczas pobierania historii. Późniejsze zmiany nazwisk, nazw i usunięcia
obiektów nie zmieniają wcześniejszych etykiet. Front wyświetla ją bez tłumaczenia;
sam tłumaczy `operation`, `entityType` i formatuje wartości zmian.

| Encja | Przykład nowej etykiety |
| --- | --- |
| `Session` | Trening poranny (tytuł sesji) |
| `SessionParticipant` | Uczestnictwo w treningu — Jan Kowalski |
| `Client` | Jan Kowalski |
| `Trainer` | Trener — Anna Nowak |
| `User` | Anna Nowak (przy braku nazwiska/imienia: e-mail) |
| `TrainerLocation` | Lokalizacja trenera — Anna Nowak — Niepołomice |
| `ClientLocationMembership` | Dostęp klienta do lokalizacji — Jan Kowalski — Niepołomice |
| `Package` | Pakiet 8 treningów (nazwa oferty) |
| `ClientPackage` | Pakiet klienta — Jan Kowalski — Pakiet 8 treningów |
| `ClientPayment` | Płatność — Jan Kowalski |
| `ClientBalanceTransaction` | Zmiana salda — Jan Kowalski |
| `TrainerRate` | Stawka trenera — Anna Nowak |
| `TrainerContract` | Umowa trenera — Anna Nowak — UM/2026 |
| `TrainerContractLocation` | Lokalizacja umowy trenera — Anna Nowak — UM/2026 — Niepołomice |
| `TrainerMonthlySettlement` | Rozliczenie trenera — Anna Nowak — 2026-10 |

Przy brakujących nazwach stosowana jest polska nazwa rodzaju obiektu z ID, np.
`Płatność — klient #123`. Wpisy historyczne zapisane przed tą poprawką zachowują
stare etykiety; nie uzupełniamy ich obecnymi danymi. Zmiana nie wymaga nowej
migracji, nie zmienia kontraktu API ani nie dodaje zapytań przy odczycie historii.

### Filtr „Kto wykonał zmianę”

Filtr już istnieje: `GET /api/activity-log?actorUserId=123`.
Należy przekazywać ID **użytkownika**, nie ID trenera lub klienta. Etykietą opcji
może być nazwisko, ale wartością musi być ID — dwie osoby mogą mieć tę samą nazwę.
Zmiana nazwiska nie wpływa na filtrowanie, a `actorName` we wpisie zachowuje nazwę
z chwili operacji. Przykład łączenia filtrów:
`GET /api/activity-log?actorUserId=123&entityType=ClientPayment&operation=Updated`.

Dla automatycznych operacji użyj `source=System` i pomiń `actorUserId`.
`search` wyszukuje zarówno nazwę wykonawcy, jak i obiektu, więc nie zastępuje
dokładnego filtra użytkownika. Metadata zwraca słowniki typów/operacji/źródeł,
nie pełną listę użytkowników; lista autorów z pojedynczej strony logu również
nie jest kompletną listą dostępnych wykonawców.

## Zakres

- `Session`, `SessionParticipant`: sesje, terminy, trener/lokalizacja, status, uczestnictwo i naliczenia.
- `Client`, `Trainer`, `User`: profile, archiwizacja, dostęp, podstawowe dane kont.
- `ClientLocationMembership`, `TrainerLocation`: przypisania do lokalizacji.
- `Package`, `ClientPackage`: oferta, zakupione pakiety, ceny, wykorzystanie, statusy, zamknięcia i zwroty.
- `ClientPayment`, `ClientBalanceTransaction`: płatności, ich rozliczenie i operacje salda.
- `TrainerRate`, `TrainerContract`, `TrainerContractLocation`, `TrainerMonthlySettlement`: stawki, umowy, przypisania umów i rozliczenia.

Operacje zatwierdzenia płatności lub wypłaty są `Updated`; znaczenie wynika ze zmiany pól `status` / `isPaid` itd.
Zmiana `isDeleted` generuje `Archived` / `Restored`. Usunięcie fizyczne to `Deleted`.
Automaty, webhooki i inne zapisy bez zalogowanego użytkownika mają `actorUserId = null`, `actorName = System`, `source = System`.
Brak osobnego rozróżnienia konkretnych workerów. Import uruchomiony przez użytkownika zachowuje jego ID.

## Gwarancje i ograniczenia

- Zapis danych i audytu odbywa się w tej samej transakcji. Błąd audytu wycofuje zmiany; rollback operacji usuwa też jej wpisy.
- Historia działa od wdrożenia migracji. Nie rekonstruuje starszych zmian ani nie łączy wstecz istniejących audytów klientów.
- `changeSetId` grupuje pojedynczy SaveChanges; jedna operacja API może wygenerować kilka grup.
- Nie ma endpointów edycji/usuwania audytu. Wpisy nie mają kaskadowych FK do użytkowników i obiektów.
- Audyt obejmuje dozwolone pola śledzonych encji zapisywane przez DbContext. Nie obejmuje ręcznego SQL, ExecuteUpdate/ExecuteDelete ani niezaładowanych rekordów usuniętych kaskadowo przez bazę. Nowe takie ścieżki wymagają osobnej integracji z audytem.
- Hasła, hashe, tokeny logowania/Outlooka, checkout URL i techniczne payloady integracji nie są zapisywane. Zmiany samych technicznych timestampów nie tworzą wpisów.
- Zakres organizacji odpowiada aktualnej instancji/bazie CRM. Projekt nie ma obecnie izolacji wielu organizacji przez tenantId. Przed współdzieleniem bazy przez organizacje trzeba dodać tenantId do audytu i wymusić filtr serwerowy.

## Wdrożenie

Wymagana migracja `AddGlobalActivityLog` przed uruchomieniem nowej wersji backendu.
Migracja tworzy wyłącznie tabelę historii i indeksy oraz odbiera dostęp do niej rolom PUBLIC, anon i authenticated (jeśli istnieją).
Nie wykonano migracji na zewnętrznych bazach w ramach tej zmiany.
