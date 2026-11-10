# Audyt API i zgodność z main — 25.10.2026

Backend testowy wskazany i potwierdzony przez użytkownika: https://atlas-bsworkout-production.onrender.com. Nazwa URL jest myląca — nie określa przeznaczenia bazy. Atlas Demo ma ID 24c277c6-d22e-4039-bd71-801aa7da38fc. Odczytowa kontrola bazy TestConnection dotyczyła projektu qlidxvbcblfgldscwrun.

## Potwierdzone przez wdrożone API

- Owner, SuperAdmin, testowy Trainer i testowy Client logują się poprawnie. Konta studia wskazują Atlas Demo / CoreCrm.
- Odczyty panelu trenera i klienta oraz rozliczeń działają.
- Zakup 4 wejść / 400 PLN, ręczna fikcyjna wpłata i zakończenie sesji: 1 wejście wykorzystane, 3 pozostałe, wpłacone 400 PLN, saldo 0. Powtórzenie wpłaty z tym samym kluczem zwróciło ten sam paymentId i nie utworzyło drugiej wpłaty.
- Owner Demo nie odczytuje istniejących rekordów BSworkout: client 42, trainer 11, location 3, package 11, session 460 — HTTP 404. Rozliczenia klienta 42 zwracają 400 / Client not found or archived. Trainer Demo również otrzymuje 404 dla klienta 42. Istnienie i przynależność tych identyfikatorów potwierdzono odczytowo w bazie, bez pobierania danych osobowych.
- Client nie odczytuje ustawień Ownera, Trainer nie odczytuje rejestru SuperAdmina — HTTP 403.
- Owner Demo nie może wywołać zaproszeń, połączenia Outlook ani checkoutu Tpay — HTTP 403. Publikacja oferty jest odrzucana HTTP 400.
- SuperAdmin widzi Atlas Demo / Active / CoreCrm, BSworkout / LegacyActive / FullCrm i studio test / Draft / Unavailable.

## Znalezione problemy i poprawki na branchu

1. Połączenie aktualnego main do c9184e8: OutlookInstanceBinding używał ON CONFLICT(Key), niezgodnego z indeksem OrganizationId+Key. Poprawiono insert oraz cel konfliktu. Bezpośrednie wywołanie usługi dla nowej organizacji jest odrzucane przed cache i zapisem; test obejmuje brak wywołań Graph.
2. Wdrożone API odrzucało serię Demo z powodu niedostępnego Outlooka. Nowe organizacje zapisują teraz serię wyłącznie w CRM, z OutlookSeriesSynced=false i ostrzeżeniem. Wymóg udanej synchronizacji dla BSworkout pozostaje bez zmian. Test obejmuje tworzenie serii i usuwanie przyszłych wystąpień zarówno dla legacy, jak i Demo.

## Outlook i dane audytu

25.10 odczyt READ ONLY potwierdził 3 nieaktywne integracje, brak access/refresh tokenów, 3 nieaktywne subskrypcje. Nie wykonywano wywołań Microsoft Graph ani wysyłki wiadomości. Po ponownym deployu testowy backend powinien zachować Outlook__EmergencyStop=true oraz własny WebhookUrl. Nie podłączać prawdziwych kalendarzy podczas audytu.

Pozostawiono oznaczone dane AUDYT-20261021-db9bf71c: lokalizacja 5, trener 36, klient 149, pakiet 61, zakup 185, fikcyjna wpłata 154, sesja 638. To dane testowe wpływające na statystyki Atlas Demo. Nie usuwano historii finansowej ani danych BSworkout. Nieudana próba serii została odrzucona przez istniejący mechanizm kompensacji; ponowne sprawdzenie po deployu jest wymagane.

## Granice weryfikacji / warunki przed main

- Zmiany main włączono do brancha organizacji; main nie został zmieniony.
- Poprawki z tego audytu wymagają nowego deployu testowego oraz sprawdzenia serii przez API.
- Nie testowano UI ani rzeczywistego kalendarza Microsoft / płatności Tpay.
- Test odczytu cudzych rekordów przez API wykonano w kierunku Demo -> BSworkout. Przeciwny kierunek jest objęty testami lokalnymi, nie potwierdzono go zdalnym logowaniem Ownera BSworkout.
- Przed produkcją potrzebne potwierdzenie poprawnego deployu, backup oraz kontrola konfiguracji instancji Outlooka. Nie uznawać samego merge bez konfliktów za pełną zgodność.

## Wyniki testów lokalnych

Pełny przebieg po połączeniu main: 479 testów, 478 poprawnych, 1 niepoprawny. Przyczyną był brak roli Trainer w danych testu OutlookTitleWriteBackTests dodanego na main; uzupełniono fixture bez zmiany uprawnień aplikacji.

Po poprawieniu fixture i obsługi serii ponowiono pięć zestawów: OutlookInstanceBindingTests, OutlookTitleWriteBackTests, TrainerPortalSeriesTests, OrganizationCoreCrmTests oraz OrganizationRequestContextTests. Wynik: 28/28 poprawnych, zero pominiętych. Obejmują nowy test serii Demo. Nie przedstawiamy tego jako ponownego pełnego przebiegu całego zestawu.

Raporty TRX lokalnie: artifacts/test-results/main-core-crm-2026-10-25.trx i main-core-crm-recheck-2026-10-25.trx. Testy PostgreSQL uruchomiono w izolowanych schematach lokalnej bazy; bez połączeń z prawdziwym Microsoft Graph.
