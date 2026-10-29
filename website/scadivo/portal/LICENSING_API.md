# Activation contract update

The direct activation section below is superseded by [ACTIVATION_CONFIRMATION.md](ACTIVATION_CONFIRMATION.md). Use prepare → explicit user confirmation → confirm. POST /device/activate now returns 410. Renewal and the signed ESLEASE1 schema remain unchanged.

# SCADIVO online licensing API — implementation contract

This Portal API is a separate layer from the offline request intake in PR #777. It manages organizations, applications, entitlements, machine activations, rotating device credentials, and signed online leases. The Portal Licenses tab provides customer organization/application management, entitlement and activation consultation, and deactivation of the account's installations. Portal Owners have a separate licensing administration tab for explicit entitlement creation, extension and revocation. Product client integration remains separate.

## Safety and deployment state

- Online signing is **disabled by default**. While disabled, device activation and renewal return HTTP 503 (`licensing-service-unavailable`).
- Do not enable the signer in production until the product verifier trusts `scadivo-online-lease-2026-01`, the product has integrated activation/renewal and secure token storage, and the end-to-end acceptance tests are complete.
- To enable device confirmation and lease renewal, mount the approved RSA-3072 private key from `/etc/scadivo/portal/signing/online-lease.pem` as a read-only runtime file at `/run/scadivo-signing/online-lease.pem`. The deployment Compose defaults `Portal:Licensing:Online:Enabled` to false; set `PORTAL_ONLINE_LEASE_SIGNING_ENABLED=true` only after the approved key is present, the encrypted backup has completed, and product verification is available. Grant read access only to the Portal API service identity (UID 1654 on the current container); keep the containing directory restricted. The encrypted Portal backup includes the signing key when it is present. Startup rejects an incorrect KeyId, fingerprint, key size, or path. Never put the private key in a repository, database, image, application package, Data Protection ring, or logs.
- The API signs only its server-created `ESLEASE1` claims. It has no arbitrary-payload signing endpoint. Production entitlement creation and lease signing are not a payment flow; payment integration is not included.
- All device calls require HTTPS. The product calls the Portal over outbound TCP 443; it does not use a browser cookie or hold the Portal password after activation.

## Ownership model

- An account can belong to several organizations. An organization can own several named applications, and each application can have several license entitlements.
- `LicenseId` is the human-visible license code that selects an entitlement; it is not an authentication secret.
- A Portal `Owner` or delegated Portal `Admin` administers organizations, issues/extends/revokes entitlements, and revokes activations. These are global SCADIVO staff roles, separate from an organization `Owner` or `Admin`, which manages only that organization's application list and members. Members can see their organization's licenses and manage their own device activations.
- A license is bound to one organization and one application. Composite database constraints prevent an activation or license from referencing an application in another organization.
- The `MaxActivations` value is required when a Portal administrator creates an entitlement. Its commercial value remains a business decision; the API does not guess a default.
- `perpetual` means the account entitlement has no commercial expiry. Online use still requires a new signed lease at least every 48 hours; every lease expires within 72 hours. Monthly and annual terms have an entitlement expiry and leases end no later than that expiry.

## Portal endpoints

The API is under `/portal/api` on the public site. Customer write calls require the authenticated Portal session and its anti-forgery token. The two product device endpoints are HTTPS-only and use the device credential flow below instead.

| Method | Path | Access |
|---|---|---|
| `GET` | `/licensing/status` | Portal account; only reports whether online activation is available |
| `GET` / `POST` | `/licensing/organizations` | Portal account; create requires a complete profile |
| `GET` / `POST` | `/licensing/organizations/{organizationId}/applications` | Organization member; create requires Owner/Admin |
| `GET` / `POST` | `/licensing/organizations/{organizationId}/members` | Organization member; add requires Owner/Admin |
| `DELETE` | `/licensing/organizations/{organizationId}/members/{userId}` | Organization Owner/Admin |
| `GET` | `/licensing/licenses` | Portal account; returns only licenses in its organizations |
| `GET` | `/licensing/activations` | Portal account; returns its device activations |
| `DELETE` | `/licensing/activations/{activationId}` | Activation's Portal account |
| `GET` | `/admin/licensing/organizations` | Portal Owner/Admin |
| `GET` / `POST` | `/admin/licensing/licenses` | Portal Owner/Admin; create an entitlement |
| `POST` | `/admin/licensing/licenses/{licenseId}/extend` | Portal Owner/Admin; subscription term only |
| `POST` | `/admin/licensing/licenses/{licenseId}/revoke` | Portal Owner/Admin |
| `GET` | `/admin/licensing/licenses/{licenseId}/activations` | Portal Owner/Admin |
| `POST` | `/admin/licensing/activations/{activationId}/revoke` | Portal Owner/Admin |

