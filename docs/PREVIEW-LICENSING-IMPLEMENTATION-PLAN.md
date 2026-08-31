# Preview/Demo Licensing Implementation Plan

Status: ACTIVE

This implementation plan executes issues #183 and #184 on `product/preview-demo-licensing` before Driver convergence is allowed to proceed to `main`.

## Slices

### A — Canonical licensing contracts

- entitlement tiers and normalized capacity semantics;
- license payload/version/key identity;
- explicit license states: Demo/Valid/Invalid;
- signed envelope codec and asymmetric verification;
- deterministic machine request-code contract.

### B — Product runtime gate

- remove the universal mutation-time 200-TAG ceiling;
- evaluate entitlement immediately before candidate Runtime activation;
- fail closed while preserving previous active Runtime;
- Demo <=200 TAG Run gate;
- licensed tier gate;
- invalid installed license gate.

### C — Demo session supervisor

- monotonic 300-minute continuous session deadline;
- clean lifecycle stop at expiry;
- explicit expired status/message;
- a later explicit Run creates a fresh session;
- injectable clock/time provider for deterministic tests.

### D — Product API/UI

- expose current mode/status/tier;
- expose machine request code;
- license import/status surface;
- show Run-block reason and Demo expiry clearly.

### E — Offline License Generator

- separate executable project with no product runtime dependency on private signing material;
- input: machine request code, tier and issuance metadata;
- private key loaded from explicitly supplied external PEM/PFX path/environment;
- signed versioned license output;
- fail closed when key material is absent/invalid;
- ephemeral-key tests prove round trip;
- Windows x64 publish command creates a standalone executable artifact.

## Acceptance

Full normal CI on exact head plus focused licensing tests. The PR remains Draft until all slices are complete.
