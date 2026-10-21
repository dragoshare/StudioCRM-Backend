# Outlook: powiązanie bazy z jednym backendem (po incydencie z 21.10.2026)

## Dlaczego

21.10.2026 backend testowy działał na kopii bazy produkcyjnej. Miał w niej aktywne połączenia Outlooka trenerów, czyli tokeny i identyfikatory subskrypcji. Domyślny `Outlook:WebhookUrl` w `appsettings.json` wskazuje produkcję.

Skutki:
- oba backendy na zmianę usuwały i zakładały te same subskrypcje Microsoftu;
- oba dostawały powiadomienia o zmianach w kalendarzu i oba „poprawiały” tytuły wydarzeń;
- każda poprawka wydarzenia z uczestnikami to mail z Exchange do każdego uczestnika.

Skrzynka trenera przekroczyła limit wysyłki i została zablokowana.

## Co robi backend

1. **Powiązanie bazy z backendem.** Przy pierwszej operacji w Outlooku backend zapisuje w `SystemSettings` (klucz `Outlook:InstanceBinding`) odcisk swojej bazy i swojego adresu webhooka. Odcisk to skrót z hosta, portu, nazwy bazy i użytkownika z connection stringu oraz z `Outlook:WebhookUrl`, bez hasła. Backend z innym odciskiem nie wykonuje **żadnego** wywołania do Microsoftu:
   - nie odświeża tokenów;
   - nie tworzy, nie odnawia i nie usuwa subskrypcji;
   - nie synchronizuje sesji;
   - nie czyta kalendarza;
   - ignoruje powiadomienia webhooka.

   W logach i w odpowiedziach pojawia się wtedy komunikat „Outlook jest powiązany z innym backendem lub inną bazą…”. Kopia bazy przeniesiona na inny serwer lub do innego projektu Supabase jest więc rozpoznawana także wtedy, gdy skopiowano zmienne środowiskowe produkcji.

2. **Kierunek Outlook → CRM tylko czyta.** Webhook i odświeżanie kalendarza aktualizują sesje w CRM, ale nie zmieniają już tytułów wydarzeń w Outlooku. Wydarzenie w Outlooku zmienia się tylko wtedy, gdy ktoś zmieni sesję w CRM.

## Wdrożenie

1. **Baza testowa:** wyłącz Outlooka i usuń skopiowane tokeny.
   ```sql
   update "CalendarSubscriptions" set "IsActive" = false;
   update "CalendarIntegrations" set "IsActive" = false, "AccessToken" = '', "RefreshToken" = '', "DisconnectedAt" = now();
   ```
   Backend testowy zostaje z `Outlook__EmergencyStop=true` i własnym `Outlook__WebhookUrl`, a nie produkcyjnym.
   Kopie bazy zrobione **przed** pierwszym powiązaniem nie mają klucza `Outlook:InstanceBinding`. Taka kopia mogłaby zostać powiązana z backendem testowym, dlatego ten krok jest konieczny.
2. Wdróż produkcję. Pierwsza operacja w Outlooku, np. worker subskrypcji, zapisze powiązanie. Sprawdzenie:
   ```sql
   select "Key", "Value", "CreatedAt" from "SystemSettings" where "Key" = 'Outlook:InstanceBinding';
   ```
3. Wyłącz `Outlook__EmergencyStop` na produkcji. Worker odtworzy subskrypcje. Odśwież kalendarz jednego trenera i przez godzinę obserwuj logi.

## Celowa zmiana (nowy host bazy, nowy adres backendu, zmiana connection stringu)

Po zmianie hosta, portu, bazy, użytkownika albo `Outlook__WebhookUrl` produkcja przestanie łączyć się z Outlookiem i będzie logować powyższy komunikat. Wtedy, **tylko na właściwej bazie**:

```sql
delete from "SystemSettings" where "Key" = 'Outlook:InstanceBinding';
```

Najbliższa operacja w Outlooku powiąże bazę z aktualnym backendem. Każda instancja zapamiętuje wynik sprawdzenia do 5 minut, więc zmiana może zadziałać z takim opóźnieniem. Restart backendu działa od razu.

## Zasada na przyszłość

Nigdy nie uruchamiaj drugiego backendu z aktywnymi połączeniami Outlooka skopiowanymi z produkcji. Przy każdym kopiowaniu bazy na test wykonaj krok 1.
