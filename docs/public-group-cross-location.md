# Zajecia grupowe w wielu lokalizacjach

## Model

`Client.LocationId` pozostaje lokalizacja domyslna klienta. Dodatkowe lokalizacje,
w ktorych klient korzysta z zajec grupowych, sa zapisywane w
`ClientLocationMemberships`. Zakup pakietu w innej lokalizacji nie zmienia trenera,
lokalizacji domyslnej ani pakietu personalnego/semi.

Pakiet grupowy i jego platnosc zawsze zachowuja `LocationId` miejsca sprzedazy. Firma
oraz konto operatora platnosci sa dalej wyznaczane z tej lokalizacji.

## Stan klienta dla frontu

```http
GET /api/public/group-classes/me
Authorization: Bearer {token klienta}
```

Odpowiedz zawiera:

- `defaultLocationId` - domyslna lokalizacja klienta,
- `locations` - lokalizacje z dostepem grupowym,
- `packages` - wszystkie pakiety grupowe wraz ze statusem platnosci, aktywnoscia i
  liczba pozostalych wejsc,
- `upcomingBookedSessionIds` - zajecia, na ktore klient jest obecnie zapisany.

Front powinien pobrac ten endpoint po logowaniu, zakupie, powrocie z Tpay, zapisie i
anulowaniu zapisu.

## Zakup w wybranej lokalizacji

1. Front pobiera pakiety przez `GET /api/public/group-classes/packages?locationId={id}`.
2. Tworzy zakup przez `POST /api/public/group-classes/packages/{packageId}/purchases/me`.
3. Dla platnosci Tpay wysyla `POST /api/payments/tpay/packages/{clientPackageId}/checkout`.
4. Przekierowuje klienta na `checkoutUrl`.
5. Po powrocie odswieza platnosc i `GET /api/public/group-classes/me`.

Platny pakiet pozostaje nieaktywny i bez daty waznosci do potwierdzenia platnosci.
Okres waznosci rozpoczyna sie w dniu potwierdzenia. Pakiet darmowy aktywuje sie od razu.

## Rezerwacje

```http
POST   /api/public/group-classes/{sessionId}/bookings/me
DELETE /api/public/group-classes/{sessionId}/bookings/me
```

Zakup, zapis i anulowanie sa serializowane dla klienta, a zapisy dodatkowo dla zajec.
Chroni to ostatnie wolne miejsce i liczbe pozostalych wejsc przed podwojnym kliknieciem
oraz rownoczesnymi zadaniami.
