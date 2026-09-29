# Zastępstwa przez kategorię Outlooka

Zmiana niezależna od odłożonych kontaktów technicznych i tłumienia maili. Bez migracji bazy.

## Zasady

- Nazwa kategorii wydarzenia odpowiada polu OutlookCategoryName trenera w CRM. Sam kolor nie identyfikuje osoby.
- Wybierz dokładnie jedną kategorię trenera: usuń poprzednią i wybierz kategorię zastępcy. Dodatkowe kategorie niezwiązane z trenerami są dozwolone.
- Trener musi być aktywny, mieć aktywne konto i przypisanie TrainerLocation do lokalizacji sesji.
- Rozpoznanie działa przy imporcie nowego wydarzenia i aktualizacji istniejącej sesji.
- Zmienia się Session.TrainerId; istniejące rozliczenia prowadzących korzystają z tego pola. Nie zmienia się opiekun klienta.
- Aktualizacja kategorią zmienia prowadzącego tylko sesji Planned bez CompletedAt. Zakończone i anulowane wymagają korekty w CRM, aby nie przepisywać rozliczonej historii.
- Brak kategorii trenera przy nowym imporcie zachowuje dotychczasowe rozpoznanie organizatora. Usunięcie kategorii z istniejącej sesji nie cofa zastępstwa; należy wybrać kategorię pierwotnego trenera.
- Dwie kategorie trenerów lub ta sama nazwa przypisana dwóm trenerom są niejednoznaczne. Nowy import jest wstrzymany, istniejąca sesja zachowuje prowadzącego. Ostrzeżenie trafia do istniejącego api/outlook/issues.
- Nieprawidłowa lokalizacja/nieaktywny zastępca działa analogicznie. Inne dane istniejącego wydarzenia nadal są synchronizowane.
- Zastępstwo ustawione kategorią zachowuje powiązanie z pierwotnym kalendarzem również przy późniejszej synchronizacji pojedynczej sesji z CRM. Nie wymaga podłączenia Outlooka zastępcy.

## Front

Nie potrzeba nowego endpointu zastępstw. Pokazywać aktualnego trenera zwracanego w sesji oraz ostrzeżenia synchronizacji; sam kolor nie potwierdza przyjęcia zastępstwa.

## Test po wdrożeniu

1. Istniejącej planowanej sesji zamień kategorię A na B; B pracuje w tej lokalizacji. CRM powinien pokazać B.
2. Przesuń tę sesję w CRM. Wydarzenie powinno zostać w pierwotnym kalendarzu, bez duplikatu.
3. Zamień kategorię B na A; sprawdź powrót prowadzącego.
4. Utwórz nowe wydarzenie z kategorią B, klientami i lokalizacją; CRM powinien przypisać B.
5. Wybierz dwie kategorie trenerów albo trenera innej lokalizacji; sprawdź ostrzeżenie i brak błędnego przypisania.
6. Zmień kategorię jednej instancji serii; sprawdź, że inne instancje nie zmieniły prowadzącego.

Testy jednostkowe obejmują wybór po nazwie, niejednoznaczność, lokalizację, aktywność konta i ochronę rozliczonych sesji. Test rzeczywistego przepływu Microsoft Graph pozostaje do wykonania po wdrożeniu.
