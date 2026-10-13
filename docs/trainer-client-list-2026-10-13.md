# Lista klientów Trainer Portal — rozszerzenie 2026-10-13

`GET /api/trainer-portal/clients` nadal zwraca tablicę, bez zmiany istniejących pól i ich znaczenia. Dodane podsumowanie jest również w `GET /api/trainer-portal/clients/page` (dotychczasowa koperta paginacji), a wspólny DTO jest uzupełniany w istniejącej liście recentClients starego dashboardu trenera.

Dostęp bez zmian: Owner/Trainer na kontrolerze, ale lista jest nadal ograniczona do profilu trenera bieżącego użytkownika i klientów przypisanych do niego. Owner bez profilu trenera nie uzyskuje tu listy wszystkich klientów. Klienci grupowi innego trenera nie trafiają tu przez sam GroupAccessEnabled lub wspólną lokalizację. Na platform-organizations obowiązuje dodatkowo filtr organizacji. Uzupełnianie finansów następuje dopiero po ograniczeniu dostępu i paginacji.

## Nowe pola

- `trainerId: int|null`, `trainerFullName: string|null`, `locationId: int`.
- `activePackageId: int|null`: istniejące przypisanie oferty katalogowej Client.ActivePackageId, takie samo jak u Ownera; nie jest ID zakupionego pakietu.
- `activeClientPackageId: int|null`, `activeClientPackageName: string|null`: aktualny otwarty pakiet indywidualny/semi, zgodnie z dotychczasową listą Ownera.
- `activePackageTotalSessions`, `activePackageUsedSessions`, `activePackageRemainingSessions: int`: podsumowanie tego pakietu; 0 bez pakietu. Pozostałe = max(0, total-used), bez odejmowania przyszłych rezerwacji.
- `activePackagePaymentStatus: string`: Unpaid / Paid / Overdue / PartiallyPaid / PendingConfirmation; pusty string bez pakietu. To stan płatności, nie ważności.
- `currentBalance: decimal`: identyczne saldo rozliczeniowe jak na liście Ownera. Suma transakcji z wyłączeniem PaymentCredit i PaymentReversal; nie jest kwotą wszystkich zaległych faktur/wpłat. Brak transakcji = 0.
- `activePackages: ClientListPackageDto[]`: wszystkie pakiety IsActive=true, bez ClosureDisposition, w tym grupowe i okresowe. Sortowanie aktywacja/zakup malejąco, potem ID malejąco. Brak pakietów = [].

`ClientListPackageDto`:

```ts
type ClientListPackageDto = {
  clientPackageId: number;
  packageId: number;
  name: string;
  billingMode: "SessionBased" | "PeriodBased";
  status: "Active" | "Scheduled" | "Expired";
  paymentStatus: "Unpaid" | "Paid" | "Overdue" | "PartiallyPaid" | "PendingConfirmation";
  totalSessions: number | null;
  usedSessions: number | null;
  remainingSessions: number | null;
  validUntil: string | null;
  periodStartsAt: string | null;
  locationId: number | null;
};
```

Dla PeriodBased wszystkie trzy pola wejść są null, nie 0. Scheduled dotyczy okresu rozpoczynającego się w przyszłości. Expired może wystąpić, gdy pakiet nadal jest otwarty/oznaczony IsActive, lecz data ważności minęła — nie oznacza to prawa do rezerwacji. Status okresowy i liczby wejść wykorzystują istniejący mapper rozliczeniowy. Pakiet wygasły i już dezaktywowany nie trafia na listę. Bezterminowy pakiet ma validUntil=null. Daty są ISO 8601 UTC.

Klient może mieć treningi, grupę i dietę jednocześnie: frontend powinien korzystać z activePackages do prezentacji wszystkich usług. Dla klienta wyłącznie okresowego płaskie pola indywidualnego pakietu pozostają puste/0. `activePackages` dodano też do DTO Ownera; dotychczasowe pola jego listy zachowują znaczenie. Wspólny ClientListSummaryReader liczy podsumowania dla obu paneli, bez N+1 (2 zbiorcze zapytania po liście klientów).

## Przykładowa odpowiedź (dane demonstracyjne)

```json
[
  {
    "clientId": 42,
    "userId": null,
    "portalAccessStatus": "NoAccount",
    "fullName": "Anna Kowalska",
    "email": "anna@example.test",
    "emailContactUrl": "mailto:anna@example.test",
    "phoneNumber": null,
    "phoneContactUrl": null,
    "avatarUrl": null,
    "goal": null,
    "status": "Active",
    "billingStatus": "Paid",
    "locationName": "Kłaj",
    "trainingStartDate": "2026-09-01T00:00:00Z",
    "createdAt": "2026-09-01T08:00:00Z",
    "trainerId": 7,
    "trainerFullName": "Jan Trener",
    "locationId": 2,
    "activePackageId": 11,
    "activeClientPackageId": 101,
    "activeClientPackageName": "Pakiet 8 treningów",
    "activePackageTotalSessions": 8,
    "activePackageUsedSessions": 3,
    "activePackageRemainingSessions": 5,
    "activePackagePaymentStatus": "Paid",
    "currentBalance": 25.00,
    "activePackages": [
      {
        "clientPackageId": 102,
        "packageId": 12,
        "name": "Prowadzenie dietetyczne",
        "billingMode": "PeriodBased",
        "status": "Active",
        "paymentStatus": "Paid",
        "totalSessions": null,
        "usedSessions": null,
        "remainingSessions": null,
        "validUntil": "2026-10-31T23:00:00Z",
        "periodStartsAt": "2026-09-30T22:00:00Z",
        "locationId": 2
      },
      {
        "clientPackageId": 101,
        "packageId": 11,
        "name": "Pakiet 8 treningów",
        "billingMode": "SessionBased",
        "status": "Active",
        "paymentStatus": "Paid",
        "totalSessions": 8,
        "usedSessions": 3,
        "remainingSessions": 5,
        "validUntil": "2026-11-01T00:00:00Z",
        "periodStartsAt": null,
        "locationId": 2
      }
    ]
  }
]
```

Bez nowych migracji, bez przeliczania/zapisywania danych historycznych. Testy obejmują zgodność z Ownerem, saldo bez kredytów płatniczych, wejścia, równoległe pakiety, okresowe przyszłe usługi, klienta innego trenera, brak przypisania, archiwizację oraz paginację.
