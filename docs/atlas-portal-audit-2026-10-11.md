# Trainer Portal — audit and rollout, 2026-10-11

Implemented: bounded calendar queries, paginated client/payment/notification reads, recent three completed client sessions, billing/dashboard/settlement summaries, lightweight package/group-client selectors, batched session occupancy, indexes; JWT account/role/version validation, single-use refresh and logout revocation, OAuth PKCE/browser correlation, webhook clientState verification, scoped Outlook imports, session/client/package locks and optional payment idempotency keys.

Validation: 185/185 tests passed on isolated PostgreSQL. HTTP benchmark: real MVC/JWT and SQL, 20 samples per route, 500 clients, 365 sessions, 100 payments, 20 package cycles. Local latency is not production latency. Full measurements: atlas-portal-benchmark.json.

## Rollout

Two migrations: HardenTrainerPortalSecurity; AddPortalReadIndexesAndPaymentIdempotency. Apply before API startup. Target confirmed by owner: TestConnection. Do not change DefaultConnection or main.

Existing read routes remain available. New lighter routes require frontend adoption. Legacy calendar without from/to remains unbounded for compatibility.

Anonymous worker keepalive now wakes the service without executing mutations. Background workers still run automatically. To explicitly execute through keepalive, configure Workers__ApiKey (at least 32 characters) and X-Worker-Key in the monitor, or use an Owner JWT. HTTP 200 from anonymous keepalive is not confirmation of completed accounting.

Outlook connect-url now sets a Secure/HttpOnly/Lax correlation cookie: frontend must preserve it through the callback (verify actual hosting/proxy and credentials handling). Existing token integrations remain. Old subscriptions without clientState are recreated by renewal; their callbacks are ignored until replacement. Browser OAuth/provider end-to-end verification is still required.

Logout/password change invalidates existing sessions. Payment idempotency requires a stable nonempty idempotencyKey GUID on retries; different payload with same key returns 409. Without the key legacy behavior remains.

## Remaining limitations

This is not certification of all security properties. Operational entities do not yet carry organizationId; multiple independent studios must not share operational data without a separate isolation implementation. SuperAdmin changes are postponed.

Notification reads still synchronize derived alerts (15–16 SQL statements in benchmark); no claim of latency improvement for notifications. Settlement summary reduces payload but uses the same calculation/query count. Index effectiveness still needs production EXPLAIN and measurements. OAuth/refresh secrets remain stored using the existing storage design. canConfirm indicates role/status eligibility; final confirmation revalidates package and gateway conditions and can reject a stale result. Full cross-provider concurrency and exhaustive endpoint penetration testing are not established by this suite.
