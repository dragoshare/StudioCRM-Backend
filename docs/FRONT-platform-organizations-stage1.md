> Aktualna notatka przekazania: [FRONT-superadmin-handoff-2026-10-12.md](FRONT-superadmin-handoff-2026-10-12.md).

# Organizacje — etap 1, 11.10.2026

Implementacja: `codex/platform-organizations`, wyprowadzony z personalizacji i zawierający main. Ten dokument zastępuje wcześniejsze propozycje dotyczące NOWYCH endpointów. Nie oznacza wdrożenia na Render.

## Granica bezpieczeństwa

To rejestr organizacji i przygotowanie członkostw, jeszcze nie wieloorganizacyjny CRM. Istniejący BSworkout ma status `LegacyActive`; nowa organizacja zawsze `Draft`, `crmAccessEnabled=false`. Nie ma endpointu aktywacji ani wyboru organizacji. Nie budować działającego przycisku „Wejdź do CRM” dla Draft. Przypisanie Ownera zapisuje członkostwo i audyt, NIE dopisuje globalnej roli Owner ani nie daje dostępu do BSworkout.

Zarządzanie Ownerami BSworkout jest celowo zablokowane (409), dopóki autoryzacja CRM nadal używa globalnych ról. Nie wolno prezentować tej blokady jako udanej zmiany uprawnień. Lista kadry BSworkout czyta bieżące role, również dla osób dodanych po migracji. Członkostwa roboczych organizacji są osobne.

## Endpointy

Wszystkie poniższe trasy wymagają aktywnego SuperAdmina zarówno w JWT, jak i w bazie. Owner nie ma dostępu. Dane uwierzytelniające i tokeny nie są zwracane.

| Metoda | Ścieżka | Opis |
|---|---|---|
| GET | `/api/super-admin/organizations?page=1&pageSize=25` | Lista |
| GET | `/api/super-admin/organizations/{id}` | Szczegóły |
| POST | `/api/super-admin/organizations` | Utworzenie Draft; 201 + Location |
| PATCH | `/api/super-admin/organizations/{id}` | Pełen formularz nazwy, slugu i wariantu |
| PUT | `/api/super-admin/organizations/{id}/owners/{userId}` | Przypisanie Ownera do Draft |
| GET | `/api/super-admin/organizations/{id}/users?role=Owner&page=1&pageSize=25` | Kadra; role pominięte = Ownerzy i Trenerzy, dozwolone Owner/Trainer |
| GET | `/api/super-admin/users?query=anna&page=1&pageSize=25` | Wyszukanie istniejących kont; query 3–100 znaków |

Wszystkie listy mają `items,total,page,pageSize`. page 1..100000, pageSize 1..100. Filtr roli dotyczy członkostwa wskazanej organizacji. Globalne wyszukiwanie kont jest dostępne tylko SuperAdminowi; nie zwraca kont z rolą platformową SuperAdmin.

POST:
```json
{"name":"Atlas Demo","slug":"atlas-demo","uiVariant":"default","initialOwnerUserId":null,"reason":"Test drugiej organizacji"}
```

Odpowiedź list/szczegółów/zapisów:
```json
{"organizationId":"<uuid>","name":"Atlas Demo","slug":"atlas-demo","uiVariant":"default","status":"Draft","revision":0,"crmAccessEnabled":false}
```

PATCH (wszystkie pola formularza wymagane, nie JSON Patch):
```json
{"name":"Atlas Demo","slug":"atlas-demo","uiVariant":"default","expectedRevision":0,"reason":"Korekta danych"}
```

PUT owner:
```json
{"expectedRevision":1,"reason":"Przypisanie opiekuna testów"}
```

Owner musi istnieć, mieć aktywne konto i potwierdzony email; konto SuperAdmina nie może zostać Ownerem. Nie tworzymy hasła/konta, nie wysyłamy maila. Powtórzenie PUT dla istniejącego Ownera z aktualną rewizją jest bez zmian; nie tworzy kolejnego audytu. Nie ma usuwania Ownera w tym etapie.

Kadra:
```json
{"items":[{"userId":123,"fullName":"Anna Nowak","email":"anna@example.test","roles":["Owner"],"accountStatus":"Active","membershipStatus":"Registered"}],"total":1,"page":1,"pageSize":25}
```
`membershipStatus=Legacy` dla obecnego BSworkout; `Registered` dla rejestru roboczych organizacji. Nie utożsamiać Registered z możliwością wejścia do CRM.

