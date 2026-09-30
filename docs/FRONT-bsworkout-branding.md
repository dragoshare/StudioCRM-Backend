# Front — personalizacja BSworkout (pierwszy etap)

Status: implementacja na codex/bsworkout-personalization, nie na main. Backend i kontrakt API; repozytorium frontu nie było modyfikowane.

## Dostęp i uruchomienie

- Nowa rola SuperAdmin, niezależna od Owner. Kontroler wymaga roli w JWT, a usługa ponownie sprawdza aktywne konto i aktualną rolę w bazie. Odebranie roli działa nawet dla wcześniej wydanego JWT.
- /api/auth/me już zwraca roles[]; front sprawdza obecność SuperAdmin, nie pierwsze pole role. Zwykłe logowanie i rejestracja pozostają bez zmian.
- Pierwszy administrator: ręczne, jednorazowe nadanie roli zaufanemu istniejącemu i zweryfikowanemu kontu, przez operatora bazy. Wzór: ADMIN-branding-access.sql. Skrypt domyślnie odmawia wykonania, dopóki nie podano ID użytkownika. Nie ma domyślnego konta/hasła ani publicznego endpointu nadawania roli.
- Ta paczka nie dodaje MFA/SSO. Ochronę drugim składnikiem trzeba uwzględnić przed szerokim udostępnieniem administracji; nie jest to nowe uprawnienie Ownera.
- Migracja AddOrganizationBranding tworzy cztery nowe tabele i pierwszy profil BSworkout. Nie zmienia klientów, pakietów, płatności, sesji ani Outlooka.
- Branding:ProfileId wybiera profil po stronie konfiguracji serwera (domyślnie b5100000-0000-4000-8000-000000000001). API nie przyjmuje dowolnego profileId z formularza.
- Publiczny branding domyślnie ma isCustomized=false. Front zachowuje obecny motyw. Na błąd pobrania/grafiki również zachowuje neutralny wygląd, nie blokuje logowania.
- Przed wdrożeniem zastosować migrację na właściwej bazie. Nie wykonywać migracji przeciw testowej/produkcyjnej bazie przez zgadywanie connection string.

## API

Wszystkie trasy poza publicznym odczytem wymagają SuperAdmin. Poniżej B oznacza /api/super-admin/organization/branding.

| Operacja | Endpoint | Wynik |
|---|---|---|
| Opublikowany wygląd, także przed logowaniem | GET /api/organization/branding | PublicBrandingDto |
| Szkic i podgląd | GET B/draft | profileId, revision, publishedVersion, draft, preview |
| Zapis szkicu | PUT B/draft | taki sam wynik jak GET draft |
| Publikacja | POST B/publish | opublikowany PublicBrandingDto |
| Przywrócenie poprzedniej wersji do szkicu | POST B/restore | szkic; wymaga osobnej publikacji |
| Historia publikacji | GET B/versions?page=1&pageSize=25 | tablica wersji malejąco |
| Historia operacji | GET B/audit?page=1&pageSize=25 | tablica, najnowsze najpierw |
| Upload PNG | POST B/assets | multipart/form-data, pole file; wynik id i url |

Stronicowanie: page>=1, pageSize 1..100; lista krótsza od pageSize oznacza koniec. API nie zwraca totalCount.

PUT draft:
```json
{
  "expectedRevision": 0,
  "reason": "Pierwszy wygląd BSworkout",
  "settings": {
    "applicationName": "BSworkout",
    "welcomeText": "Witaj w panelu studia",
    "primaryColor": "#123456",
    "accentColor": null,
    "lightLogoAssetId": null,
    "darkLogoAssetId": null,
    "iconAssetId": null,
    "loginImageAssetId": null
  }
}
```

Przesyłamy pełne settings; null usuwa opcjonalny element. Kolory tylko #RRGGBB. Nazwa 1–80 znaków, tekst powitalny do 240. Teksty renderować jako tekst, nigdy HTML. Kolory dotyczyć mogą tylko marki, nie statusów/błędów i kategorii trenerów.

POST publish:
```json
{ "expectedRevision": 1, "reason": "Zatwierdzony podgląd" }
```

POST restore:
```json
{ "expectedRevision": 2, "version": 1, "reason": "Powrót do poprzedniego wyglądu" }
```

