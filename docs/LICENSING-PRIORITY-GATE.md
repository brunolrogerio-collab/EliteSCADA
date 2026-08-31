# Preview/Demo Licensing Priority Gate

Date: 2026-08-31 (BRT)

## Decision

Development Lead reprioritized the EliteSCADA delivery sequence: the Preview/Demo licensing stage must be completed before Driver convergence is merged to `main` and before the post-main L3 laboratory begins.

This does **not** discard or reopen the already validated seven-Driver convergence. It pauses the mainline transition while licensing/product-distribution behavior is completed on a stacked product branch.

## Active implementation line

- Base coordinator line: `coordination/driver-convergence-v3`
- Licensing implementation branch: `product/preview-demo-licensing`
- Product contract: `docs/LICENSING-AND-DEMO-MODE.md`
- Detailed implementation tracker: issue #183
- Coordination priority gate: issue #184

## Completion gate

Before PR #175 may resume controlled mainline integration, all of the following must be implemented and validated:

1. Versioned host-owned entitlement/license contract.
2. Demo mode with Engineering allowed above 200 TAGs but Run blocked above 200 TAGs.
3. Demo industrial runtime limited to 300 continuous minutes per explicit Run session, enforced with monotonic time.
4. Graceful Runtime stop on Demo expiry plus clear user-visible status/message.
5. Deterministic hardware fingerprint and copyable versioned machine request code.
6. Asymmetric signed license verification in EliteSCADA using public verification material only.
7. Licensed TAG tiers: 500 / 1000 / 1500 / 3000 / 5000 / Unlimited.
8. Installed invalid/tampered/wrong-hardware license blocks Run; absent license means Demo.
9. Transitional mutation-time 200-TAG ceiling removed/refactored into the entitlement-aware Run/activation gate.
10. Controlled offline EliteSCADA License Generator.
11. A Windows x64 executable publish path for the License Generator.
12. Generator private signing material loaded only from a controlled external source and never committed or embedded in product/CI artifacts.
13. Automated acceptance coverage for Demo, expiry/restart, tiers, Unlimited, tamper, hardware mismatch, invalid/missing license and signed round-trip.
14. Exact-head normal CI green.

## Revised stage order

```text
Preview/Demo licensing implementation + License Generator
    -> exact-head CI green
    -> integrate licensing branch into coordinator line
    -> PR #175 controlled merge to main
    -> exact post-main CI green
    -> issue #180 integrated seven-Driver L3 PASS
    -> Wave 11
```

## Security lock

The production private signing key must never enter this repository, normal EliteSCADA binaries, customer packages or CI artifacts. Tests may generate ephemeral keys at runtime. The offline License Generator must fail closed when valid controlled signing material is not supplied.