An administrator-created entitlement is not itself a signed machine license. It becomes usable by the product only after the product performs an authenticated device activation while the dedicated online signer is enabled.

## Product activation

`POST /portal/api/licensing/device/activate`

The product generates and persists an `ActivationId`, a stable `RequestId`, and a cryptographically random 32-byte Base64URL refresh credential **before** sending the first request. It sends the Portal login only for this initial activation, and must not log the password or request body.

```json
{
  "username": "portal-user-or-email",
  "password": "provided-at-activation-only",
  "licenseCode": "SCV-...",
  "activationId": "00000000-0000-0000-0000-000000000000",
  "requestId": "00000000-0000-0000-0000-000000000000",
  "machineFingerprint": "64-hex-characters",
  "refreshCredential": "43-character-base64url-token"
}
```

The Portal verifies the confirmed/active account, password, organization membership, entitlement state, application binding, machine fingerprint and activation limit. It stores only a SHA-256 hash of the refresh credential. The response contains the activation ID, signed lease and lease expiry. The license code selects the entitlement; possession of the code alone does not activate it.

The request is idempotent for the same activation ID, request ID and refresh credential. If the response is lost, resend the exact persisted request. Reusing an activation ID with different identity, license, application, or machine is rejected.

## Product renewal

The product initiates renewal every 48 hours using HTTPS. It creates and persists the renewal `RequestId` and next random refresh credential before sending the request. It replaces the current credential only after a successful response; after a lost response it resends the same request ID and next credential.

`POST /portal/api/licensing/device/activations/{activationId}/renew`

```http
Authorization: Bearer <current-refresh-credential>
Content-Type: application/json
```

```json
{
  "requestId": "00000000-0000-0000-0000-000000000000",
  "nextRefreshCredential": "43-character-base64url-token"
}
```

On success the server atomically supersedes the prior credential, stores the next credential's hash, and returns a newly signed lease. A retry with the same old credential, request ID and next credential returns the exact previously issued lease. Reuse of a superseded credential for a different request is treated as compromise: all credentials for that activation are revoked and the installation must reactivate with the Portal account. Password changes, password resets, suspension, organization membership removal, license revocation, or activation revocation invalidate future renewals.

Revocation blocks future leases. It does not make a previously issued lease disappear: that lease remains valid to the product until its signed `NotAfterUtc`, at most 72 hours after issuance. When an online lease expires, the product contract requires an explicit transition to Demo without deleting the project; this Portal API does not implement that product behavior.

## `ESLEASE1` schema 1

The envelope is `ESLEASE1.<base64url-json>.<base64url-signature>`. The JSON uses camelCase and carries:

`schemaVersion`, `licenseId`, `organizationId`, `applicationId`, `activationId`, `machineFingerprint`, `tier`, `viewOnlySeats`, `interactiveSeats`, `haRuntime`, `assetProcessIntelligence`, `issuedAtUtc`, `notAfterUtc`, and `keyId`.

The API signs the UTF-8 JSON bytes with RSA-PSS/SHA-256 and key ID `scadivo-online-lease-2026-01`. Its private key must never be shared with the offline generator. The product must separately pin the approved online public key and check the signature, purpose/KeyId, schema, machine identity, times and 72-hour maximum. Offline `ESLIC1` schema 1, `ESLIC2` schema 2 and `ESLIC3` schema 3 remain unchanged; the offline `elite-prod-2026-01` key never signs an online lease.

## Current exclusions and follow-up

- The customer and Owner license screens are PT-BR/EN/ES and use the same authenticated API/anti-forgery boundaries. They never activate a product on behalf of the browser, handle refresh tokens or reveal signing material. Entitlement creation requires explicit capacities, term and activation limit; it does not process a payment or install a license.
- The offline `ESREQ1` intake remains in PR #777; offline request review, signing, conversion, delivery and offline-key operations remain separate.
- Product-side verification, Windows/Linux/Docker token protection, License UI, shared Runtime quotas, HA, Demo transition and the 48-hour renewal scheduler must be implemented on the product branch under issue #301.
- Payment processing, automatic account purchase, commercial activation policy, and translation of community posts are outside this API stage.
- Keep signing disabled until the product and operational key controls are integrated and verified. This PR does not deploy to the live Portal.
