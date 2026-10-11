# Uruchomienie B&S Workout i rozwój platformy — 11.10.2026

## Produkcja i realne dane

Produkcja pozostaje na zatwierdzonym main. Backend: https://atlas-bsworkout-production.onrender.com. Front: https://www.atlassystem.pl. Dotychczasowa baza produkcyjna zostaje; nie kopiujemy danych z testu i nie uruchamiamy ponownie importerów czyszczących. Wprowadzane teraz prawdziwe dane będą zachowane przy dodaniu OrganizationId do istniejących rekordów BSworkout.

Przed rozpoczęciem codziennej pracy potwierdzić:
1. Front produkcyjny wskazuje API produkcyjne; login Ownera i odczyt rzeczywistych klientów/pakietów.
2. Zatwierdzony commit i komplet migracji; brak oczekujących migracji. Seed demo wyłączony; osobny JWT.
3. Backup oraz procedura odtworzenia. Próba każdej większej migracji na świeżej kopii danych przed wdrożeniem, nie tylko na starym snapshotcie.
4. Świadoma konfiguracja synchronizacji kalendarzy i zakresu dat. `Outlook__AutoCreateSessionsFromImportedEvents` nie jest skutecznym wyłącznikiem w aktualnym kodzie. Nie zakładać, że false blokuje import.
5. Rzeczywiste stawki/umowy trenerów, przypisania klientów, płatności i początek okresów. Cennik jest w bazie, zakupy nie zostały automatycznie przydzielone.
6. R2 jest obecnie skopiowane z testu — ustalić rozdzielenie plików przed realnymi uploadami. Maile mają prawidłowy frontend i zweryfikowanego nadawcę.
7. Tpay pozostaje sandbox; nie przedstawiać płatności jako produkcyjnych do czasu uruchomienia i weryfikacji właściwego konta. Frontowe płatności wymagają osobnego odbioru.

## Pierwsza paczka organizacji

Branch codex/platform-organizations zawiera dotychczasową personalizację i main. Wprowadza rejestr z Draft/LegacyActive, audyt, wyszukiwanie kont, listę kadry i przypisywanie Ownerów do Draft. To NIE jest jeszcze pełna izolacja wielu studiów. Nie kierować produkcji automatycznie na rozwijany branch.

Wdrożenie testowe: backup testu → migracje zatwierdzonego commita → backend tego commita → Atlas Demo przez POST → testy frontu. Bez drugiego rzeczywistego studia i bez publicznej rejestracji do Draft.

Wdrożenie produkcyjne później: świeża kopia produkcji → próba migracji i testy zachowania danych → backup → kontrolowane migracje → zatwierdzony release/main → smoke test. Atlas Demo i superadmin@atlas.test nie są seedowane przez migracje i nie trafiają do produkcji.

W razie błędu po rozpoczęciu normalnej pracy nie przywracać starego dumpa w ciemno (utrata nowych danych). Preferować poprawkę naprzód lub zgodny rollback aplikacji; odtworzenie danych wymaga ustalenia okna i zachowania późniejszych zapisów.
