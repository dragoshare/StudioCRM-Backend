# SuperAdmin: rejestr organizacji — 01.10.2026

## Zakres obecnej implementacji

Osobny panel platformy, lista organizacji, szczegóły i branding wskazanej organizacji. Migracja dodaje rejestr oraz BSworkout i wiąże je z istniejącym profilem brandingu. Nie zmienia klientów, lokalizacji, sesji, pakietów ani integracji. Dane operacyjne nadal nie mają pełnej izolacji wielu organizacji. NIE dodawać drugiego studia produkcyjnego ręcznie. Nie ma endpointu tworzenia organizacji.

BSworkout organizationId: b5100000-0000-4000-8000-000000000002.
Branding profileId: b5100000-0000-4000-8000-000000000001.
To odrębne identyfikatory. Front używa organizationId jako kontekstu panelu; nie profileId.

## Wejście i publiczny wygląd

GET /api/organization (anonimowo, bez cache) zwraca organizationId, name, uiVariant i branding. Branding zawiera wyłącznie opublikowaną konfigurację zgodną z FRONT-bsworkout-branding.md. uiVariant to aktualnie bsworkout; nieznany wariant front obsługuje standardowym układem. Konfiguracja backendu Branding:ProfileId wskazuje organizację bieżącego wdrożenia. Nie jest to wybór organizacji przez parametr z przeglądarki. Brak pasującego rekordu to 404, nigdy fallback do innego studia. Front w razie błędu zachowuje neutralny wygląd i możliwość logowania.

Stare GET /api/organization/branding oraz /api/super-admin/organization/branding/... nadal działają według skonfigurowanego profilu. Nowy panel korzysta ze ścieżek jawnie wskazujących organizację.

## API panelu platformy

Wymagany JWT z rolą SuperAdmin oraz aktywny użytkownik z tą rolą aktualnie w bazie. Owner sam w sobie nie ma dostępu. Role front odczytuje z /api/auth/me. Nie przełączać konta ani roli na Owner po wybraniu studia.

- GET /api/super-admin/organizations?page=1&pageSize=25 — items, total, page, pageSize. Element: organizationId, name, slug, uiVariant. page 1..100000, pageSize 1..100.
- GET /api/super-admin/organizations/{organizationId} — ten sam zestaw pól pojedynczej organizacji.
- Pod /api/super-admin/organizations/{organizationId}/branding dostępne: GET draft, PUT draft, POST publish, POST restore, GET versions, GET audit, POST assets.

Treści żądań i odpowiedzi brandingu identyczne jak w FRONT-bsworkout-branding.md. Zachować expectedRevision, obsłużyć konflikt 409; nie nadpisywać automatycznie. Restore odtwarza tylko szkic, wymaga późniejszej publikacji. Upload multipart file, PNG maks. 5 MiB.

400 — błędne dane/paginacja; 401 — brak ważnej sesji; 403 — brak aktualnych uprawnień; 404 — nieznana organizacja. Lista/szczegóły organizacji nie ujawniają kluczy integracji ani danych użytkowników.

## Układ frontu

Osobna przestrzeń /super-admin: lista studiów → szczegóły /super-admin/organizations/{id} → Wygląd. Kontekst w adresie i kluczach cache per organizationId, nigdy jedna globalna zmienna aktywnej organizacji dla wszystkich kart. Widoczny nagłówek nazwy studia i przycisk powrotu do listy. Nie pokazywać jeszcze niepodłączonych akcji jako działających.

Podstawowe kolory/logo/teksty z branding. uiVariant wybiera specjalny layout/banner; dopuszczalne wyjątki po organizationId. Wariant UI nie jest uprawnieniem ani przełącznikiem funkcji backendu. Pole uiVariant jest w tym etapie ustawiane migracją, bez edycji w API.

## Co pozostaje

1. Ustawienia studia i kontrolowana edycja wariantów/tekstu: walidacja, rewizja, historia zmian, podział publiczne/prywatne.
2. Lokacje: zarządzanie po przypisaniu istniejących danych do organizacji; bez usuwania historii sesji.
3. Ownerzy/trenerzy: zakres członkostwa i uprawnień, zabezpieczenie ostatniego ownera, audit, odwoływanie sesji.
4. Integracje: odczyt stanu i błędów, potem jawne akcje naprawcze; żadnego ujawniania tokenów.
5. Impersonacja: odrębny projekt actor/subject/organization, powód, czas ważności, zakres, zakończenie i audyt; jeszcze brak endpointów.
6. Drugie studio: pełna izolacja biznesowa, zadań w tle, webhooków i publicznych ofert przed uruchomieniem tworzenia organizacji.

## Wdrożenie i odbiór

Osobny branch codex/bsworkout-personalization. Migracje AddOrganizationBranding i AddPlatformOrganizationRegistry muszą zostać zastosowane przed uruchomieniem nowych API. Pierwszą rolę nadać zgodnie z ADMIN-branding-access.sql. Nie wykonano tych operacji na produkcji.

Sprawdzić na staging: stary panel działa; publiczny endpoint daje BSworkout; Owner otrzymuje 403; SuperAdmin widzi listę i draft; publikacja zmienia wyłącznie wybrany profil; usunięcie roli blokuje stary JWT; nieznany ID daje 404; akcje brandingu zapisują prawdziwy ActorUserId. Testy PostgreSQL wymagają TPAY_TEST_DB. Testy modeli bez bazy nie zastępują weryfikacji migracji na kopii danych.
