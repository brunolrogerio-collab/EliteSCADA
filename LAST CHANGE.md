# LAST CHANGE — EliteSCADA

Date: 2026-08-31 (BRT)

## Read first

Stable product intent: [`PROJECT GOAL.md`](PROJECT%20GOAL.md)  
Operational source of truth: [`docs/CURRENT-COORDINATOR-HANDOFF.md`](docs/CURRENT-COORDINATOR-HANDOFF.md)  
Short transfer checkpoint: [`docs/COORDINATOR-TRANSFER-2026-08-31.md`](docs/COORDINATOR-TRANSFER-2026-08-31.md)

Live GitHub refs and exact-SHA Actions evidence override stale SHAs copied into prose.

## Current checkpoint

- `main` at last audit: `d0a4e13816992b0a0eb0eb68c36e78c560cc1d88`.
- Active coordinator branch: `coordination/driver-convergence-v3`.
- PR #175: **DRAFT / OPEN / MERGEABLE / DO NOT MERGE until controlled integration**.
- Driver coordinator convergence: **7/7 CLOSED**.
- Independent product-path L2: **7/7 PASS / ACCEPTED**.
- Latest exact code-validated coordinator head: `6d340e8ca3baaabf138c19be2fb947297854e1f6`.
- EliteSCADA CI #982: **SUCCESS**.

CI #982:

- backend build: 0 warnings / 0 errors;
- Core: 246 passed;
- Drivers: 347 passed;
- Historian: 23 passed;
- Security: 27 passed;
- PostgreSQL: 107 passed;
- total backend: **750 passed / 0 failed**;
- runtime smoke: SUCCESS;
- Web: SUCCESS;
- Chromium E2E: SUCCESS.

Documentation-only `[skip ci]` commits after `6d340e8...` do not create a newer code-validation claim.

## MERGED

- Wave 10: **CLOSED / MERGED / POST-MAIN GREEN**.
- Common seven-peer interoperability laboratory infrastructure: **MERGED** through PR #173.

Driver convergence described below is **not yet merged to `main`**.

## IMPLEMENTED IN PR

### Driver convergence / Engineering shared contracts

On Draft PR #175:

- Engineering schema v15 / canonical `CommunicationBinding`: CLOSED;
- MQTT: CLOSED;
- IEC-104: CLOSED;
- CIP / EtherNet/IP: CLOSED;
- OPC UA: CLOSED;
- DNP3: CLOSED;
- Siemens S7 ISO-on-TCP: CLOSED;
- BACnet/IP: CLOSED;
- shared readiness/runtime planner/factory composition preserved;
- Driver product-path L2: 7/7 PASS / ACCEPTED.

### Transitional Preview 200-TAG safeguard

Functional head `6d340e8...` / CI #982 currently implements a static project-wide 200-TAG capacity safeguard:

- canonical registry rejects creation of the 201st TAG;
- Engineering Preview/Apply rejects imports that would exceed 200;
- existing TAGs remain editable at the limit;
- oversized candidate runtime also fails through the capped registry.

This code is validated, but it is now explicitly **transitional behavior** and does not represent the final Demo/licensing contract.

## SPECIFIED / NOT IMPLEMENTED

Product decision locked on 2026-08-31:

### Final Demo mode

- no installed license => Demo;
- Engineering may contain more than 200 TAGs;
- Demo Run is allowed only when project count is <= **200 TAGs**;
- >200 TAGs blocks Run without deleting or truncating Engineering data;
- Demo industrial runtime maximum: **300 continuous minutes per explicit Run session**;
- at expiry Runtime stops gracefully, application/Engineering remains alive and a clear evaluation-expired message is shown;
- user may explicitly Run again for a fresh 300-minute Demo session;
- elapsed enforcement uses monotonic time.

### Hardware-bound licensing

- EliteSCADA generates a copyable versioned machine request code derived from a canonical hashed hardware fingerprint;
- controlled offline License Generator issues a signed license code/file;
- initial TAG tiers: **500 / 1000 / 1500 / 3000 / 5000 / Unlimited**;
- valid licensed/evaluation entitlement removes the 300-minute Demo runtime limit;
- valid license must match the current hardware;
- installed invalid/tampered/wrong-hardware license blocks Run and does not silently downgrade to Demo;
- absent license enters Demo;
- private signing key exists only in the controlled generator environment and MUST NOT be committed to GitHub, CI artifacts or normal EliteSCADA builds.

Not implemented yet:

- entitlement/license service;
- machine fingerprint/request-code generator;
- signed-license verifier/import/status UI;
- 200-TAG Demo Run gate;
- 300-minute Demo runtime supervisor;
- graceful Demo-expiry notification flow;
- offline License Generator;
- licensed tier enforcement.

Authority: `docs/LICENSING-AND-DEMO-MODE.md`  
Tracking issue: **#183**.

The next implementation must refactor the current mutation-time 200-TAG ceiling into the entitlement-aware Run/activation behavior instead of layering a second contradictory limit on top.

## L3 / L4 stage policy

### L3

After Driver convergence is merged to `main` and exact post-main CI is green, issue **#180** runs one integrated laboratory with all seven Drivers active simultaneously in one EliteSCADA build/runtime.

L3 must prove acquisition, supported writes/commands, shared readiness, cache identity isolation, one-peer fault isolation, recovery and clean shutdown.

Seven isolated L2 PASS results do not satisfy L3.

### L4

Physical Driver validation occurs later with a Preview build and does not block Wave 11.

Acceptance authority: **Bruno Luiz Rogerio, Development Lead**.

Evidence is per exact Preview build plus real manufacturer/model/firmware.

## NEXT

Immediate project order remains:

`PR #175 controlled final audit/merge -> exact post-main CI green -> issue #180 integrated seven-Driver L3 PASS -> Wave 11`

The Demo/licensing work in issue #183 is a separate Preview/distribution track. Do not insert it into PR #175 or treat it as already implemented unless the project ordering is explicitly changed.

## Last actual repository change

The most recent repository work after CI #982 is **documentation only**:

- locked the final Demo/licensing contract in `PROJECT GOAL.md`;
- created `docs/LICENSING-AND-DEMO-MODE.md`;
- revised `docs/PREVIEW-CAPACITY-POLICY.md` to distinguish transitional code from final Demo behavior;
- refreshed `docs/CURRENT-COORDINATOR-HANDOFF.md`;
- created `docs/COORDINATOR-TRANSFER-2026-08-31.md`;
- opened issue #183 for licensing implementation.

**No licensing/product code was committed after the conversation stalled.** The latest code-validation checkpoint remains `6d340e8...` / CI #982.
