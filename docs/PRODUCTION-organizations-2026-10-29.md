# Produkcyjne wdrożenie organizacji — 29.10.2026

Aktualne adresy potwierdzone przez właściciela: produkcja https://studiocrm-backend.onrender.com, test https://atlas-bsworkout-production.onrender.com. Starsze dokumenty mają odwrotne oznaczenia; nie stosować ich adresów bez tej korekty.

Do brancha codex/platform-organizations włączono main 55c6be4 (ponowny import zdarzenia po usunięciu sesji). Uzupełniono rolę Trainer w fixture OutlookSettlementSafetyTests, bez zmiany autoryzacji aplikacji.

## Baza i przygotowanie

- Produkcyjny projekt: cngkbpbkpgreehrigdye.
- Świeży backup public wykonano 29.10; 1055122 bajty, zapis SHA256, sprawdzony pg_restore --list.
- Odtworzono go w osobnej lokalnej bazie, bez uruchamiania API, workerów ani Graph.
- Jedyna oczekująca migracja: 20261018072226_ScopeStudioSettingKeys.
- Próba migracji zachowała identyczne hashe zawartości wszystkich 47 tabel (poza historią migracji).
- Utworzono Atlas Demo 98aae707-cfc4-4eaf-b4e0-d1fe2b575ad8 w Draft i Ownera dragoshare@gmail.com (userId 104). Bez globalnych ról i bez kopiowania kont/płatności z testu. Hasło wyłącznie w lokalnym prywatnym pliku, poza Git.
- Przygotowanie zapisano jako OrganizationBootstrapPrepared w audycie, z powodem autoryzowanego wdrożenia przez właściciela.

## Kolejność przełączenia

1. Zatrzymać produkcyjną usługę na krótkie okno wdrożenia. Powód: stary main używa ON CONFLICT(Key) w OutlookInstanceBinding, a migracja usuwa ten globalny indeks. Nie pozostawiać starej aplikacji pracującej na nowym indeksie.
2. Wykonać kontrolowaną migrację produkcji. Nie kasować bindingu Outlooka ani tokenów. Zachować konfigurację produkcyjnego WebhookUrl.
3. Wdrożyć zatwierdzony commit organizacji. Nie podmieniać connection stringa na testowy; Seed__DemoData=false. Po kontrolowanej migracji Database__ApplyMigrationsOnStartup=false.
4. Aktywować Atlas Demo i sprawdzić logowanie Ownera, Auth/me oraz GET /api/organization: wskazane ID, CoreCrm.
5. Sprawdzić działanie BSworkout / FullCrm i izolację danych. Front musi pobierać organizację z tokenem oraz respektować CoreCrm.

Na moment przygotowania tego dokumentu migracja produkcyjna i deploy NIE zostały wykonane. Nie traktować utworzenia konta Draft jako potwierdzonego logowania do aktywnego CRM.

## Ograniczenia

CoreCrm nie udostępnia Outlooka, Tpay, zaproszeń, publicznej sprzedaży/zapisów ani edycji brandingu Ownera. Dane drugiego studia są oddzielone, ale backend i baza współdzielą zasoby. Nie prowadzić testów obciążeniowych lub niekontrolowanych prób na produkcji.

## Testy

Pełna regresja: 481 testów, 480 poprawnych i 1 błąd fixture nowego testu z main. Po dodaniu brakującej roli Trainer ponowiono cały OutlookSettlementSafetyTests: 5/5 poprawnych, zero pominiętych. Błąd uruchomienia wcześniejszej próby bez lokalnego PostgreSQL nie był wynikiem aplikacji i został zastąpiony właściwym przebiegiem.
