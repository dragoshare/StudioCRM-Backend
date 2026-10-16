# Rozliczenie trenera — sesje poza umową (widok Ownera)

Panel trenera (`GET /api/trainer-portal/settlement`) i widok Ownera (`GET /api/trainers/{id}/settlement?year=&month=`)
korzystają z tej samej usługi i zwracają ten sam `TrainerMonthlySettlementDto`. Różnica jest tylko w UI:
panel trenera pokazuje sekcję „Sesje poza umową”, widok Ownera jej nie pokazuje, więc u trenera bez umowy
Owner widzi „0 sesji / 0 zł” mimo zakończonych sesji.

## Zasada liczenia

Do kwoty (`totalAmount`, `totalSessions`, `totalHours`, `items`) wchodzą tylko zakończone sesje, których
lokalizacja należy do aktywnej umowy trenera (`TrainerContract` + `ContractLocations`), a data (dzień w czasie
polskim) mieści się w okresie umowy — `validFrom` i `validTo` włącznie. Umowa od 15.10 nie obejmuje sesji z 1–14.10.
Ta sama zasada dotyczy ewidencji godzin. Pozostałe zakończone
sesje trafiają do `nonContractedItems`, `nonContractedTotalSessions`, `nonContractedTotalHours` i
`nonContractedTotalAmount`. Mają wyliczoną stawkę i kwotę, ale nie są doliczane do wypłaty (`totalAmount`).
`nonContractedTotalAmount` zwracają też lista rozliczeń Ownera i podsumowanie w panelu trenera.

## Co pokazać u Ownera

1. Kafelki jak dziś (objęte umową).
2. Gdy `nonContractedTotalSessions > 0`: ostrzeżenie „N sesji (X h) poza umową — nie są w kwocie rozliczenia”
   z akcją „Dodaj / popraw umowę trenera” (lokalizacje i okres obejmujący miesiąc).
3. Tabela „Szczegóły sesji poza umową” z `nonContractedItems`, w tym samym układzie co w panelu trenera.
4. Oznaczenie wypłaty jako zapłaconej przy `nonContractedTotalSessions > 0` potwierdzaj dodatkowym komunikatem,
   bo te sesje nie wejdą do wypłaconej kwoty.

Po dodaniu umowy sesje przechodzą do części objętej umową w obu widokach, bez zmian w danych sesji.
