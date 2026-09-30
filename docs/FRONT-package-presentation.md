# Pakiety klienta: typ i najbliższe zajęcia

Zmiana jest addytywna i nie wymaga migracji bazy.

## Nowe pola

`packageType` jest stringiem:

- `Individual`: rozliczenie `OneToOne`;
- `SemiPersonal`: `TwoToOne`, `ThreeToOne`, `FourToOne`;
- `Group`: `Group`;
- `Unknown`: nieprawidłowa/stara wartość typu rozliczenia; UI powinno pokazać neutralny wariant.

Typ wynika z zapisanej konfiguracji pakietu klienta, nie z typu ostatnio rozliczonej sesji. Dotychczasowe pola rozliczeniowe pozostają bez zmian.

`nextSessionAt` to nullable data ISO 8601 w UTC. Front powinien formatować ją w strefie `Europe/Warsaw`.
Jest to najwcześniejszy przyszły termin sesji `Planned`, dla której klient ma uczestnictwo `Planned` powiązane przez dokładny `clientPackageId` i jeszcze nierozliczone z pakietu.
Usunięte, zakończone i anulowane sesje oraz anulowane uczestnictwa są pomijane.
Brak takiej rezerwacji daje `null`; lokalizacja, nazwa pakietu i katalogowe `packageId` nie są używane do zgadywania terminu.

## Odpowiedzi z nowymi polami

- `GET /api/billing/clients/{id}`: `packages[]`;
- `GET /api/billing/clients/{id}/active-package`;
- `GET /api/clients/{id}/subscription/current-cycle`;
- `GET /api/clients/{id}/packages/history`: `items[]`;
- odpowiedzi subskrypcji: `currentCycle`;
- dashboard portalu klienta: `package` (dodatkowo `clientPackageId`; przy braku pakietu typ i identyfikator są `null`);
- billing portalu klienta: `packages[]`;
- `GET /api/public/group-classes/me`: `packages[]`.

Endpointy współdzielące powyższe DTO i mapowania także otrzymują nowe pola. Daty są pobierane jednym zapytaniem dla całej zwracanej listy pakietów.

## Istniejące kontrakty

Przypisania lokalizacji: `GET /api/clients/{id}/locations` zwraca `locationId`, `name`, `isHomeLocation`, `groupAccessEnabled`, niezależnie od posiadanych pakietów.

Zaproszenia: lista, szczegóły, tworzenie i resend zwracają `clientId`, jeśli zaproszenie utworzono z tym powiązaniem. Nie należy zastępować powiązania dopasowaniem po adresie e-mail.

Owner ma dostęp do profilu i historii archiwalnego klienta. Poprawiono dodatkowo odczyt aktywnego pakietu, aby globalny filtr klienta nie ukrywał pakietu w starszych danych archiwalnych. Uprawnienia trenera i klienta pozostają sprawdzane przed odczytem.
