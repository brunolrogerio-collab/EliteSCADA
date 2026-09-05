# Wave 14 — Post-DEMO Licensing Trust Anchor / Production Signing Key

**Recorded:** 2026-09-05 BRT  
**Authority:** Product Owner decision during Wave 14 canonical EEE DEMO closure  
**State:** DESIGN LOCK / KEYPAIR GENERATED / PRODUCT INTEGRATION NOT IMPLEMENTED  
**Scope:** generic EliteSCADA offline licensing trust model

## 1. Product Owner decision

The normal EliteSCADA installation must not require a customer, operator or Engineering user to import a PEM verification key through the Licensing UI.

The production product should carry its normal **public license-verification trust anchor as part of the compiled/distributed product**. The offline License Generator keeps using the corresponding **private RSA signing key** supplied externally to the generator.

The customer-facing Licensing surface imports/installs only the signed EliteSCADA license artifact.

## 2. Current product audit

Audit against C11 candidate:

`3486a488181201062ba2f6790cd6deb7f5bccb8a`

Current generator behavior:

- `Scada.LicenseGenerator` explicitly asks for `PrivateKeyPath`;
- it opens the PEM through `RSA.ImportFromPem`;
- it signs the canonical license payload using RSA-PSS with SHA-256;
- the license payload includes a `KeyId`;
- the current WinForms generator defaults that field to historical/test-oriented `preview-1`.

Current Runtime behavior:

- `FileProductLicenseService` receives a dictionary of public RSA verification keys;
- `ProductLicensingConfiguration` currently builds this dictionary exclusively from `Licensing:VerificationKeys` configuration entries whose values are external file paths;
- the license `KeyId` selects the corresponding public key;
- an unknown `KeyId`, invalid signature, tampered payload, wrong machine fingerprint or expired license fails closed.

Therefore the current product supports the correct asymmetric cryptographic model but still requires an **external public-key file/configuration** for normal licensed production operation.

## 3. Security boundary

The production private signing key must **never** be embedded in EliteSCADA.

The private key must not be:

- committed to GitHub;
- placed in the Runtime or Engineering binaries;
- copied into the installer;
- shipped to customer machines;
- written into application/project packages;
- included in logs, CI artifacts, test reports or support bundles;
- exposed through Licensing UI or API.

Possession of the private key allows generation of cryptographically valid licenses, so embedding it in a customer-distributed binary would destroy the licensing trust boundary regardless of obfuscation.

The public verification key is intentionally distributable. It is not a secret. Embedding the public key in the product is cryptographically appropriate because it can verify signatures but cannot create them.

## 4. Production trust anchor v1

A new RSA 3072-bit production keypair was generated for this design lock.

Canonical production key identity:

`elite-prod-2026-01`

Public-key fingerprint:

- algorithm: SHA-256 over DER SubjectPublicKeyInfo;
- fingerprint: `62244a1ca23f4a03d581e3df8fb46508264e29cd13d8747992710d3b0b4aac72`.

Private PEM format generated:

- RSA 3072 bit;
- PKCS#8 PEM;
- header `-----BEGIN PRIVATE KEY-----`;
- unencrypted because the current generator calls `RSA.ImportFromPem` directly and does not request a PEM passphrase.

Public PEM format generated:

- SubjectPublicKeyInfo PEM;
- header `-----BEGIN PUBLIC KEY-----`;
- RSA 3072 bit.

**The private PEM is intentionally not stored in this repository.**

## 5. Required production implementation

The production Runtime must have a built-in verification-key registry containing at least:

- Key ID `elite-prod-2026-01`;
- the exact public key whose fingerprint is recorded above.

Normal installed operation must therefore validate licenses signed by this authority without requiring `Licensing:VerificationKeys:<keyId>=<external-path>` to be configured.

The built-in registry remains host-owned product configuration. It is not project Engineering state and must not enter `.escadapkg`.

## 6. External verification-key configuration

