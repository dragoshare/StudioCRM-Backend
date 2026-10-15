# Konsultacje — kontrakt backendu

Konsultacja to czas pracy trenera bez klienta w CRM (zwykle przed założeniem kartoteki). Nie ma uczestników,
nie odlicza wejść, nie zmienia salda. Po zakończeniu wlicza się do godzin i wynagrodzenia trenera po zwykłej
stawce godzinowej (bez dodatku semi).

## Z Outlooka

Wydarzenie z dwiema kategoriami: **kategoria trenera** (`Trainer.OutlookCategoryName`) + **`Konsultacje`**.
Kalendarz musi być podłączony do CRM; Owner może zapisać konsultację innemu trenerowi w swoim kalendarzu.
Wymagane pole lokalizacji wydarzenia. Tytuł wydarzenia staje się tytułem sesji i nie jest nadpisywany imionami
klientów. Uczestnicy (attendees) są ignorowani. Wydarzenie nakładające się na inną sesję tego trenera trafia na
listę problemów zamiast tworzyć sesję. Obowiązuje granica importu `Outlook:ImportSessionsFrom`.

## Ręcznie

`POST /api/sessions` z `plannedSessionType: "Consultation"`, `participants: []`, `isPubliclyBookable: false`.
Tytuł opcjonalny (domyślnie „Konsultacja”). `status: "Completed"` zapisuje od razu zakończoną konsultację.
Konsultacja z uczestnikami lub publicznym zapisem zwraca 400.

## Zakończenie i rozliczenie

Zaplanowana konsultacja po godzinie końca jest kończona automatycznie (`actualSessionType: "Consultation"`,
`actualParticipantsCount: 0`). Ręczne `POST /api/sessions/{id}/complete` przyjmuje pustą listę uczestników.
Konsultacja w miesiącu z opłaconym rozliczeniem trenera jest zablokowana jak każda sesja. W widokach pokazuj
typ „Konsultacja”, bez liczników wejść i bez cen.