Wyszukiwarka kont zwraca userId, fullName, email, accountStatus, emailVerified. Dane klientów bez konta nie są użytkownikami — liczników klientów nie należy wyliczać z tej listy.

Nazwa 1–100 znaków po trim, slug małe litery/cyfry i pojedyncze myślniki, do 100 znaków, unikalny. UiVariant tylko `default` lub `bsworkout`; istniejący frontend powinien zachować fallback także dla wcześniejszego `standard`. Reason 1–1000 znaków, nie same spacje.

401 brak sesji; 403 brak aktualnej roli; 404 organizacja nie istnieje; 400 walidacja; 409 konflikt rewizji/slugu lub Owner w LegacyActive. Po 409 odświeżyć dane, nie nadpisywać automatycznie.

## Audyt i migracja

`AddOrganizationAdministration` dodaje Revision/Status oraz OrganizationMemberships i PlatformAuditEntry. Przepisuje istniejące globalne role Owner/Trainer do członkostw BSworkout, nie zmienia Users/UserRoles, klientów, pakietów, sesji ani płatności. Nie seeduje Atlas Demo, kont ani haseł. Atlas Demo należy utworzyć powyższym POST WYŁĄCZNIE w środowisku testowym.

Każdy zapis organizacji lub przypisanie Ownera zapisuje rzeczywisty ActorUserId, organizację, opcjonalny SubjectUserId, powód i before/after w tej samej transakcji. Podgląd audytu UI/API jest osobnym etapem. Nowe tabele nie są dostępne dla Supabase anon/authenticated.

## Organizacja po zalogowaniu i docelowa izolacja

Aktualizacja 12.10.2026: `/api/auth/me` zawiera organizationId (null dla SuperAdmina), `/api/organization` wskazuje skonfigurowaną organizację LegacyActive. Nie podmieniać kontekstu nagłówkiem/parametrem z frontu. Draft nie może zostać bieżącą organizacją wdrożenia.

Następny etap: membership-scoped roles, organizationId w tokenie i `/api/auth/me`, wybór organizacji po walidacji członkostwa, owner/trainer/client profile per organizacja, OrganizationId na wszystkich danych operacyjnych, kontrola kluczy obcych przy zapisie. Izolacja musi objąć odczyty po ID, listy, webhooki Outlook/Tpay, zadania w tle, storage, cache, zaproszenia i ustawienia. Dopiero wtedy uruchomienie drugiego CRM i zarządzanie dostępami BSworkout.

Branding docelowo należy do Ownera. Obecne endpointy SuperAdmina są jeszcze zgodnościowe; nowy panel nie powinien rozwijać ich edycji. Przeniesienie uprawnień nastąpi przy wdrożeniu kontekstu organizacji — nie wystarczy zmiana atrybutu roli.

Impersonacja później: oddzielne actor/subject/organization, ograniczona sesja bez odziedziczenia SuperAdmina, reason, audit, cofnięcie i banner frontu.

## Kontrakt identyfikacji przy logowaniu — 12.10.2026

Odpowiedź logowania/odświeżenia i GET /api/Auth/me zawierają teraz `organizationId: string | null`.
- Owner, Trainer i Client obecnego studia: `b5100000-0000-4000-8000-000000000002`.
- SuperAdmin: `null` — panel platformy nie ma wybranej aktywnej organizacji CRM.
- GET /api/organization nadal dotyczy istniejącego BSworkout (LegacyActive). Front nie powinien używać go jako aktywnej organizacji SuperAdmina.
- Tokeny CRM zawierają podpisany claim `organization_id`. Front korzysta z odpowiedzi API, nie ustawia własnego ID w nagłówku.
- Nie ma jeszcze endpointu przełączania organizacji. Atlas Demo/Draft nadal nie umożliwia pracy w CRM.
- Starsze tokeny bez organization_id są akceptowane wyłącznie według dotychczasowego modelu jednego studia. Obce/błędne/powtórzone organization_id jest odrzucane.

To etap zgodności kontraktu, nie zakończenie autoryzacji wielu organizacji. Kontekst jest już zapisany w refresh tokenie, a wspólny resolver obsługuje role członkostw dla przyszłych organizacji Active. BSworkout nadal korzysta z trybu LegacyActive. Przełączanie organizacji, integracje i publiczne endpointy pozostają do dokończenia.
