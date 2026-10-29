# Device activation confirmation â€” Portal contract v1

All URLs below are under `/portal/api/licensing/device`. HTTPS required. No Portal cookie or CSRF token is used. Responses containing confirmation tokens or leases use Cache-Control: no-store. Signing stays disabled until joint product validation; preparation is available without signing, and reports `onlineActivationAvailable`.

## POST /prepare

JSON: `username` (Portal username or email), `password`, `licenseCode` (single public code), `activationId` (client-generated nonempty UUID), `machineFingerprint` (64 hex characters). Machine and activation identifiers are device-generated, never user-entered internal organization/application IDs.

200: `confirmationToken` (256 random bits, Base64URL), `expiresAtUtc` (five minutes), `onlineActivationAvailable`, `summary`. Only the token hash is stored. Password is authenticated only here, never persisted in a preparation.

Summary: `licenseId`, `organizationId`, `applicationId`, `licenseCode`, `applicationName`, `mode` (`online-account`), `term` (`perpetual`, `monthly`, `annual`), `entitlementExpiresAtUtc` (nullable commercial expiry), `tier`, `interactiveSeats`, `viewOnlySeats`, `haRuntime`, `assetProcessIntelligence`, `maxActivations`.

Preparation checks confirmed/accepted/unsuspended account, membership and active unexpired license. It creates no activation, credential or lease, and does not reserve capacity. Display summary and require explicit user confirmation before calling confirm. A commercial perpetual right still uses short online leases. Do not infer commercial term from lease TTL.

## POST /confirm

JSON: `confirmationToken`, same `activationId`, same `machineFingerprint`, `requestId` (client-generated nonempty UUID), `refreshCredential` (client-generated 32 random bytes encoded as 43 Base64URL characters). Persist the request intent securely before sending, so a lost response can be retried with exactly the same fields.

200: `activationId`, `lease`, `notAfterUtc`, `summary` (same prepared snapshot). No password. User/security stamp, membership, current license and application summary are revalidated under database locks. Changes to prepared rights or presentation require preparing again; no silently changed grant. Capacity is checked and token consumption, activation and hashed refresh credential are committed in the same transaction.

An exact retry (including simultaneous requests) returns the original lease with no additional activation. A consumed token cannot authorize another request or refresh credential. First confirmation must occur within five minutes; exact retries are retained until 24 hours after token expiry. Old rows are removed on subsequent preparation.

## POST /cancel

JSON: `confirmationToken`. Returns 204, including unknown token. Marks an unconsumed preparation cancelled. Cancellation after successful confirmation does not revoke an activation. Cancellation and confirmation serialize on the preparation row; whichever commits first determines the result. Abandoning or cancelling an unconsumed preparation costs no activation slot.

## Errors

400: invalid input (`invalid-input`) or `https-required`. 401: `invalid-credentials` for account authentication/unknown token. 404: license eligibility failures return `license-not-available`. 409: `preparation-invalidated` (cancelled, changed account stamp/summary or machine/activation mismatch), `preparation-expired`, `preparation-already-consumed`, `activation-id-already-bound`, `activation-revoked`, `activation-already-active`, `activation-limit-reached`, `refresh-credential-already-used`, or `activation-request-conflict`. 429: throttled. Confirm returns the existing 503 signing-unavailable response while issuance is disabled. Old POST /activate returns 410 `activation-confirmation-required` and cannot bypass confirmation.

## Signed rights and renewal

The ESLEASE1 schema and its 14 claims are unchanged. Summary is HTTPS presentation metadata, not additional signed rights. Product must verify signature, identity, machine, tier, seats, flags and times against the lease and reject mismatches before committing local installation. Commercial term/name are not claims and cannot expand signed rights. Renewal remains the existing Bearer rotating-device-credential endpoint, automatic at 48 hours with maximum 72-hour leases. It does not use a Portal browser session or resubmit the account password. Password changes invalidate prepared tokens through the security stamp and existing activation credential invalidation.

No production deployment, signing-key provisioning or issuance activation is part of this change. Product integration remains under #301; Portal scope #741. This branch is based on #778; #783/#784 and offline intake #777 remain independent review dependencies.
