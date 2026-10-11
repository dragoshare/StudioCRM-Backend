# Data wpłaty

Tworzenie wpłaty już obsługuje opcjonalne paymentDate w CreateClientPaymentRequest (POST /api/billing/payments oraz POST /api/trainer-portal/clients/{clientId}/payments). Bez niego stosuje aktualny czas. Front może przekazać datę wcześniejszego otrzymania pieniędzy.

Edycja: PATCH /api/billing/payments/{paymentId}/date (Owner) lub PATCH /api/trainer-portal/payments/{paymentId}/date (Owner/Trainer, trener tylko własnych klientów).

```json
{"paymentDate":"2026-10-05T12:00:00+02:00","expectedPaymentDate":"2026-10-11T10:00:00Z","reason":"Wpłata otrzymana wcześniej, wpisana z opóźnieniem"}
```

Wszystkie pola wymagane. Daty ISO 8601 z offsetem, zapisywane w UTC. expectedPaymentDate to obecne paymentDate z DTO, zabezpiecza przed nadpisaniem równoległej korekty. Powód 1..1000 znaków. Nowa data nie może być przyszła. Odpowiedź 200: pełny ClientPaymentDto. Identyczne ponowienie jest bez dodatkowego audytu.

Dotyczy wpłat ręcznych PendingConfirmation/Confirmed. Daty płatności operatora oraz wpłaty odrzucone/anulowane/cofnięte nie podlegają tej edycji. 400 — walidacja/niedozwolony stan/dostęp do obcego klienta (istniejący mechanizm uprawnień); 401 — brak tokena; 403 — brak roli; 404 — brak wpłaty; 409 — data zmieniona równolegle.

Zmiana dotyczy wyłącznie PaymentDate, po której już sortujemy i filtrujemy historię oraz zestawiamy przychody. CreatedAt, ConfirmedAt, wpłaty na pakiet, saldo, daty ważności/aktywacji oraz daty dokumentów pozostają historycznymi zdarzeniami. Zmiana nie księguje wpłaty ponownie. Activity Log zachowuje zmianę daty; audyt klienta PaymentDateCorrected zawiera wykonawcę, powód i przed/po. Brak nowej migracji.
