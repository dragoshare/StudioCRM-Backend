# Tpay sandbox integration

This release supports one-time checkout for an existing client package in PLN.
It does not create a new package, implement recurring debits or send fiscal receipts.
Checkout is deliberately blocked when `Tpay__UseSandbox=false` until production rollout.
Sandbox confirmations update the CRM database configured for the backend: use the test database.

## Backend environment (Render)

```text
Tpay__UseSandbox=true
Tpay__BackendBaseUrl=https://studiocrm-backend.onrender.com
Tpay__ReturnUrl=https://YOUR-FRONT-DOMAIN/payment-result
Tpay__Accounts__Studio1__ClientId=YOUR_SANDBOX_CLIENT_ID
Tpay__Accounts__Studio1__ClientSecret=YOUR_SANDBOX_CLIENT_SECRET
Tpay__Accounts__Studio1__MerchantId=YOUR_NUMERIC_SANDBOX_MERCHANT_ID
Tpay__Accounts__Studio1__SecurityCode=YOUR_NOTIFICATION_SECURITY_CODE
```

`MerchantId` is the numeric merchant identifier, NOT the OAuth Client ID.
`SecurityCode` is the notification verification code from Notifications > Security;
leave it empty only if no verification code is set in the Tpay panel.
Secrets stay exclusively in backend environment variables.

In the Tpay sandbox panel, set the notification URL to
`https://studiocrm-backend.onrender.com/api/payments/tpay/notifications` and enable
notification URL override. The backend also supplies this URL when creating checkout.
ReturnUrl must be a real frontend route. Both success and failure redirect there;
the frontend must read the payment status from CRM, never infer payment from the redirect.

The account created in the test database is provider account 1, `AccountKey=Studio1`,
legal entity 3 (BS Workout Niepolomice), related location 3.
It remains inactive until the above configuration is ready. Activate it through
the existing owner payment configuration endpoint, preserving all other account fields.
OAuth credentials and the notification security code never go into account metadata or frontend payloads.
The numeric MerchantId may optionally also be stored in the account metadata; it is not a secret.

## Frontend API

All client endpoints require the existing Client bearer token. A trainer assignment
is not required, so clients registered for group classes can pay too.

### Create or resume checkout

`POST /api/payments/tpay/packages/{clientPackageId}/checkout`

No request body. Use the purchased **ClientPackage ID**, not the catalog Package ID.
The backend determines the client, company, provider account and remaining amount.
The package must have an explicit active location with an active company and Tpay account.

Returns the existing `ClientPaymentDto`. Relevant fields:

| Field | Meaning |
| --- | --- |
| `id` | CRM payment ID |
| `clientPackageId` | Purchased package ID |
| `amount`, `currency` | Outstanding package amount, PLN |
| `checkoutUrl` | Redirect the browser here |
| `status` | Existing ClientPaymentStatus enum |
| `providerStatus` | `sandbox:creating`, `sandbox:pending`, `sandbox:paid`, `sandbox:create-rejected` |
| `providerPaymentId` | Tpay transaction ID |
| `externalPaymentId` | Tpay transaction title (TR-...) |
| `legalEntityId`, `locationId` | Company and location captured at checkout |

Disable the checkout button while the request is running. A repeated request resumes
the existing pending checkout, provided its amount and provider account still match.
HTTP 409 with `{ "message": "..." } means configuration, eligibility or pending-payment
conflict. Show the error; do not retry in a loop.

### Payment result

`GET /api/payments/tpay/payments/{paymentId}`

Returns the same DTO, only for the authenticated client's own payment; otherwise 404.
The return URL receives `?paymentId=...` (or an appended query parameter).
Poll this CRM endpoint with a bounded interval (for example 3 seconds, at most one minute),
then allow manual refresh. Polling does not query the Tpay API.
Use `status=2` (Confirmed) for success; this API currently serializes status, method and source as numbers.
`PendingConfirmation` is value 1; it means payment is not yet confirmed, including an abandoned checkout.
ReceiptStatus is a separate string.
Refresh package and billing queries after confirmation.

### Notifications (Tpay only)

`POST /api/payments/tpay/notifications`

Anonymous form-urlencoded endpoint requiring the Tpay `X-JWS-Signature` header.
The frontend never calls it. Verifies certificate chain, RS256 body signature, merchant ID,
checksum, transaction title, sandbox flag, amount and currency before recording payment.
Successful notifications receive plain-text `TRUE` only after database commit.
Repeated/concurrent notifications do not create duplicate credits.
Requests requiring review return a failure so Tpay can retry; monitor backend warnings.

### Owner connection test

`POST /api/billing/payment-configuration/tpay/Studio1/test-connection`

Checks OAuth credentials only. It does not validate merchant ID, notification security code,
callback delivery, checkout permissions or receipt handling.

## Accounting and limitations

- Confirms the existing ClientPayment and uses the existing package/balance accounting.
- Group packages can be activated without deactivating individual packages. Paying old debt
  does not reactivate an exhausted or expired package.
- Receipt issuance remains a separate operation, with the existing receipt workflow.
- Gateway payments do not appear in the manual confirmation queue and cannot be manually confirmed/rejected.
- Manual payment entry for a package with a pending gateway checkout is blocked to prevent double payment.
- Provider fees and payout dates can use the existing settlement endpoint; this integration does not import them.
- Gateway status/transaction ID cannot be edited through the settlement endpoint.
- A timeout after checkout reservation is ambiguous: do not create a new transaction automatically.
  The persisted payment can be recovered by its signed callback (`tr_crc=crm-payment-{id}`).
  Otherwise staff must reconcile it in Tpay; automatic recovery/cancellation is not included yet.
- A definitive API rejection (400/401/403/422) marks the local reservation rejected,
  allowing another attempt after correcting configuration. It never confirms payment.
- Abandoned checkout can be resumed using its existing URL. No automatic expiration is invented.
- Refund/chargeback notifications, mismatched amounts and production payments are not automatically booked.
  They require manual review; provider refunds and recurring payments are outside this release.

## Verification before activation

1. Set environment variables and enable panel notifications/override.
2. Activate only the intended sandbox provider account in the test CRM.
3. As a client, create checkout for their unpaid package in Niepolomice.
4. Complete a sandbox payment and verify one confirmed payment and one package credit.
5. Resend the notification from Tpay and verify balances are unchanged.
6. Test another client's payment ID, a different company's package and an abandoned checkout.

Automated PostgreSQL tests use `TPAY_TEST_DB`; they create and remove a random isolated schema.
Without that variable they are explicitly skipped. They use a fake provider, not real Tpay keys.

References:
- https://docs-api.tpay.com/en/first-steps/first-transaction/
- https://docs-api.tpay.com/en/webhooks/
- https://docs-api.tpay.com/en/first-steps/environments/
