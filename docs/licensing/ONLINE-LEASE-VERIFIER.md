# Online lease verifier — first product slice (#301)

This slice validates an online lease without installing it or changing a running application.
The governing contract is [#301, 24 October 2026](https://github.com/brunolrogerio-collab/EliteSCADA/issues/301#issuecomment-6392078669).
The implementation starts from product integration commit `d41c2f84ba179f5eae01dc1df96c9c1a28830dc7`.
The Portal wire contract was checked against PR #778 commit `3f825c9750eed841d309004af452cb5bff3d0db5`,
`website/scadivo/portal/Licensing/OnlineLeaseSigning.cs` (`OnlineLeaseClaims`, `RsaPssOnlineLeaseSigner`).

## Code and trust boundary

- `src/Scada.Core/Product/Licensing/OnlineLeaseVerifier.cs`: `OnlineLeaseVerifier.Verify` uses the pinned online public key. Its internal test entry accepts ephemeral keys; production callers cannot override the anchor.
- `ProductLicenseTrustAnchors.CreateOnlineLeasePublicKey`: RSA 3072, KeyId `scadivo-online-lease-2026-01`, SPKI SHA-256 `26a33c58eeeb1329e8901b2722776545be5a8a266016cc44295bf115960d59aa`. Only public material is embedded.
- `EliteScadaLicenseCodec.ValidateSignatureMachineAndExpiry`: rejects the reserved online KeyId in ESLIC1/2/3, even if a caller supplies it in the offline key dictionary. Other existing offline keys, schemas and expiry rules are preserved.
- `CreateBuiltInVerificationKeys` remains an offline-only dictionary. This slice adds no signing API or production key configuration.

## Input and result

The envelope is `ESLEASE1.<base64url-json>.<base64url-signature>`. Verification uses the original JSON bytes with RSA-PSS/SHA-256. It does not reserialize them before checking the signature.

All fourteen camelCase fields are required exactly once: `schemaVersion`, `licenseId`, `organizationId`, `applicationId`, `activationId`, `machineFingerprint`, `tier`, `viewOnlySeats`, `interactiveSeats`, `haRuntime`, `assetProcessIntelligence`, `issuedAtUtc`, `notAfterUtc`, `keyId`. Unknown or case-changed properties, incorrect JSON types, numeric/unknown tiers and negative or overflowing seat totals are invalid. IDs are nonempty GUID strings in D format; the fingerprint is SHA-256 hexadecimal. Times must carry explicit UTC (`Z` or `+00:00`), so an offset-free date cannot change meaning with the host timezone.

`OnlineLeaseBinding` supplies **all four expected IDs and the expected machine**. A later activation client must obtain these identities from its persisted intent and authorized license selection, rather than copy them from the response being checked. The matching requirement is deliberate; this verifier does not resolve an entered License Code or application name.

The accepted interval is `IssuedAtUtc <= nowUtc < NotAfterUtc`, with a positive lifetime no longer than 72 hours. A shorter commercial-expiry-limited lease is allowed. Invalid signatures or bindings never become an `Expired` result merely because their dates are old.

`Valid` returns typed claims. `Expired` and `Invalid` return no claims or entitlements. `Expired` means signature, identities and structure passed but the signed end time has been reached; it is **not** an instruction to fall back to Demo. Diagnostics do not echo the envelope or untrusted values. Input allocation is bounded (16,384 envelope characters, 8,192 payload bytes, 384 signature bytes), with canonical unpadded Base64url required.

## Verification and remaining work

`OnlineLeaseVerifierTests` signs fictitious Portal-compatible payloads with ephemeral keys and covers time boundaries, all fields, identities, totals, JSON/encoding rejection, tampering, padding, and signed cross-format cases. `ProductLicenseTrustAnchorsTests` checks the production public anchor fingerprint and that the offline dictionary stays separate. Existing ESLIC1/2/3 tests remain regression evidence.

Run the complete Core test project and the existing licensing/capacity tests. Wave 15 T1 declares `FOUNDATION_LIFECYCLE, SESSION_LICENSING`: the additive first profile selects Core, where these tests live; SESSION_LICENSING alone selects Drivers and would miss them. No router, retry, filter or timeout is changed.

Next slices remain: protected durable activation/renewal state and persisted network intentions; replay/crash recovery and trusted clock watermark; canonical lifecycle/epoch transition on online expiry; Demo without a Run timer; consistent usage counters and conditional License UI; integrated Windows/Linux/Docker tests; Help updates after the behavior exists. The pure verifier receives a time value; **it does not yet protect against host clock rollback or replay across restarts**. It has no network, file, Runtime, Authority, HA, UI, Preview or DEMO call sites. Portal signing remains disabled.
