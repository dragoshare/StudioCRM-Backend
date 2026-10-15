# Zabezpieczenie bazy produkcyjnej — plan wdrożenia (15.10.2026)

Stan wyjściowy (odczyt): role `anon`/`authenticated` nie mają dostępu do schematów CRM; połączenie z SSL
(`Require`), ale `Trust Server Certificate=true`; aplikacja łączy się jako `postgres` (BYPASSRLS); RLS wyłączone
na 48 tabelach; tokeny sesji, resetu hasła i Outlooka zapisane jawnie; schemat `archive_preprod_20260911`
w tej samej bazie. Sekretów nie zapisujemy w tym dokumencie ani w repozytorium.

Kolejność ma znaczenie. Każdy krok można wykonać osobno; po każdym sprawdź `/health` i logowanie do CRM.

## 1. Wdrożenie kodu (hashowanie i szyfrowanie tokenów)

Kod zapisuje nowe tokeny odświeżania i resetu hasła jako SHA-256, a stare (jawne) nadal działają do wygaśnięcia.
Tokeny Outlooka są szyfrowane AES-256-GCM, gdy ustawiony jest klucz; bez klucza działa jak dotąd.

1. Wygeneruj klucz (PowerShell, lokalnie; nie wklejaj go nigdzie poza Renderem i menedżerem haseł):
   ```powershell
   $b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
   ```
2. Render → studiocrm-backend → Environment: dodaj `Security__TokenEncryptionKey` = wygenerowana wartość.
   Zachowaj kopię w menedżerze haseł. Utrata klucza = trenerzy muszą ponownie podłączyć Outlooka.
3. Wdróż kod. Tokeny Outlooka zaszyfrują się przy najbliższym odświeżeniu (co ok. godzinę).
4. Po wdrożeniu zahashuj istniejące tokeny (SQL Editor Supabase) — od tej chwili odczyt bazy nie daje działających sesji:
   ```sql
   UPDATE "RefreshTokens" SET "Token" = encode(sha256(convert_to("Token", 'UTF8')), 'hex')
   WHERE "Token" !~ '^[0-9a-f]{64}$';
   UPDATE "PasswordResetTokens" SET "Token" = encode(sha256(convert_to("Token", 'UTF8')), 'hex')
   WHERE "Token" !~ '^[0-9a-f]{64}$';
   ```

Zaproszenia (`Invitations.Token`) zostają jawne, bo panel pokazuje link zaproszenia do skopiowania; są ważne
krótko i jednorazowe. Do rozważenia później: pokazywanie linku tylko przy tworzeniu/ponownym wysłaniu.

## 2. Osobna rola aplikacji zamiast `postgres` + RLS

Aplikacja dostaje tylko odczyt/zapis danych (bez DDL i bez omijania RLS). Migracje dalej wykonuje `postgres`.
W SQL Editor (hasło ustaw własne, silne; nie zapisuj go w repozytorium):

```sql
CREATE ROLE crm_app LOGIN PASSWORD '<silne-haslo>' NOINHERIT;
GRANT USAGE ON SCHEMA public TO crm_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO crm_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO crm_app;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO crm_app;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO crm_app;

-- RLS jako druga warstwa: włączone na wszystkich tabelach, dostęp tylko dla crm_app.
DO $$
DECLARE t record;
BEGIN
  FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t.tablename);
    EXECUTE format('DROP POLICY IF EXISTS crm_app_all ON public.%I', t.tablename);
    EXECUTE format('CREATE POLICY crm_app_all ON public.%I FOR ALL TO crm_app USING (true) WITH CHECK (true)', t.tablename);
  END LOOP;
END $$;
```

Po migracji dodającej nową tabelę uruchom ponownie blok `DO $$ ... $$` (nowa tabela musi mieć RLS i politykę).

Przełączenie: Render → `ConnectionStrings__DefaultConnection` z użytkownikiem `crm_app.<project-ref>` (format jak
dotychczasowy użytkownik puli Supavisor) i nowym hasłem. Sprawdź logowanie, listę klientów, zakończenie sesji
i wpłatę. Gdyby coś nie działało — przywróć poprzedni connection string (rola `postgres` nadal działa).
Po potwierdzeniu: zmień hasło `postgres` w Supabase (było używane lokalnie do diagnozy) i trzymaj je tylko
do migracji.

## 3. Weryfikacja certyfikatu serwera

1. Supabase → Project Settings → Database → SSL Configuration → pobierz certyfikat CA.
2. Dodaj go do repozytorium jako `StudioCRM.Api/certs/supabase-ca.crt` (to publiczny certyfikat, nie sekret)
   i ustaw kopiowanie do wyniku publikacji (zgłoś — dodam wpis w `.csproj`).
3. W connection stringu zamień `SSL Mode=Require;Trust Server Certificate=true` na
   `SSL Mode=VerifyFull;Root Certificate=/app/certs/supabase-ca.crt`.

## 4. Ograniczenie dostępu sieciowego

Supabase → Project Settings → Database → Network Restrictions: zezwól tylko na adresy wychodzące Rendera
(Render → service → Settings → Outbound IP Addresses) oraz Twój stały adres (do SQL Editor nie jest potrzebny —
działa przez panel). Uwaga: diagnozy wykonywane z innego komputera wymagają wtedy dopisania jego adresu.

## 5. Porządki i konta

- Usuń `archive_preprod_20260911` dopiero po sprawdzeniu kopii zewnętrznej:
  `DROP SCHEMA archive_preprod_20260911 CASCADE;`
- Sprawdź plan Supabase: codzienne kopie / PITR; raz w miesiącu wykonaj kopię poza Supabase i próbę odtworzenia.
- Włącz MFA na kontach Supabase, Render, GitHub i hostingu frontu.
