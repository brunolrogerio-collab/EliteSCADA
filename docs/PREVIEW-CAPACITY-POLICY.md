# EliteSCADA Preview — Product Capacity Policy

Status: **TRANSITIONAL IMPLEMENTATION VALIDATED / FINAL DEMO POLICY SPECIFIED**  
Current functional head: `6d340e8ca3baaabf138c19be2fb947297854e1f6`  
Validation: **EliteSCADA CI #982 — SUCCESS**  
Final Demo/licensing contract: [`LICENSING-AND-DEMO-MODE.md`](LICENSING-AND-DEMO-MODE.md)

## 1. Current implemented behavior

The coordinator branch currently contains a validated transitional Preview safeguard:

**200 TAGs per project**

The limit is project-wide. It is not a per-Driver or per-Data-Source quota. TAGs from communication Drivers and internal memory sources all contribute to the same project total.

Current code at `6d340e8...` enforces the limit during Engineering/registry mutation:

- `ProductCapacityPolicy.MaxTagsPerProject = 200`;
- `InMemoryTagRegistry` rejects creation of the 201st TAG;
- existing TAGs can still be edited at the limit;
- Engineering Preview calculates the projected project count;
- Apply is blocked atomically when an import would exceed 200;
- runtime candidate construction uses the same registry.

This behavior is covered by `tests/Scada.Core.Tests/PreviewProductCapacityTests.cs` and is green in CI #982.

CI #982 evidence:

- Core: **246 passed**;
- Drivers: **347 passed**;
- Historian: **23 passed**;
- Security: **27 passed**;
- PostgreSQL: **107 passed**;
- total backend: **750 passed / 0 failed**;
- runtime smoke: **SUCCESS**;
- Web: **SUCCESS**;
- Chromium E2E: **SUCCESS**.

## 2. Final Preview/Demo behavior now locked

The current mutation-time 200-TAG cap is **not the final desired Demo behavior**.

The product decision made on 2026-08-31 supersedes that behavior for the future Preview distribution:

- Engineering may create/import/save projects containing more than 200 TAGs;
- without a valid license, the product is in **Demo mode**;
- Demo may Run a project containing at most **200 TAGs**;
- if the project contains more than 200 TAGs, **Run/activation is blocked**, but Engineering data remains editable and intact;
- Demo runtime is limited to **300 continuous minutes per Run session**;
- after 300 minutes, industrial runtime stops gracefully and the product displays an evaluation-expired message;
- the user may explicitly start Runtime again for a new 300-minute Demo session;
- a valid hardware-bound license removes the 300-minute limit and grants its licensed TAG tier.

Authoritative final contract:

`docs/LICENSING-AND-DEMO-MODE.md`

## 3. Licensed capacity tiers

Initial licensed capacities are:

- 500 TAGs;
- 1,000 TAGs;
- 1,500 TAGs;
- 3,000 TAGs;
- 5,000 TAGs;
- Unlimited.

A valid license above the Demo tier has no 300-minute continuous-runtime restriction under the current product contract.

## 4. Required refactor

Before the final Preview/licensing behavior can be called implemented, the current code must be refactored so that:

1. the canonical Engineering/TAG model no longer rejects the 201st TAG merely because the installation is unlicensed;
2. capacity enforcement moves to a host-owned **Run/activation entitlement gate**;
3. `200` becomes the Demo runtime entitlement rather than a universal TAG-registry hard ceiling;
4. licensed limits come from a verified product-entitlement provider;
5. invalid installed licenses block Run rather than silently falling back to Demo;
6. Demo sessions are supervised by the 300-minute monotonic continuous-runtime timer;
7. the previous active runtime remains intact when activation is rejected by entitlement/capacity validation.

The current `PreviewProductCapacityTests` must be revised rather than simply deleted. Their atomicity and previous-runtime safety intent remains useful, but expectations must follow the new Run-gate semantics.

## 5. L3 interaction

The post-main integrated seven-Driver L3 laboratory is a Driver/system-integration gate, not the licensing acceptance gate.

The L3 project can remain at or below 200 TAGs so it can execute under the currently available Preview-capacity code while Driver convergence is integrated to `main`.

Wave 11 remains blocked by L3, not by completion of the future hardware licensing system unless the roadmap is explicitly changed later.

## 6. Security boundary

The current 200-TAG code is capacity control/misuse deterrence, not cryptographic licensing.

The future license system uses hardware-bound request codes and asymmetric signed licenses. The private signing key must exist only in the controlled License Generator environment and must never be committed to GitHub or embedded in normal EliteSCADA builds.
