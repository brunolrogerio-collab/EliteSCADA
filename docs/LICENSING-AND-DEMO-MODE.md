# EliteSCADA — Demo Mode and Hardware-Bound Licensing

Status: **SPECIFIED / NOT IMPLEMENTED**  
Product decision: **2026-08-31**  
Authority: `PROJECT GOAL.md` + this document

## 1. Purpose

EliteSCADA Preview is intended to be distributable to external evaluators without becoming an unrestricted production SCADA installation if the build is copied outside the intended evaluation group.

The product therefore has two runtime entitlement states:

1. **Demo** — no valid installed license;
2. **Licensed** — a valid EliteSCADA license cryptographically bound to the current machine.

Licensing is a product-capability gate. It must not be implemented inside individual Drivers and must not change protocol semantics.

## 2. Demo mode

When no license is installed, EliteSCADA operates in **Demo mode**.

Demo limits:

- maximum runnable project capacity: **200 TAGs per project**;
- maximum continuous industrial runtime execution: **300 minutes per Run session**;
- the 300-minute limit is per continuous execution, not a cumulative lifetime allowance;
- after the runtime stops because the 300-minute evaluation period expired, the user may start Run again and receive a new continuous 300-minute Demo session.

### 2.1 TAG behavior

Engineering is allowed to contain more than 200 TAGs.

If the active/project candidate contains more than 200 TAGs and no valid license authorizes that capacity:

- **Run/activation MUST be blocked**;
- the existing active runtime must not be partially replaced or corrupted;
- the UI/API must report clearly that the Demo limit of 200 TAGs was reached/exceeded;
- editing, import/export and project preparation may continue.

Recommended user-facing meaning:

> Demo mode supports up to 200 TAGs. This project exceeds the evaluation limit and cannot enter Run without a valid license.

The final Demo contract therefore **supersedes the current transitional implementation** on the coordinator branch that rejects creation/import of the 201st TAG. That current implementation is validated code history, not the final desired Preview behavior.

### 2.2 300-minute continuous runtime behavior

At the beginning of every Demo Run session, the product starts a monotonic continuous-runtime allowance of **300 minutes**.

Requirements:

- use monotonic elapsed time for enforcement; wall-clock changes must not extend the session;
- normal application/Engineering UI remains alive when Demo runtime expires;
- industrial runtime is stopped gracefully through the normal runtime lifecycle;
- Drivers, subscriptions, polling loops and runtime-owned resources are disposed/stopped normally;
- the runtime must not exceed 300 continuous minutes in Demo mode merely because a browser is disconnected, the wall clock changes or a Driver is reconnecting;
- restarting Run starts a fresh Demo session;
- restarting Run must require a normal explicit runtime start/activation action, not silently auto-restart after expiry.

Required user-facing meaning after expiry:

> The 300-minute evaluation period has expired. Runtime was stopped. You may start Runtime again to continue evaluating EliteSCADA.

## 3. Licensed mode

A valid machine-bound license removes the 300-minute Demo runtime limit and grants the licensed TAG capacity.

Initial commercial/evaluation capacity tiers:

- **500 TAGs**;
- **1,000 TAGs**;
- **1,500 TAGs**;
- **3,000 TAGs**;
- **5,000 TAGs**;
- **Unlimited**.

A license issued for internal/customer evaluation above 200 TAGs uses the same entitlement mechanism and also has **no 300-minute continuous runtime limit**, unless a future explicitly versioned license feature adds a separate expiry/time entitlement.

If a valid license grants `N` TAGs and the project exceeds `N`:

- Run/activation is blocked fail-closed;
- Engineering data is not deleted or truncated;
- the user is told the licensed TAG limit and project TAG count.

## 4. Machine request code

EliteSCADA must provide a licensing UI that generates a **copyable machine request code** derived from stable hardware identity.

The request code is intended to be copied and sent by email or another normal business channel to the EliteSCADA licensing authority.

Requirements:

- do not expose raw motherboard/CPU/storage identifiers in the request code when a one-way canonical fingerprint can be used instead;
- collect only the minimum stable hardware identifiers necessary for machine binding;
- canonicalize identifiers deterministically before hashing;
- the request code must include a version so the fingerprint algorithm can evolve;
- the request code should include checksum/error detection for copy/paste;
- hardware identity collection failures must produce diagnostics rather than silently generating a weak universal fingerprint.

Initial preferred direction:

`stable hardware identifiers -> canonical normalized material -> SHA-256 machine fingerprint -> versioned copyable request code`

Exact hardware identifier weighting/tolerance must be tested on the supported Windows x64 target before being locked. A future controlled re-host/reissue workflow may be required for legitimate hardware replacement.

## 5. License issuance

EliteSCADA Development/Commercial control retains an **offline License Generator** that is not distributed with the normal EliteSCADA installer.

Generator inputs:

- machine request code;
- licensed TAG tier: 500 / 1000 / 1500 / 3000 / 5000 / Unlimited;
- license identifier;
- edition/purpose metadata where required;
- issue metadata that is safe to include in a customer license.

Generator output:

- a versioned signed EliteSCADA license code/file that can be copied/imported into the target EliteSCADA installation.