The current external `Licensing:VerificationKeys` mechanism may remain only if there is a deliberate product reason such as automated test fixtures, development/Preview signing authorities, controlled OEM/private-label deployments or migration/key-rotation tooling.

If retained, production policy must be explicit. A normal customer installation must not be able to replace the production trust anchor casually through the Licensing UI.

Any external override/extension mechanism must preserve fail-closed semantics and must not allow an unprivileged user to redefine trusted signing authorities.

## 7. Licensing UI contract

The normal Licensing UI should expose product/license operations, not cryptographic-authority administration.

Customer-facing flow:

1. EliteSCADA shows the machine request code;
2. license authority uses that request code in the offline generator;
3. generator signs the license with the private key for `elite-prod-2026-01`;
4. customer receives the resulting `.license` artifact/code;
5. user installs/imports that license through the Licensing surface;
6. EliteSCADA verifies it using the built-in public trust anchor.

There is no normal `Import PEM`, `Choose verification key` or private-key field in the customer Licensing page.

## 8. License Generator contract

For production license generation:

- private key file: `EliteSCADA-License-Signing-Private.pem`;
- `KeyId`: `elite-prod-2026-01`;
- output remains the normal EliteSCADA signed `.license` artifact;
- the private key is read for signing only and is not incorporated into the license file or generator executable.

A later implementation should change the generator's production-default Key ID away from `preview-1` to `elite-prod-2026-01`, while keeping deliberate test/Preview authorities separate.

## 9. Key rotation

The runtime format already carries `KeyId`, which provides a sound basis for key rotation.

Production implementation should support a set of built-in public keys rather than assuming one timeless key forever.

Recommended lifecycle:

1. add a new built-in public key with a new `KeyId` in a product update;
2. keep prior public keys trusted while licenses issued under them must remain valid;
3. start issuing new licenses under the new private key;
4. retire old public authorities only under an explicit compatibility/security policy.

Private-key compromise requires immediate rotation and must be treated as a licensing security incident.

## 10. Private-key custody

The production private PEM is a business-critical secret.

At minimum:

- keep more than one secure offline backup under Product Owner/company control;
- restrict read/write access to authorized license issuers;
- do not send it to customers or contractors merely to generate a license;
- do not place it in source control or ordinary cloud CI;
- record which `KeyId` each private key belongs to.

Loss of the private key does not invalidate already-issued licenses because verification uses the public key, but it prevents issuing additional licenses under that authority.

## 11. Required acceptance tests when implemented

At minimum prove:

1. standard production build validates a correctly signed `elite-prod-2026-01` license with no external public-key file configured;
2. the exact built-in public-key fingerprint is `62244a1ca23f4a03d581e3df8fb46508264e29cd13d8747992710d3b0b4aac72`;
3. license signed with the matching private PEM is accepted for the matching machine;
4. tampered payload/signature is rejected;
5. license signed by another RSA key but claiming `elite-prod-2026-01` is rejected;
6. unknown `KeyId` is rejected;
7. wrong-machine license is rejected;
8. expired license is rejected;
9. Licensing UI requires only the signed license artifact and exposes no normal private-key import;
10. private signing-key bytes/string/header are absent from product source, compiled distributable artifacts, installer and runtime configuration;
11. Preview/test signing authorities remain isolated from production authority;
12. rotation can add another public `KeyId` without silently invalidating licenses under retained authorities.

## 12. Classification

As of this record:

- asymmetric signed-license model: **IMPLEMENTED**;
- private external signing key in License Generator: **IMPLEMENTED**;
- `KeyId`-based public-key selection: **IMPLEMENTED**;
- external configured public verification-key files: **IMPLEMENTED / CURRENT MODEL**;
- production RSA-3072 keypair `elite-prod-2026-01`: **GENERATED**;
- private production PEM stored in GitHub: **FORBIDDEN / NOT PRESENT**;
- built-in production public trust anchor: **NOT IMPLEMENTED**;
- customer-facing removal of external public-key dependency: **NOT IMPLEMENTED**;
- this document: **DESIGN LOCK** only.
