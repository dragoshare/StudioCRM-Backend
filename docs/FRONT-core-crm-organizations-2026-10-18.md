# Atlas Demo — operacyjny CRM (2026-10-18)

Ten dokument aktualizuje zakres opisany w FRONT-organization-activation-2026-10-13.md. Branch: codex/platform-organizations. Aktywne nowe studia przechodzą z SetupOnly do CoreCrm po wdrożeniu kodu i migracji. Nie trzeba ponownie aktywować Atlas Demo ani przenosić jego danych.

## Identyfikacja i dostęp

Po zalogowaniu używaj organizationId z odpowiedzi logowania / GET /api/auth/me. GET /api/organization z Bearer tokenem zwraca organizację bieżącego konta, uiVariant, branding i accessMode. Wywołanie anonimowe nadal dotyczy domyślnego BSworkout — nie używać go do identyfikacji zalogowanego studia.

- CoreCrm: aktywna nowa organizacja; zakres poniżej.
- FullCrm: istniejące BSworkout.
- Unavailable: organizacja niedostępna, np. Draft.

Jedno konto należy do jednego studia. Nie dodajemy przełącznika organizacji ani organizationId do żądań operacyjnych. Backend wyznacza kontekst z uwierzytelnionego konta i weryfikuje go przy każdym żądaniu. Same flagi w UI nie zastępują autoryzacji API.

## Dostępne moduły CoreCrm

Obowiązują istniejące DTO i uprawnienia Owner/Trainer/Client z OpenAPI wdrożonego backendu:

- Lokalizacje, trenerzy, klienci oraz archiwizacja i zakończenie współpracy.
- Katalog pakietów, przypisanie pakietu klientowi, historia i pakiety okresowe.
- Sesje i serie, uczestnicy, obecności, rozliczanie wejść, korekty.
- Ręczne wpłaty personelu, saldo, koszty oraz rozliczenia trenera.
- Zestawienia Ownera, dashboard API, powiadomienia wewnętrzne, aktywność i milestones.
- GET/PUT /api/owner/settings — ustawienia operacyjne, osobne wartości dla studia.
- Panel trenera z wyjątkiem zaproszeń; panel klienta z wyjątkiem żądania zmiany emaila i deklarowania płatności.
- POST /api/auth/register (Owner): ręczne utworzenie konta Client/Trainer, ze wskazaniem lokalizacji/trenera z tego studia. Bez wysyłania zaproszenia. Osobny nieużywany email.
- POST /api/trainers: istniejący kontrakt tworzenia osobnego konta trenera. Dodanie profilu trenera do już istniejącego konta Ownera nadal wymaga osobnego rozszerzenia.
- Zmiana hasła, avatary oraz podstawowe dane konta.

Automatyczne kończenie sesji i obsługa pakietów okresowych działają oddzielnie dla każdej aktywnej organizacji. Błąd jednego studia nie blokuje przetwarzania pozostałych. Powiadomienia Ownerów i kontakt do Ownera w panelu klienta są ograniczone do właściwego studia.

## Nadal niedostępne w CoreCrm

Ukryj/wyłącz te akcje, a nie całe podstawowe moduły:

- Outlook: połączenie, import, webhooki i synchronizacja. Sesja zapisana w CRM nie tworzy zdarzenia Outlooka. Odpowiedź serii może zawierać informację o braku synchronizacji.
- Tpay, konfiguracja płatności, publiczna sprzedaż i samodzielny zapis na zajęcia grupowe.
- Publikacja oferty/zajęć: IsPubliclyAvailable=true i IsPubliclyBookable=true są odrzucane dla nowych studiów. Ręczna praca w grafiku pozostaje dostępna.
- Zaproszenia i publiczna rejestracja, zmiana emaila klienta wymagająca wiadomości, deklaracja płatności z panelu klienta.
- Edycja brandingu przez Ownera — wymaga dalszego etapu. Odczyt brandingu jest dostępny.

Zablokowane akcje kontrolerów zwracają HTTP 403 z code=OrganizationCapabilityUnavailable. Walidacja publikacji zwraca istniejący błąd walidacji akcji. Nie interpretować CoreCrm jako pełnej gotowości integracji zewnętrznych.

## Aktywacja kolejnych studiów

Istniejący endpoint aktywacji SuperAdmina przyjmuje expectedRevision, reason oraz confirmCoreCrm=true. Starsze confirmSetupOnly=true jest zachowane dla zgodności frontu. Odpowiedź aktywacji zawiera już accessMode=CoreCrm. Atlas Demo jest aktywne; nie aktywować go drugi raz.

## Wdrożenie na backend testowy

1. Wdrożyć ten branch na usługę testową i testową bazę. Nie zmieniać usługi produkcyjnej BSworkout.
2. Zastosować migrację 20261018072226_ScopeStudioSettingKeys standardowym mechanizmem wdrożeniowym. Zamienia unikalność klucza ustawień na parę OrganizationId+Key; nie usuwa danych ani ustawień BSworkout.
3. Sprawdzić logi migracji i zalogować się ponownie jako Owner Atlas Demo.
4. Sprawdzić GET /api/organization: właściwy organizationId i CoreCrm. Zweryfikować lokalizację, trenera, klienta, pakiet, ręczną wpłatę, sesję i odjęcie wejścia po jej zakończeniu.
5. Sprawdzić te same listy kontem BSworkout — dane Demo nie mogą się tam pojawić.

Nie cofać migracji bez sprawdzenia danych: po utworzeniu tego samego klucza w dwóch studiach poprzedni globalny indeks nie da się odtworzyć. Powrót do poprzedniego kodu może pozostawić nowy indeks; nie usuwać ustawień automatycznie.

## Zakres weryfikacji

Testy PostgreSQL obejmują izolację ustawień, sprzedaży i wpłat, rozliczenie wejścia, rejestrację konta bez globalnej roli, kontakt i powiadomienia właściwego Ownera, niezależne scope zadań cyklicznych, blokadę publikacji oraz wykonanie faktycznych operacji migracji bez utraty ustawień. Testy lokalne nie zastępują sprawdzenia wdrożenia i frontendu.

Wynik końcowy lokalnej regresji: 447/447 testów przeszło po połączeniu z aktualnym branchem, 0 pominiętych (18.10.2026, PostgreSQL).
