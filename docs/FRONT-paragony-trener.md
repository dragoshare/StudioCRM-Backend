# Paragony w portalu trenera

Trener oznacza, że paragon za wpłatę jego klienta został wybity na kasie. Owner widzi wtedy przy wpłacie
`receiptStatus = "Issued"`, numer, datę i to, kto oznaczył paragon (`receiptIssuedByUserId`).

Endpointy (rola Trainer lub Owner; trener tylko dla klientów przypisanych do siebie, inaczej `400` z
`message = "Trainer does not have access to this client."`):

| Metoda | Ścieżka | Opis |
| --- | --- | --- |
| `GET` | `/api/trainer-portal/receipts/pending` | Potwierdzone wpłaty jego klientów, które czekają na paragon (`receiptStatus` = `Pending`, `ManualRequired` albo `Failed`). |
| `POST` | `/api/trainer-portal/payments/{paymentId}/receipt/issue` | Oznacza paragon jako wystawiony. Body opcjonalne: `{ "receiptNumber": "…", "receiptNote": "…" }`; bez numeru backend nadaje numer automatycznie. |
| `POST` | `/api/trainer-portal/payments/{paymentId}/receipt/cancel` | Cofa oznaczenie (np. pomyłka); status zmienia się na `Cancelled`. |

Odpowiedź `issue` i `cancel`: zaktualizowany `ClientPaymentDto`. Błędy `400` z `message`:

- `Only confirmed payments can receive a receipt.` — wpłata nie jest potwierdzona;
- `Receipt has already been issued for this payment.` — paragon już oznaczony;
- `Only issued receipts can be cancelled.` — anulowanie bez wystawionego paragonu.

Na froncie: przycisk w kolumnie „Akcje” historii wpłat pokazujemy przy potwierdzonej wpłacie z
`receiptRequired = true` i `receiptStatus` innym niż `Issued`. Po wystawieniu pokazujemy numer i datę
(`receiptNumber`, `receiptIssuedAt`) oraz akcję „Cofnij”. Każda zmiana trafia do logu operacji.

Owner nadal korzysta z tych samych operacji pod `/api/billing/...` (`receipts/pending`,
`payments/{paymentId}/receipt/issue`, `payments/{paymentId}/receipt/cancel`).