The generator must validate the machine request code before issuing a license.

## 6. Cryptographic trust model

Licenses must use **asymmetric digital signatures**.

Required trust boundary:

- the **private signing key exists only in the controlled License Generator environment**;
- the private signing key MUST NOT be committed to GitHub, embedded in EliteSCADA binaries, included in CI artifacts or distributed to customers;
- normal EliteSCADA binaries contain only the corresponding public verification key/key identifier;
- the license payload is canonical/versioned before signing;
- signature verification is fail-closed.

The signed payload must include at least:

- license schema/version;
- license ID;
- machine fingerprint/request identity;
- TAG entitlement/tier;
- edition/purpose metadata if used;
- signing key ID / algorithm version;
- issue timestamp or equivalent issuance metadata.

A concrete first signing algorithm should use a modern .NET-supported asymmetric primitive and remain versioned so cryptographic migration does not invalidate the overall licensing contract.

## 7. Runtime license states

The runtime entitlement provider must distinguish at least:

### No license installed

- state: `Demo`;
- runnable capacity: 200 TAGs;
- continuous runtime: 300 minutes.

### Valid license for this hardware

- state: `Licensed`;
- runnable capacity: entitlement from signed license;
- continuous runtime: unlimited under the current contract.

### License file/code present but invalid

Examples:

- signature invalid/tampered;
- signed by an unknown key;
- malformed/unsupported schema;
- hardware fingerprint does not match the current machine.

Behavior:

- **Run is blocked**;
- do not silently downgrade an explicitly installed invalid license to Demo;
- show a clear `LICENSE_INVALID`/hardware-mismatch diagnostic without exposing sensitive hardware details.

This distinction prevents a copied license from another computer from being treated as a harmless Demo activation.

## 8. Required product interfaces

Implement a protocol-neutral licensing boundary, for example conceptually:

- `ILicenseService` / `IProductEntitlementProvider`;
- immutable `ProductEntitlement` snapshot;
- `MachineRequestCodeService`;
- signed license parser/verifier;
- runtime gate consuming entitlement + project TAG count;
- Demo continuous-runtime supervisor;
- protected Licensing UI/API for request-code display, license import/status and diagnostics.

Drivers must not read license files or hardware identifiers directly.

The runtime gate must be host-owned and evaluated before publishing/replacing an Active Runtime candidate.

## 9. Required regression coverage

At minimum:

### Demo capacity

- 200 TAG project may Run;
- 201 TAG project may be engineered/saved but Run is blocked;
- blocked Run leaves previous active runtime intact;
- message identifies Demo 200-TAG limit.

### Demo runtime duration

Use injectable monotonic clock/timer abstractions so tests do not wait five hours.

Prove:

- Demo continues before 300 minutes;
- exactly at/after 300 minutes runtime stops through normal lifecycle;
- expiry does not terminate Engineering/application host;
- a later explicit Run starts a fresh 300-minute session;
- wall-clock manipulation does not reset/extend elapsed Demo time.

### License validation

- valid signature + matching hardware + 500 entitlement permits <=500 TAGs with no Demo time limit;
- equivalent cases for each tier and Unlimited;
- 501 TAGs under a 500 license blocks Run;
- invalid signature blocks Run;
- modified payload blocks Run;
- license from different hardware blocks Run;
- unknown schema/signing key fails closed;
- absent license enters Demo rather than invalid-license state.

### License generator compatibility

A fixture generated by the controlled generator must verify in the product using only the public key. No private signing key may appear in normal product tests/artifacts except an explicitly non-production test key isolated for deterministic unit tests.

## 10. Distribution/security boundary

This scheme is intended to provide meaningful commercial/demo licensing and deter casual unauthorized production use. It is not a claim of perfect anti-tamper protection against an attacker capable of patching/rebuilding the executable.

Future hardening may include signed installers/binaries, protected license storage, anti-tamper measures and licensing telemetry only if privacy/security requirements justify them.

No licensing feature may weaken industrial runtime fail-safe behavior or put the signing private key at risk.

## 11. Current implementation gap

As of the 2026-08-31 coordinator handoff:

- Driver convergence: **7/7 CLOSED in Draft PR #175**;
- L2: **7/7 PASS / ACCEPTED**;
- current functional Preview capacity code at `6d340e8ca3baaabf138c19be2fb947297854e1f6` / CI #982: **IMPLEMENTED IN PR / VALIDATED**, but it blocks creation/import of the 201st TAG;
- Demo 200-TAG **Run gate**: **SPECIFIED / NOT IMPLEMENTED**;
- Demo 300-minute continuous-runtime supervisor: **SPECIFIED / NOT IMPLEMENTED**;
- hardware machine request code: **SPECIFIED / NOT IMPLEMENTED**;
- signed license verification: **SPECIFIED / NOT IMPLEMENTED**;
- offline License Generator: **SPECIFIED / NOT IMPLEMENTED**;
- licensed tiers and unlimited entitlement: **SPECIFIED / NOT IMPLEMENTED**;
- Licensing UI/status/import: **SPECIFIED / NOT IMPLEMENTED**.

The next coordinator must not report these licensing features as implemented until code + exact-head CI evidence exists.
