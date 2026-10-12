# Izolacja danych — warstwa persystencji, 12.10.2026

Branch: codex/platform-organizations. Ten etap NIE uruchamia dostępu CRM dla drugiego studia.

## Wprowadzone zabezpieczenia

- OrganizationId w danych operacyjnych (klienci, trenerzy, lokalizacje, sesje, pakiety, płatności, umowy, rozliczenia, powiadomienia i integracje).
- Stały kontekst organizacji dla instancji DbContext; filtry odczytu połączone z dotychczasowym ukrywaniem archiwum.
- Odczyty archiwum zachowują ograniczenie organizacji, także etykiety audytu i seeder.
- Generator klucza organizacji, kontrola zapisu i token współbieżności blokują zapis, aktualizację i usuwanie danych innej organizacji przez standardowy SaveChanges.
- Dodatkowe złożone klucze obce (ID + OrganizationId) uniemożliwiają powiązania danych różnych studiów również w SQL. Dotychczasowe relacje i zachowania usuwania pozostają.
- Audyt zawiera organizację; aktualizacje SQL Outlooka mają warunek organizacji.

## Migracja

20261011205449_AddOrganizationDataBoundaries przypisuje istniejące dane do BSworkout bez zmiany ich identyfikatorów. Nie tworzy Atlas Demo, użytkowników ani pakietów. Usunięto wygenerowane przez EF operacje ponownego wstawiania seedów, aby zachować istniejące ustawienia i nagrody. Migracja została wykonana wyłącznie na lokalnej kopii platform_org_migration_trial; produkcja i zdalna baza testowa pozostają nietknięte.

## Granice tego etapu — konieczne przed aktywowaniem drugiego studia

- Runtime nadal domyślnie używa BSworkout. Nie dodano jeszcze wyboru organizacji ani organizationId do tokenów i Auth/me.
- Globalne UserRoles i unikalne powiązania profili użytkownika wymagają przejścia na autoryzację członkostwa per organizacja. Samo przypisanie Ownera do Draft nie nadaje dostępu CRM.
- Zadania w tle, webhooki Outlook/Tpay, publiczne zapisy, cache i magazyn plików wymagają jawnego rozwiązywania i weryfikowania organizacji przed utworzeniem kontekstu.
- Globalne konta użytkowników i ich bezpieczeństwo nie stają się danymi studia. Trzeba odrębnie zweryfikować endpointy operujące na tych kontach.
- Pozostałe ograniczenia unikalności (np. ustawienia i profile trenerów) trzeba dostosować do wielu organizacji.
- Nie wprowadzono PostgreSQL RLS. Filtry EF nie zabezpieczają dowolnego SQL przed odczytem; SQL musi mieć jawne ograniczenie organizacji.
- Domyślna wartość OrganizationId w bazie zapewnia migrację starego schematu; przed pełnym uruchomieniem wielu studiów należy ocenić jej usunięcie, żeby pominięty kontekst nie trafiał do BSworkout.

## Walidacja

Pełny zestaw 200 testów przeszedł na lokalnym PostgreSQL, w tym trzy nowe testy dwóch organizacji. Dodatkowy test obejmuje archiwalne pakiety i odrzucanie jawnie obcego OrganizationId. Testy używają osobnych losowych schematów, nie danych produkcyjnych.

## Odświeżanie sesji — kolejny etap

Migracja 20261012060227_BindRefreshTokensToOrganization dodaje do RefreshTokens OrganizationId (nullable) i ScopeVersion (0 dla istniejących rekordów). Nowe sesje mają wersję 1: organizację BSworkout albo null dla panelu platformy. Odświeżenie sprawdza zapisany kontekst przed unieważnieniem starego tokena. Sesje starszego formatu są obsługiwane tylko w kontekście BSworkout i przy rotacji otrzymują jawny kontekst. Obca organizacja, nieznana wersja lub próba użycia sesji platformy jako sesji Ownera są odrzucane. W tym etapie nadal nie ma przełączania organizacji ani autoryzacji opartej na członkostwach.

Walidacja: 204 testy, wszystkie poprawne; migracja wykonana na lokalnej kopii platform_org_migration_trial. Zdalne bazy bez zmian.