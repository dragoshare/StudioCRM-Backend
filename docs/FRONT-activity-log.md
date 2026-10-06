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
`entityLabel` używa nazwy/tytułu/imienia i nazwiska, a przy braku etykiety: `Typ #ID`.

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