Każdy zapis szkicu/publikacja/przywrócenie podnosi revision. Publikacja tworzy nową niezmienną wersję. Wartość expectedRevision pobierać z GET draft lub odpowiedzi zapisu; konflikt daje 409. Po publikacji pobrać szkic ponownie, aby uzyskać nową revision. Nie ponawiać automatycznie publikacji ze zmienionym numerem po timeout — najpierw sprawdzić stan i historię.

Publiczny wynik zawiera tylko profileId, version, isCustomized, applicationName, welcomeText, primaryColor, accentColor, lightLogoUrl, darkLogoUrl, iconUrl, loginImageUrl. Nie zawiera użytkowników, audytu, szkicu ani ustawień integracji. Odpowiedź no-store; cache frontu musi uwzględniać profil i wersję.

## Grafiki

W tej wersji przyjmujemy statyczne, nieprzeplatane PNG: maks. 5 MiB, 4096 px na bok i 8 mln pikseli. Walidacja obejmuje sygnaturę, strukturę i CRC bloków oraz ograniczoną dekompresję; metadane są usuwane. SVG, animacje i URL podane przez użytkownika nie są obsługiwane. Inne materiały należy wyeksportować do PNG przed uploadem.

Front używa ID zwróconego przez upload, nie zapisuje własnego URL w settings. Backend odrzuca ID grafiki z innego profilu. Wymagany działający R2 z publicznym adresem HTTPS; ścieżki mają postać branding/{profileId}/{assetId}.png.

Stare zasoby są zachowywane do przywracania wersji. Nie ma endpointu kasowania. Osierocone pliki po awarii storage/DB wymagają przyszłego procesu sprzątania. Szkic nie jest widoczny przez publiczne API, ale sam upload jest publicznym zasobem R2 po poznaniu URL — nie umieszczać materiałów poufnych.

## Widok frontu

1. Osobna trasa /super-admin/branding widoczna wyłącznie dla SuperAdmin.
2. Formularz nazwy, tekstu, kolorów i czterech grafik.
3. Podgląd na ciemnym/jasnym tle i telefonie, bez zmieniania opublikowanego motywu.
4. Oddzielne przyciski „Zapisz szkic” i „Opublikuj”, wymagany powód.
5. Historia wersji: „Przywróć do szkicu” → podgląd → publikacja.
6. Historia operacji z aktorem, datą i powodem. JSON przed/po prezentować jako dane, nigdy wykonywalny HTML.
7. Logo/icon/loginImage nie mogą być warunkiem renderowania formularza logowania. Brak zasobu = wygląd zastępczy.
8. 401: zaloguj ponownie; 403: brak uprawnienia; 409: odśwież po konflikcie; 400: popraw dane. Nigdy nie sygnalizować publikacji po samym zapisie szkicu.
9. Przy isCustomized=false zachować aktualny motyw ATLAS. Przy null koloru zachować dotychczasowy kolor, nie pustą wartość CSS.

## Przygotowanie pod wiele organizacji

Profil, wersje, audyt i pliki mają ProfileId. Zakres rozwiązuje IBrandingProfileContext; później może bazować na zweryfikowanej aktywnej organizacji. Zastąpienie mechanizmu kontekstu nie wymaga globalnego przechowywania logo ani przepisywania historii.

To nie włącza wielu organizacji w istniejącym CRM. Dane biznesowe nadal obsługują jedno studio. Nie ma tworzenia organizacji, przełącznika studiów, zarządzania ownerami i modułami ani nowych zasad maili.

## Odbiór i ograniczenia

Wymagane testy na stagingu: Owner/Trainer/Client bez dostępu; stary token po odebraniu SuperAdmin bez dostępu; szkic niewidoczny publicznie; publikacja i przywrócenie; konflikt dwóch administratorów; upload prawdziwych materiałów R2; zachowanie starego frontu; neutralny wygląd przy awarii CDN/API.

Testy integracyjne w repo wymagają TPAY_TEST_DB (izolowany losowy schemat). Brak tego ustawienia oznacza pominięcie testów bazodanowych, nie potwierdzenie wdrożenia. API nie zostało testowane w produkcji i nie zmieniono tam konfiguracji ani ról.
