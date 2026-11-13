# Powiadomienie „Nagroda do wydania” (2026-11-13)

Nowy typ powiadomienia w istniejącym mechanizmie (`GET /api/Notifications`, `/page`, `/unread-count`). Endpointy, format `NotificationDto` i oznaczanie jako przeczytane są bez zmian. Bez migracji bazy.

## Kto dostaje

**Tylko trener, do którego klient jest przypisany** (`Client.TrainerId`). Owner bez profilu trenera nie dostaje tych powiadomień; Owner z profilem trenera dostaje je tylko dla swoich klientów. Inni trenerzy nigdy.

## Kiedy (ochrona przed spamem)

Jedno powiadomienie na parę **klient + milestone**, gdy jednocześnie:
- klient osiągnął milestone (aktywna definicja, `(trainingStartDate ?? createdAt) + requiredMonths ≤ dziś`) **w ciągu ostatnich 30 dni**,
- nagroda nie jest wydana,
- klient nie ma statusu `Inactive` (i nie jest zarchiwizowany).

Starzy klienci z dawno osiągniętymi milestone'ami nie dostaną powiadomień po wdrożeniu. Takie zaległe nagrody nadal są widoczne w `GET /api/trainer-portal/milestones/rewards-pending` i na liście klientów (`hasPendingReward=true&sort=rewardsPendingDesc`, patrz `FRONT-klienci-nagrody-milestone.md`).

Uwaga: milestone dodany później powiadomi tylko klientów, którzy osiągają go teraz (w oknie 30 dni), nie wszystkich z dłuższym stażem — ci są na liście nagród do wydania.

## Treść

| Pole `NotificationDto` | Wartość |
| --- | --- |
| `type` | `MilestoneRewardPending` |
| `category` | `milestones` (etykieta „Nagrody”, nowa zakładka; `GET /api/Notifications/categories`) |
| `severity` | `Info` |
| `title` | `Nagroda do wydania` |
| `message` | `Anna Kowalska: Kwartał — Koszulka` (klient: nazwa milestone'u — nagroda) |
| `relatedEntityType` / `relatedEntityId` | `client` / id klienta |
| `actionUrl` | `/clients/{clientId}/workspace` |
| `createdAt` | data osiągnięcia milestone'u |

## Kiedy znika

Po wydaniu nagrody (`PATCH /api/trainer-portal/clients/{clientId}/milestones/{milestoneDefinitionId}/claim`) nieprzeczytane powiadomienie oznacza się jako przeczytane przy następnym odświeżeniu powiadomień. Tak samo po upływie 30 dni od osiągnięcia albo gdy klient stanie się `Inactive`.

## Front

- Dodaj zakładkę/filtr `milestones` („Nagrody”) — `unreadByCategory` w `/unread-count` ma nowy klucz `milestones`.
- Kliknięcie: przejście do `actionUrl` (workspace klienta, sekcja milestone'ów z przyciskiem wydania).
