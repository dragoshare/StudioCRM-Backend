# Listy klientów — nagrody za milestone'y (2026-11-13)

Dotyczy obu list:
- Owner: `GET /api/clients` (pola w obu trybach, filtr i sortowanie w trybie stronicowanym, `order=asc|desc` działa jak dla innych wartości),
- Trener: `GET /api/trainer-portal/clients/page` (pola także w `GET /api/trainer-portal/clients` i `recentClients` dashboardu).

Istniejące parametry i pola są bez zmian.

## Nowe pola wiersza (`ClientDto`, `TrainerPortalClientDto`)

| Pole | Typ | Opis |
| --- | --- | --- |
| `milestonesAchieved` | int ≥ 0 | Ile **aktywnych** milestone'ów klient osiągnął: `(trainingStartDate ?? createdAt) + requiredMonths ≤ dziś`, ta sama reguła co w `GET .../milestones`. |
| `milestoneRewardsClaimed` | int ≥ 0 | Ile nagród wydano (`isRewardClaimed = true`). Liczy też nagrody z milestone'ów później wyłączonych — klient faktycznie je dostał. |
| `milestoneRewardsPending` | int ≥ 0 | Ile osiągniętych aktywnych milestone'ów nie ma wydanej nagrody. |

Wartości są liczone przy każdym odczycie z definicji i stażu, nie z zapisanych liczników. Dlatego:
- milestone dodany później od razu liczy się wszystkim klientom, którzy mają wymagany staż (nie trzeba nic przeliczać ani otwierać profilu klienta),
- zmiana `trainingStartDate` od razu przesuwa wyniki,
- cofnięcie wydania (unclaim) od razu zwiększa `milestoneRewardsPending`.

**Wyświetlanie:** np. „2/3 nagród” = `milestoneRewardsClaimed / milestonesAchieved` i znaczek „1 do wydania” gdy `milestoneRewardsPending > 0`. Uwaga: `milestoneRewardsClaimed` może być większe niż `milestonesAchieved` (nagroda z wyłączonego milestone'u albo po późniejszej zmianie daty startu) — wtedy pokaż np. „wydane: 2”.

## Nowy filtr

| Parametr | Wartości | Opis |
| --- | --- | --- |
| `hasPendingReward` | `true` / `false` | `true`: tylko klienci z `milestoneRewardsPending > 0`. `false` lub brak: bez filtra. Działa przed stronicowaniem (`totalCount` po filtrze). |

## Nowe wartości `sort`

| Wartość | Kolejność |
| --- | --- |
| `rewardsPendingDesc` | najwięcej nagród do wydania; przy remisie dłuższy staż (wcześniejsza `trainingStartDate ?? createdAt`) wyżej; potem `clientId` |
| `rewardsPending` | najmniej do wydania; przy remisie krótszy staż wyżej; potem `clientId` |
| `rewardsClaimedDesc` | najwięcej wydanych; przy remisie więcej do wydania wyżej; potem `clientId` |
| `rewardsClaimed` | najmniej wydanych; przy remisie mniej do wydania wyżej; potem `clientId` |

Nieznana wartość `sort`: 400. Sortowanie i filtr działają w jednym zapytaniu SQL na stronę.

## Uprawnienia

Bez zmian: Owner widzi klientów organizacji, trener tylko swoich (portal i `/api/clients`). Wydawanie nagród nadal przez istniejące endpointy milestone'ów.

## Przykłady

```http
GET /api/trainer-portal/clients/page?hasPendingReward=true&sort=rewardsPendingDesc
GET /api/trainer-portal/clients/page?sort=rewardsClaimedDesc&page=1&pageSize=25
GET /api/clients?page=1&pageSize=25&hasPendingReward=true&sort=rewardsPendingDesc
```

```json
{ "clientId": 41, "fullName": "Anna Kowalska", "milestonesAchieved": 3, "milestoneRewardsClaimed": 2, "milestoneRewardsPending": 1, "...": "..." }
```
