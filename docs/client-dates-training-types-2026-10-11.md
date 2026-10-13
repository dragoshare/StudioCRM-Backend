# Daty klienta i typy treningów — kontrakt 11.10.2026

## Data dołączenia

GET /api/Clients/filter zwraca ClientDto.trainingStartDate (DateTime?, ISO 8601 lub null): ręcznie podana data rozpoczęcia treningów. To istniejące pole biznesowe do pokazania daty rozpoczęcia współpracy. GET/PATCH klienta używa tego samego pola; CreateClientDto i UpdateClientDto przyjmują trainingStartDate. Nie dodano drugiego pola ani migracji.

createdAt to techniczny czas utworzenia rekordu klienta, także importu. Nie jest wiarygodną datą dołączenia do studia. Nie stosuj go jako ukrytego fallbacku. Osobnego ogólnego joinedAt klienta nie ma. ClientLocationMembership.JoinedAt opisuje przypisanie do lokalizacji, nie pierwsze dołączenie do studia.

Klaudia S. (clientId 70): trainingStartDate=null; createdAt=2026-08-20T09:13:09.331607Z. Faktyczna data rozpoczęcia wymaga uzupełnienia przez obsługę. Nie ustalano jej z pierwszej sesji.

## Pełny słownik

| Kod odpowiedzi | Wartość enum w bazie / typowanych requestach | Etykieta |
|---|---:|---|
| OneToOne | 1 | Trening 1:1 |
| TwoToOne | 2 | Trening 2:1 |
| ThreeToOne | 3 | Trening 3:1 |
| FourToOne | 4 | Trening 4:1 |
| Group | 5 | Zajęcia grupowe |

Group nie oznacza pięciu uczestników: grupa może mieć inną frekwencję. Odpowiedzi wymienione poniżej zwracają nazwy tekstowe, nie polskie etykiety, nie Individual/SemiPersonal. PackageType to osobny słownik i nie należy go mieszać z SessionBillingType.

| Pole | Znaczenie | Brak wartości |
|---|---|---|
| plannedSessionType | zadeklarowany plan całej sesji | null dla starszych/nieoznaczonych sesji |
| actualSessionType | faktyczny typ całej sesji po wykonaniu | null przed wykonaniem lub gdy historycznie nie zapisano |
| expectedBillingType | typ zakupionego pakietu klienta | w podsumowaniu usage pusty string gdy nie ma aktualnego cyklu |
| plannedBillingType | zapisany oczekiwany typ rozliczenia uczestnika, zwykle z pakietu | pusty string przy braku snapshotu |
| actualBillingType | faktycznie rozliczony typ uczestnika | pusty string przy braku rozliczenia |

Nie stosuj expectedBillingType/plannedBillingType zamiast faktycznego typu sesji: klient z pakietem 1:1 może uczestniczyć w semi z korektą ceny. To istniejąca logika i nie została zmieniona w tym zadaniu. Nie wyliczaj historycznego typu z obecnej liczby uczestników.

## Wynik diagnostyki

Odczyt TestConnection: policzone uczestnictwa z brakującym ActualBillingType: 0. Istnieją sesje z plannedSessionType=null; to rzeczywisty brak planowanego typu, nie brak typu faktycznego.

Dla Klaudii, usage zwrócone przez rzeczywistą usługę backendową:

| SessionId | Data UTC | plannedBillingType | actualBillingType |
|---|---|---|---|
| 628 | 2026-10-08 06:00 | OneToOne | ThreeToOne |
| 500 | 2026-09-18 07:00 | OneToOne | OneToOne |
| 498 | 2026-09-04 07:00 | OneToOne | OneToOne |
| 497 | 2026-08-28 07:00 | OneToOne | TwoToOne |
| 474 | 2026-08-27 08:00 | OneToOne | FourToOne |
| 469 | 2026-08-25 17:15 | OneToOne | TwoToOne |
| 465 | 2026-08-21 14:00 | OneToOne | ThreeToOne |

expectedBillingType=OneToOne; totalSessions=12; usedSessions=7; remainingSessions=5. Zrzut pokazuje poprawną etykietę wyłącznie dla TwoToOne. Dla tego przykładu pozostałe „Typ nieokreślony”/„Brak danych” sugerują odczyt niewłaściwego pola albo niepełny słownik frontendu (bez kodu frontendu nie potwierdzono konkretnej przyczyny), a nie brak ActualBillingType w bazie lub DTO usage. Nie wykonywano backfillu ani korekt rozliczeń.

## Poprawka API i integracja frontendu

Sessions/filter już zawiera plannedSessionType i actualSessionType na sesji oraz plannedBillingType i actualBillingType w participants[]. Te same pola sesji są w TrainerPortalSessionDto. SubscriptionUsageDto używany przez current-cycle/usage i Trainer Portal ma expectedBillingType, a sessions[] ma plannedBillingType/actualBillingType. Kontrakty tych istniejących pól nie zmieniły się.

Dedykowana historia klienta ClientSessionHistoryDto nie miała żadnego z tych pól. Dodano cztery pola do wspólnej projekcji: plannedSessionType (string|null), actualSessionType (string|null), plannedBillingType (string, brak=""), actualBillingType (string, brak=""). Dotyczy endpointów historii klienta i trainer-portal/clients/{id}/sessions oraz /sessions/recent. Istniejąca paginacja, autoryzacja i rozliczenia bez zmian. Jedna projekcja SQL, bez dodatkowych zapytań na każdy trening.

Dla zakończonej sesji etykietę treningu buduj z actualSessionType. Dla planowanej z plannedSessionType, a null oznacz jako „Nie określono planowanego typu”. W wykorzystaniu pakietu pokazuj actualBillingType; plan rozliczenia pokazuj osobno jako plannedBillingType/expectedBillingType. Rzeczywiste null/pusty string nadal są brakiem danych — API ich nie zgaduje.

## Aktualny DTO historii (dodane pola na końcu)

```csharp
namespace StudioCRM.Application.DTOs.Clients;

public class ClientHistoryFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string Scope { get; set; } = "All";
}

public class ClientAuditDto
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public int? ActorUserId { get; set; }
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientSessionHistoryDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = "";
    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = "";
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string AttendanceStatus { get; set; } = "";
    public bool IsCountedFromPackage { get; set; }
    public int? ClientPackageId { get; set; }
    public string? PlannedSessionType { get; set; }
    public string? ActualSessionType { get; set; }
    public string PlannedBillingType { get; set; } = "";
    public string ActualBillingType { get; set; } = "";
}

```

## Zapobieganie nowym błędom

Tworzenie i edycja sesji bez jawnego typu rozpoznają cztery osoby jako FourToOne, zamiast Group. Sesje publicznie rezerwowalne zapisują Group niezależnie od frekwencji. Nowe sesje importowane z Outlooka zapisują plannedSessionType dla 1–4 unikalnych klientów. Nie zmieniano zapisanych wcześniej sesji. Zmiany DTO i zapisu wymagają wdrożenia backendu; nie należy zakładać ich dostępności na live przed wdrożeniem.
