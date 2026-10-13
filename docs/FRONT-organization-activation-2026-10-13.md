# Aktywacja drugiego studia — kontrakt frontendu (2026-10-13)

Ten etap pozwala wyjść z Draft do Active w trybie SetupOnly. Nie oznacza pełnej gotowości CRM do obsługi treningów i płatności.

## SuperAdmin

1. GET /api/super-admin/organizations/{organizationId}/activation-readiness
   - canActivate, eligibleOwnerCount, blockers[].code/message.
   - Wymagany aktywny, zweryfikowany Owner bez powiązań z innym studiem.
2. GET /api/super-admin/organizations/{organizationId} — pobierz aktualną revision.
3. POST /api/super-admin/organizations/{organizationId}/activate

```json
{
  "expectedRevision": 1,
  "reason": "Uruchomienie konfiguracji Atlas Demo",
  "confirmSetupOnly": true
}
```

Revision jest przykładowa: użyj wartości pobranej z API. Aktywacja jest atomowa, audytowana, dostępna wyłącznie SuperAdminowi. Nie zmienia BSworkout ani nie nadaje globalnych ról. Powtórzenie żądania ze starą revision zwraca 409. Nieprawidłowe warunki lub brak potwierdzenia ograniczonego trybu zwracają 400; brak organizacji 404.

Odpowiedź zawiera status Active, crmAccessEnabled true i accessMode SetupOnly. Legacy BSworkout ma FullCrm; Draft ma Unavailable. AccessMode jest też w GET /api/organization. Nie traktuj samego crmAccessEnabled jako dostępności wszystkich modułów.

## Owner nowego studia

Loguje się zwykłym POST /api/auth/login. Backend wybiera studio z przypisania konta. Nie przesyłaj organizationId ani nie pokazuj przełącznika studia.

Dostępne:
- Auth: login, refresh, me, organizations, logout, change-password.
- GET /api/organization — własna organizacja, branding, accessMode.
- Lokalizacje: GET /api/locations, GET /api/locations/{id}, POST /api/locations, PUT /api/locations/{id}.
- Trenerzy: GET /api/trainers, GET /api/trainers/{id}, POST /api/trainers.
- Klienci: GET /api/clients, GET /api/clients/{id}, GET /api/clients/filter, POST /api/clients.

POST /api/trainers używa istniejącego DTO (email, password, firstName, lastName, locationIds itd.). W nowym studiu tworzy osobne konto i członkostwo Trainer bez globalnej roli. Istniejący email jest odrzucany; nie podpinamy kont z BSworkout. Żadna wiadomość email nie jest wysyłana. Lokalizacje muszą należeć do bieżącego studia. Tworzenie profilu trenera dla istniejącego Ownera nie jest jeszcze dostępne w SetupOnly.

POST /api/clients tworzy kartotekę bez konta do logowania, tak jak dotychczas. Trener i lokalizacja muszą należeć do tego studia. W tym etapie uprawnienia kontrolerów klientów i listy trenerów pozostają Owner-only. Trener może się zalogować i pobrać swój profil Auth/me; pełny panel trenera jest kolejnym etapem.

Pozostałe moduły zwracają 403 OrganizationCapabilityUnavailable. Front powinien pokazać banner „Tryb konfiguracji studia” i ukryć niedostępne akcje, m.in. grafik, pakiety, rozliczenia, zaproszenia, ustawienia studia i integracje. Nie pokazuj tych blokad jako awarii.

## Granice wdrożenia

Brak nowych migracji. Anonymous/public endpoints oraz procesy w tle nadal obsługują wyłącznie legacy BSworkout. Nowe studio nie uruchamia Outlooka, Tpay ani automatycznych zadań. Ustawienia finansowe i automatyczne seedy nie są kopiowane z BSworkout.

Przed aktywacją Atlas Demo na Renderze musi być wdrożony ten commit. Dopiero udane POST activate oraz ponowny GET potwierdzają zmianę w bazie testowej. Sam push lub testy lokalne nie aktywują organizacji.

Walidacja: 245 testów przeszło, 0 pominiętych, lokalny PostgreSQL.
