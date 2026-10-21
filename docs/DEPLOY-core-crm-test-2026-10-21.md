# Kandydat do wdrożenia testowego — 21.10.2026

Branch: `codex/platform-organizations`. Zawiera `main` do commita `49264be` oraz CoreCrm dla drugiej organizacji. Main i produkcja nie zostały zmienione. Niezacommitowane zmiany z głównego katalogu nie są częścią tej paczki.

## Render — testowy backend

- Usługa: `studiocrm-backend` (`https://studiocrm-backend.onrender.com`). Nie wybierać `atlas-bsworkout-production`.
- Branch: `codex/platform-organizations`; wdrożyć wskazany finalny commit tej paczki.
- `Database__ConnectionName=TestConnection` oraz `ConnectionStrings__TestConnection` wskazujące dotychczasową bazę testową. Nie kopiować produkcyjnego connection stringa.
- Jeżeli migracje wykonuje startup aplikacji: `Database__ApplyMigrationsOnStartup=true`. Jeżeli są wykonywane osobno, zastosować je przed uruchomieniem nowej wersji.
- Nowa migracja względem aktualnego main: `20261018072226_ScopeStudioSettingKeys`. Pozostałe zaległe migracje zależą od stanu bazy testowej — sprawdzić logi startu. Nie resetować bazy i nie uruchamiać importów danych produkcyjnych.
- Po deployu przekazać potwierdzenie i SHA wdrożonego commita; następny krok to test API.

## Weryfikacja API po wdrożeniu (bez frontendu)

1. Logowanie istniejącego Ownera Atlas Demo i kontrola `GET /api/auth/me` oraz `GET /api/organization`: właściwe organizationId, rola Owner i accessMode CoreCrm.
2. Logowanie SuperAdmina: rejestr organizacji i użytkowników; brak globalnego dostępu zwykłego Ownera.
3. Odczyty BSworkout i Demo: lokalizacje, trenerzy, klienci, pakiety, ustawienia i zestawienia; próby odczytu cudzych identyfikatorów muszą być odrzucone lub nie zwracać danych.
4. Na oznaczonych danych testowych Demo: klient/trener, pakiet, ręczna wpłata, sesja, obecność i odjęcie wejścia; kontrola salda oraz widoczności po zalogowaniu jako Trainer/Client.
5. Serie trenera: tworzenie i usuwanie w CRM. Synchronizacja z Outlookiem pozostaje zablokowana w Demo. Dostosowano dostęp do nowego kontrolera serii z main.
6. Sprawdzenie blokady Tpay, publikowania ofert/zajęć i zaproszeń w nowej organizacji. Testy nie powinny uruchamiać realnych płatności ani wiadomości.
7. BSworkout: odczyty i kontrola kontraktów API. Testy rzeczywistej synchronizacji Outlook wymagają oddzielnego scenariusza na kalendarzu testowym, bo mogłyby zmienić zdarzenia zewnętrzne.

Brak frontendu nie blokuje powyższych testów, ale nie potwierdzimy wyglądu, routingu, zapisu tokenów ani obsługi odpowiedzi przez UI. Testy API po deployu pozostają do wykonania. Sam push nie jest potwierdzeniem wdrożenia.

## Przed merge do main / produkcją

PR pozostaje draft do czasu sprawdzenia wdrożonego testowego API. Bez auto-merge. Potrzebne: backup produkcji i plan odtworzenia, sprawdzenie auto-deploy, przegląd migracji oraz ustalony moment wdrożenia. Migracja zachowuje wartości ustawień; rollback indeksu nie jest bezpieczny po dodaniu takich samych kluczy w różnych studiach. Nie usuwać ustawień, żeby wymusić rollback.

## Wynik lokalnej weryfikacji

Pełny przebieg: 470 poprawnych testów i 1 błąd niepełnych danych testu importu zajęć grupowych (brak roli Trainer). Po dodaniu wyłącznie roli do fixture ten test przeszedł w osobnym przebiegu. Łącznie zweryfikowano 471 przypadków; żadnego nie pominięto. Kompilacja poprawna. Raporty TRX zapisane lokalnie w artifacts/test-results. Testów wdrożonego API ani frontendu jeszcze nie wykonano.
