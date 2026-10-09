# Zajęcia grupowe: zasady, miejsca i uczestnicy

## Wdrożenie

Backend wymaga migracji `20261009091836_AddGroupBookingRules` przed uruchomieniem
nowej wersji. Migracja dodaje trzy kolumny do Sessions. Nie zmienia istniejących
rezerwacji ani rozliczeń. W tej zmianie nie wdrażamy automatycznej listy rezerwowej,
minimalnej liczby uczestników ani kary za późne anulowanie.

## Tworzenie i edycja sesji

Istniejące endpointy sesji, również trainer-portal, przyjmują dodatkowo:

```json
{
  "eventRules": "Przyjdź 5 minut wcześniej. Zabierz wodę.",
  "registrationClosesBeforeMinutes": 30,
  "cancellationClosesBeforeMinutes": 720,
  "plannedSessionType": "Group",
  "publicCapacity": 10
}
```

- Są to dodatkowe pola, przykład nie jest pełnym żądaniem utworzenia sesji.
- Nowe sesje mają domyślnie 30 minut na zapis i 720 minut (12 godzin) na anulowanie.
- Istniejące sesje po migracji mają oba progi równe 0: termin to rozpoczęcie zajęć.
- Zakres każdego progu: 0–525600 minut. Zasady dotyczą publicznych zapisów klienta.
- Pominięcie/null nowych pól przy edycji zachowuje dotychczasowe wartości.
  Pusty string usuwa tekst eventRules, jawne 0 zeruje próg.
- Serie kopiują ustawienia do kolejnych wystąpień.
- publicCapacity można ustawić także dla grup nieudostępnionych publicznie.
- Note pozostaje opisem zajęć, eventRules jest oddzielnym tekstem. Renderować jako
  zwykły tekst, nie jako zaufany HTML.

## Odczyt

PublicGroupClassDto, pełny SessionDto i TrainerPortalSessionDto zwracają:

```json
{
  "isGroupSession": true,
  "eventRules": "Przyjdź 5 minut wcześniej. Zabierz wodę.",
  "capacity": 10,
  "bookedSeats": 4,
  "availableSeats": 6,
  "isFullyBooked": false,
  "bookingRules": {
    "registrationClosesBeforeMinutes": 30,
    "cancellationClosesBeforeMinutes": 720,
    "registrationClosesAtUtc": "2026-10-15T15:30:00Z",
    "cancellationClosesAtUtc": "2026-10-15T04:00:00Z",
    "lateCancellationPolicy": "ContactStudio"
  }
}
```

Terminy mają UTC, niezależnie od starszych pól dat w czasie studia. Licznik miejsc
to bookedSeats/capacity. Capacity i availableSeats mogą być null w kalendarzu,
gdy nie określono pojemności. Nie zastępować ich limitem lokalizacji.
BookedSeats pomija CancelledInTime i CancelledLate. Starsze ParticipantsCount
pozostaje dla kompatybilności i nie służy do licznika aktywnych zapisów.

Po osiągnięciu terminu backend odrzuca zapis/anulowanie. Front powinien pokazać
komunikat z API i odświeżyć dane (także przy jednoczesnych zapisach na ostatnie miejsce).
BookingRules opisuje terminy, nie potwierdza uprawnienia klienta do rezerwacji:
nadal wymagane są weryfikacja e-mail, regulamin, wolne miejsce i opłacony pakiet.
Po terminie anulowania wyświetlić „Skontaktuj się ze studiem”. Nie obiecywać
automatycznego zwrotu ani kary. Terminowe anulowanie zwalnia rezerwację wejścia.

## Panel trenera

Lista sesji i listy sesji w dashboardzie mają te same nowe pola, a także
locationId, plannedSessionType, actualSessionType, isPubliclyBookable i participants:

```json
{
  "clientId": 123,
  "clientFullName": "Anna Nowak",
  "attendanceStatus": "Planned",
  "profileUrl": "/api/trainer-portal/sessions/456/participants/123/profile"
}
```

Lista zawiera również anulowanych uczestników z ich statusem. Sekcja „Zapisani”
powinna odfiltrować oba statusy anulowania. Publiczne API nie ujawnia listy nazwisk.

ProfileUrl jest adresem API, nie trasą strony frontu. Front tworzy własny link/widok
i pobiera dane z tego endpointu. Pełny SessionDto udostępnia ten sam adres jako
participants[].trainerProfileUrl. Odpowiedź:

```json
{
  "clientId": 123,
  "fullName": "Anna Nowak",
  "attendanceStatus": "Planned",
  "sessionId": 456,
  "locationId": 2
}
```

Jest to ograniczony widok uczestnika, bez finansów, kontaktu i prywatnych notatek.
Trener musi mieć dostęp do lokalizacji sesji, a klient musi być jej uczestnikiem.
Brak dostępu lub uczestnika daje 404. Uprawnienia do istniejącego pełnego profilu
klienta pozostają bez zmian.

Trener nadal edytuje własne sesje. Do grupy może dodatkowo dodać klienta z
GroupAccessEnabled w ClientLocationMembership dla lokalizacji zajęć. Nie rozszerza
to dostępu do edycji klientów ani listy wszystkich klientów trenera.

## Kalendarz

- Wyróżnienie grupy opierać na isGroupSession, nie na liczbie osób ani samej fladze
  isPubliclyBookable.
- Zachować kolor instruktora; dodać kontrastową ramkę/tło lub znacznik „Grupowe”.
- W karcie wyświetlić bookedSeats/capacity i listę zapisanych osób.
- Wygląd kalendarza wymaga zmian w repozytorium frontendu.
