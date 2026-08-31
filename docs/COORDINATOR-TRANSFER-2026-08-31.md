# EliteSCADA — Coordinator Transfer Checkpoint — 2026-08-31

This is a short transfer note for replacing the active ChatGPT/Development Coordinator without reconstructing state from old conversations.

## Read first

1. `PROJECT GOAL.md`
2. `LAST CHANGE.md`
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`
4. live PR #175
5. live issue #174
6. issue #180 for L3
7. `docs/LICENSING-AND-DEMO-MODE.md` for the locked future Demo/licensing contract

Live GitHub refs and exact-SHA Actions results override stale SHAs copied into prose.

## Current project checkpoint

- `main`: `d0a4e13816992b0a0eb0eb68c36e78c560cc1d88` at the last audit.
- coordinator branch: `coordination/driver-convergence-v3`.
- PR #175: Draft / Open / mergeable / DO NOT MERGE until controlled final integration.
- Driver coordinator convergence: **7/7 CLOSED**.
- independent Driver L2: **7/7 PASS / ACCEPTED**.
- last code-validated coordinator head: `6d340e8ca3baaabf138c19be2fb947297854e1f6`.
- validation: EliteSCADA CI #982 **SUCCESS**.
- CI #982: 750/750 backend tests, runtime smoke, Web and Chromium green.

## What changed most recently

The last functional code change introduced a **transitional 200-TAG Preview capacity safeguard**. It currently rejects creation/import of the 201st TAG and is validated by CI #982.

After that code was validated, the product requirement was refined:

- final unlicensed **Demo** mode must allow Engineering projects above 200 TAGs but block **Run** when project count exceeds 200;
- Demo continuous runtime is limited to **300 minutes per explicit Run session**;
- after expiry Runtime stops gracefully, shows an evaluation-expired message, and may be explicitly started again for a fresh 300-minute Demo session;
- licensed/evaluation entitlements above 200 TAGs remove the 300-minute runtime limit;
- licenses are hardware-bound and asymmetrically signed;
- initial tiers: 500 / 1000 / 1500 / 3000 / 5000 / Unlimited;
- EliteSCADA generates a copyable machine request code;
- a controlled offline License Generator signs the returned license;
- the private signing key never enters GitHub or normal product builds;
- a license present but invalid/mismatched to hardware blocks Run;
- no license installed means Demo mode.

**None of the Demo timer, hardware fingerprint, signed-license verification, License Generator or licensing UI is implemented yet.**

Authoritative specification: `docs/LICENSING-AND-DEMO-MODE.md`.

The current 200-TAG mutation-time implementation must later be refactored into a Run/activation entitlement gate. Do not report the final Demo/licensing behavior as implemented merely because `ProductCapacityPolicy.MaxTagsPerProject = 200` exists today.

## Immediate execution order

The current project stage remains:

`PR #175 controlled merge -> exact post-main CI green -> issue #180 integrated seven-Driver L3 PASS -> Wave 11`

Licensing/Demo implementation is a separate Preview/product-distribution track and does **not** replace the post-main L3 gate.

## L3

L3 begins only after Driver convergence is present on `main` and post-main CI is green.

One EliteSCADA build/runtime must operate MQTT, IEC-104, CIP/EtherNet-IP, OPC UA, DNP3, Siemens S7 ISO-on-TCP and BACnet/IP simultaneously, proving concurrent acquisition, writes/commands, readiness, identity isolation, one-peer fault isolation, recovery and clean shutdown.

Seven isolated L2 results are not L3.

## L4

Physical Driver validation is performed later using a Preview build. Acceptance authority is Development Lead Bruno Luiz Rogerio. Evidence is per exact build and real manufacturer/model/firmware.

## Do not accidentally change these facts

- PR #175 is not merged yet.
- L3 has not run yet.
- Wave 11 has not been released by L3 yet.
- Final Demo/licensing behavior is specified, not implemented.
- The 200-TAG code currently in the coordinator branch is transitional behavior and differs from the final Demo Run-gate requirement.
- Private license-signing material must never be committed to this repository.
