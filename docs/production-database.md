# Produkcyjna baza danych

## Stan przygotowania

- `DefaultConnection` wskazuje projekt Supabase `cngkbpbkpgreehrigdye`.
- Poprzednie 29 tabel CRM zapisano w schemacie `archive_preprod_20260911`.
- Schemat `public` utworzono ponownie z 46 aktualnych migracji.
- Baza zawiera 3 role aplikacyjne, 0 użytkowników i 0 danych demonstracyjnych.
- Role Supabase `anon` i `authenticated` nie mają dostępu do schematu ani tabel CRM.
- Działający Render nadal korzysta z `TestConnection`.

Archiwum znajduje się w tym samym projekcie Supabase. Chroni przed pomyłką przy
czyszczeniu, ale nie zastępuje kopii przechowywanej poza projektem. Można je usunąć
dopiero po wykonaniu i sprawdzeniu zewnętrznego backupu.

## Konfiguracja

Sekrety są przekazywane przez zmienne środowiskowe. Nie wpisujemy ich do
`appsettings.json`, dokumentacji ani frontu.

Najważniejsze zmienne produkcyjnego backendu:

```text
Database__ConnectionName=DefaultConnection
ConnectionStrings__DefaultConnection=...
Database__ApplyMigrationsOnStartup=false
Seed__DemoData=false
Jwt__Key=...
```

Pozostałe sekrety integracji (`Resend`, `Outlook`, `CloudflareR2`, `Tpay`) również
pozostają wyłącznie w Environment usługi backendowej.

## Migracje i wdrożenie

Produkcja nie wykonuje automatycznych migracji podczas zwykłego startu API.
Jeżeli schemat jest nieaktualny, API przerwie start z komunikatem o oczekujących
migracjach. Migrację wykonujemy świadomie przed uruchomieniem nowej wersji:

```text
dotnet ef database update --project StudioCRM.Infrastructure --startup-project StudioCRM.Api
```

Polecenie musi otrzymać produkcyjny connection string i właściwe środowisko. Przed
migracją wymagany jest aktualny backup oraz próba migracji na jego odtworzonej kopii.
Development ma `ApplyMigrationsOnStartup=true` w `appsettings.Development.json`.

## Supabase

CRM komunikuje się z bazą wyłącznie przez backend ASP.NET. Front nie powinien używać
Supabase REST ani GraphQL do tabel CRM. W panelu Supabase należy wyłączyć Data API dla
tego projektu albo usunąć `public` z Exposed schemas. Po każdej migracji kontrolujemy,
że `anon` i `authenticated` nadal nie mają grantów.

Przed uruchomieniem:

1. Włączyć backupy Supabase odpowiednie dla wybranego planu i wykonać kopię zewnętrzną.
2. Odtworzyć kopię w osobnym projekcie i sprawdzić logowanie, klientów, sesje i płatności.
3. Utworzyć osobne dane dostępowe runtime i migracyjne, jeśli plan Supabase na to pozwala.
4. Wymienić hasła baz, klucz JWT i wszystkie sekrety, które wcześniej znajdowały się w Git.
5. Ustawić alerty Render/Supabase oraz ograniczyć dostęp do paneli administracyjnych.
6. Dopiero wtedy przełączyć Render z `TestConnection` na `DefaultConnection`.

Usunięcie sekretów z bieżącego pliku nie usuwa ich z historii Git. Rotacja jest
obowiązkowa. Czyszczenie historii repozytorium wymaga osobnej, skoordynowanej operacji,
ponieważ zmienia historię dla wszystkich kopii projektu.
