# Adresy techniczne klientów Outlook

## Zachowanie

`Client.CalendarEmail` jest osobnym, generowanym przez backend adresem `klient-{losowy GUID}@domena`.
Nie zawiera imienia, nie zmienia się po zmianie danych klienta ani po utworzeniu konta.
`Client.Email` i `User.Email` nadal służą wyłącznie do kontaktu i logowania.
Klient bez prawdziwego e-maila może mieć kontakt Outlook i być uczestnikiem treningu.
Nie powstaje przy tym konto użytkownika ani zaproszenie do portalu.

Adres jest generowany przy zapisie klienta, gdy skonfigurowano domenę. Starszym klientom
adres nadaje endpoint przygotowania lub synchronizacja kontaktów/wydarzeń po aktywacji.
DTO klienta zwraca `calendarEmail`; formularze nie mogą go edytować.
Import i webhook rozpoznają adres techniczny oraz stary prawdziwy adres.

## Wdrożenie etapami

1. Wykonać kopię bazy i zastosować migrację `AddClientCalendarEmail` standardowym procesem wdrożenia.
   Migracja dodaje nullable `CalendarEmail` i indeks unikalny; nie zmienia istniejących maili.
2. Ustawić zmienne środowiskowe backendu (przykład dla domeny głównej):

   ```text
   Outlook__CalendarEmailDomain=atlassystem.pl
   Outlook__UseCalendarEmails=false
   ```

   Domena musi być domeną odbierającą wiadomości w Cloudflare, bez `https://`, `@` i ścieżki.
   Konfiguracja domeny generuje adresy w bazie, ale przełącznik `false` pozostawia dotychczasowe
   adresowanie kontaktów i wydarzeń. Nie oznacza to wyciszenia dotychczasowych maili Microsoftu.
3. Jako Owner wywołać `POST /api/outlook/contacts/prepare-calendar-addresses`.
   Odpowiedź: `{ "prepared": 123 }`. Ponowne wywołanie zwraca 0, jeśli nic nie trzeba uzupełnić.
   Obejmuje również klientów zarchiwizowanych. Nie łączy się z Microsoftem ani Cloudflare i nie wysyła maili.
4. W Cloudflare uruchomić Email Routing i sprawdzić odbieranie wiadomości z Outlooka.
   Dla domeny głównej catch-all obsłuży wszystkie generowane adresy. Przy subdomenie trzeba
   najpierw zapewnić odbiór każdego adresu: ten backend nie tworzy reguł Cloudflare przez API.
5. Na środowisku testowym ustawić `Outlook__UseCalendarEmails=true`, zrestartować backend.
6. Na koncie trenera wywołać `POST /api/outlook/contacts/sync-clients`.
   Jak dotychczas synchronizowane są kontakty klientów przypisanych do aktualnego trenera;
   Owner bez profilu trenera nie synchronizuje wszystkich skrzynek. Nie ma automatycznego
   zadania odświeżania kontaktów po każdym utworzeniu klienta — należy wywołać ten endpoint.
   Najpierw wyszukiwany jest kontakt po adresie technicznym, następnie po unikalnym prawdziwym
   adresie, żeby zaktualizować stary kontakt. Wspólny prawdziwy e-mail kilku klientów wymaga
   ręcznego uporządkowania starych kontaktów. Błąd wyszukania nie tworzy nowego kontaktu.
7. Sprawdzić klienta bez e-maila i klienta z kontem: wyszukanie po nazwisku, utworzenie treningu,
   zmiana terminu, cykl, odwołanie oraz import do CRM. Usunąć stare podpowiedzi Outlook
   wskazujące prawdziwy adres. Potwierdzić brak zwrotek oraz odbiór po stronie Cloudflare.
8. Po teście ustawić Drop w Cloudflare i włączyć przełącznik na środowisku docelowym.

## Stare wydarzenia i ograniczenia

Zmiana kontaktu nie aktualizuje uczestników istniejących wydarzeń. W trybie technicznym
backend sprawdza uczestników wydarzenia przed pełną aktualizacją przez synchronizację sesji.
Wydarzenie z uczestnikami innymi niż zapisane adresy techniczne (poza zasobami) wymaga
osobnej migracji: nie jest automatycznie przepisywane, ponieważ Microsoft może wysłać
anulowania na stare adresy. Błąd jest zwracany z jasną informacją.

Bezpośrednie zmiany w Outlooku, usuwanie starych wydarzeń oraz automatyczne poprawianie
ich tytułów przez istniejący webhook mogą nadal powiadamiać starych uczestników.
Ta zmiana nie blokuje poczty Microsoftu globalnie. Ochrona działa dla wydarzeń mających
adresy techniczne. Migrację starych serii wykonujemy osobno po próbie na danych testowych.

Nie zmieniać domeny po nadaniu adresów bez osobnego planu migracji: istniejące adresy są
zachowywane i stara domena musi nadal odbierać pocztę. Cofnięcie flagi po migracji kontaktów
może ponownie udostępnić prawdziwe adresy w kontaktach; nie jest bezpiecznym wyciszeniem.

## Weryfikacja

Testy jednostkowe sprawdzają generowanie, trwałość, blokadę używania adresu technicznego
do logowania, odbiorców wydarzeń, kontakty bez prywatnego e-maila i błędy odczytu Graph.
Test PostgreSQL `CalendarAddressMappingTests` sprawdza import mieszanych adresów i webhook.
Uruchamia się po ustawieniu `TPAY_TEST_DB` na dedykowaną bazę testową; tworzy izolowany schemat.
Testy HTTP korzystają z atrap; nie wysyłają wiadomości i nie modyfikują rzeczywistych kalendarzy.
