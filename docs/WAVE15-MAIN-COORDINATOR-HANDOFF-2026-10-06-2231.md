# WAVE 15 — MAIN COORDINATOR HANDOFF — 2026-10-06 22:31 BRT

> GitHub live is the only authority. Revalidate every issue/branch/PR/CI before acting.
> This document records the outgoing coordinator state after the Product Owner requested a chat rotation because the context was saturated.

## 1. Fundamental rules

- Repository: `brunolrogerio-collab/EliteSCADA`.
- Product authority: product bytes delivered to GitHub and accepted by Main/Product Owner are authoritative.
- Tests validate product. Do not change accepted product merely to satisfy stale tests.
- DEV does not merge. Flow: `DEV -> MAIN AUDIT -> DEV CORRECTION -> T1 -> MAIN INTEGRATION`.
- CI watching: launch/observe about 1–2 minutes, then stop and revalidate later.
- No merge without Main/Product Owner authorization.
- `HA = High Availability` only.
- `HAB = Home Assistant Bridge`.
- Do not use the Product Owner as a messenger between chats; persist decisions/checkpoints in GitHub.

## 2. Exact integrated product checkpoint

Current accepted product merge:

`wave15/corrections-integration@27a9347e3d6f87db799a1541de2a27484b8bda51`

This is PR #550 / #549 S0 integration:
`W15 DRIVER: transient event + rich command S0 contract lock (#550)`.

Exact tested pre-merge head:
`a12be788549aa16870fbf64631df6e126617f45f`.

Exact T1:
- workflow run #929;
- run ID `37548910779`;
- `Scada.Core.Tests: 513 passed / 0 failed / 0 skipped`;
- merge was ancestry-only with zero product-file delta relative to the tested head.

The coordination commit containing this handoff is docs-only. Treat `27a9347e...` as the exact product checkpoint when reasoning about product bytes.

## 3. DRIVER-INTERACTION program

### S0 — completed

#549 is closed and integrated through PR #550.

Frozen invariant:
`STATE != EVENT != COMMAND`.

Integrated shared contracts include:
- bounded interaction scalar vocabulary;
- Transient Event definition/occurrence contracts;
- causality/correlation metadata;
- Capability -> Event reference;
- Rich Command definition/invocation/result/binding contracts.

S0 did NOT implement Event Runtime, Rich Command execution, Engineering persistence/schema, Script/HMI/Gateway consumers, or protocol adoption.

### S1 — ACTIVE / NOT STARTED

Issue:
`#554 — DRIVER-INTERACTION-S1 — bounded transient-event Runtime dispatch`

Branch:
`work/driver-interaction-s1-transient-runtime`

Release base / current branch head at outgoing revalidation:
`27a9347e3d6f87db799a1541de2a27484b8bda51`

State at handoff:
`ACTIVE / TRANSIENT_EVENT_RUNTIME_ONLY / NO_RICH_COMMAND_EXECUTOR / NO_SCHEMA_MIGRATION / NO_PROTOCOL_ADOPTION / NO_MERGE`

Branch was `ahead 0 / behind 0` against the accepted product checkpoint and had no product commit and no PR.

First action for the next Main coordinator:
1. revalidate #554 and branch live;
2. if still untouched, start the DEV with CHECKPOINT 1 only;
3. freeze publication envelope, definition resolver, ingress interface and bounded queue/backpressure;
4. implement core transient-event dispatcher + focused tests only;
5. STOP at checkpoint.

Important S1 boundaries:
- reuse `IScadaEventBus`, `InMemoryScadaEventBus`, `RuntimeEventGate`;
- do not create a second EventBus;
- prefer an `IScadaEvent` Runtime envelope around S0 `TransientEventOccurrence`; do not mutate S0 to implement `IScadaEvent` without Main decision;
- bounded queue, deterministic first ordering, no silent drop/coalescing;
- invalid/unknown definition fails closed;
- transient events remain ephemeral;
- no Operational Event/Alarm/Audit/Historian auto-promotion;
- no Server Script/HMI/Gateway/realtime consumer in S1;
- no protocol driver in S1;
- #500 remains diagnostics authority;
- non-authoritative HA instance must not forward canonical transient events;
- if current authority seam is insufficient: `BLOCKED_HA_AUTHORITY_SEAM / MAIN_DECISION_REQUIRED`.

S2 Rich Command Runtime must NOT start until S1 is Main-accepted unless Main explicitly changes sequencing.

## 4. HA = High Availability — #534 CODEX_LOCAL

Issue #534 remains open.

Branch:
`work/w15-ha-restart-epoch-recovery`

Latest pushed backup head:
`f4e4adf2551d697fdcd57d3eff1ad91b29c203e9`

That branch is NOT integration-ready.
Against product checkpoint `27a9347e...` it was:
- `diverged`;
- ahead 1 / behind 138;
- merge-base `47894cba7138f82cde9316e6271393f91b2d67d8`;
- 33 changed files in the compare.

No PR and no CI were active for #534 at outgoing revalidation.

Latest local/CODEX evidence recorded in #534 includes:
- two-node local HA UI exercised;
- controlled switchover and failback observed;
- Active/ReadyStandby transitions observed;
- remote DB topology shown healthy on both nodes;
- 5,000-TAG project loaded and sample runtime values observed;
- build/UI and focused HA tests had local passes;
- backup commit pushed to GitHub.

Still NOT accepted as final:
- formal 5,000-TAG throughput/latency benchmark;
- complete remote DB replacement/migration proof on clean stack;
- stale HA-history/UI observation still needs investigation (`authority-runtime-promotion` shown `running` after effective authority already converged);
- Tag Monitor crashed during one capacity attempt;
- no Main audit/PR/T1/integration.

Rule for next coordinator:
`DO NOT REBASE OR MERGE #534 BLINDLY`.
Wait for/obtain final CODEX handoff, then audit the exact GitHub branch against live integration and classify every overlap before any convergence.

## 5. Industrial PLC roadmap — #551

Both research lanes are now Main-accepted and closed.

### Mitsubishi — #552 — ACCEPTED / CLOSED

Decision:
`MITSUBISHI_MELSEC = GO_WITH_GATES`

Frozen v1 direction:
- DriverType `mitsubishi.melsec.mc`;
- `SLMP 3E / MC Protocol QnA-compatible 3E`;
- TCP / Binary;
- persistent connection / one outstanding request / no pipeline;
- first public L4 families: `FX5U-32MT/DS` and iQ-R bench `R04ENCPU + R35B + R63P + RX40C7 + RY40NT5P`;
- core areas `X/Y/M/L/B/D/W`, `R` profile-gated;
- types Bit/Int16/UInt16/Int32/UInt32/Float32;
- 0401 primary read; 0403/0406 bounded read optimization;
- 1401 canonical contiguous write;
- no blind write retry/replay after ambiguous dispatch;
- ConnectionTest/PointRead yes; generic Browse/Discover no;
- #500 diagnostics only;
- no shared SDK/Runtime/schema delta required;
- built-in managed .NET codec/session;
- no production dependency;
- legal/public naming review required.

Final research branch/head:
`research/industrial-mitsubishi-melsec@8de7fc9101ebcf46fd1b62159d4dce5c8b82e32c`

Do not implement from that old research branch. Any product branch must start from the then-current Main-authorized integration.

### Panasonic — #553 — ACCEPTED / CLOSED

Decision:
`PANASONIC_MEWTOCOL = GO_WITH_GATES`

Frozen IDs:
- `panasonic.mewtocol.tcp`;
- `panasonic.mewtocol.serial`.

Frozen v1:
- MEWTOCOL-COM;
- TCP + existing #469 Host Serial;
- first family scope after L4: FP0R, FP-XH, bounded current FP7 R-series;
- types Boolean/UInt16/Int16;
- contacts X/Y/R/L/T/C;
- words WX/WY/WR/WL/DT/LD;
- MEWTOCOL7 later;
- MEWTOCOL-DAT not v1;
- FP7 MC is outside Panasonic native scope and must reuse common Mitsubishi/MC provider if ever productized;
- built-in .NET codec/session;
- no production dependency;
- #500 diagnostics only;
- legal/public naming review required.

Implementation order remains:
`MITSUBISHI_FIRST -> PANASONIC_SECOND`.

No Mitsubishi/Panasonic product branch is active at handoff.
With #554 and #534 occupying product attention, do not fan out industrial product coding unless Product Owner/Main explicitly changes capacity/sequencing.

## 6. HOME/BUILDING roadmap state

Completed/integrated foundations and drivers include:
- #531 HOME common S1;
- Shelly #532/#535;
- ESPHome #536/#537;
- HAB #538/#544;
- #543 managed sidecar + typed Host Resource foundation;
- #549 DRIVER-INTERACTION-S0.

Research accepted:
- #539 KNX/DALI;
- #541 Zigbee2MQTT + Native Zigbee;
- #542 Matter + Z-Wave JS;
- #545 Bluetooth/BTHome;
- #546 Transient Event + Rich Command;
- #547 Tuya + Intelbras.

Important remaining sequencing:
- full Z2M button/action, BTHome event, Native Zigbee remote and Z-Wave Central Scene adoption waits for S1 transient-event Runtime;
- ordinary truthful state/read/write work can be independent where it does not need S1;
- Tuya is preferred first cloud; Intelbras after vendor/commercial qualification;
- DALI remains gateway-first; no native DALI Runtime is justified;
- no generic CloudConnectorFoundation is authorized.

Do not release multiple protocol product branches merely because research is complete.

## 7. Open PRs

At outgoing revalidation, the only open PR was:
`#362 — feat(preview): add safe local workbench operator`.

It is unrelated Preview infrastructure and is NOT part of the current Driver/Industrial sequence.
Do not merge it accidentally.

## 8. Coordinator pacing and CI rules

Normal DEV checkpoints:
1. live takeover/audit before broad implementation;
2. meaningful slice / about 2–4 substantive commits;
3. before long T1/broad CI;
4. after launching CI and observing <=2 min;
5. after classifying RED before another large correction pass;
6. before context saturation;
7. before architecture expansion.

Every checkpoint should record exact HEAD/tree/base/merge-base/ahead/behind/files/tests/CI/blockers/next <=3 and `NO_MERGE`.

RED classification:
`PRODUCT / TEST_STALE / ENVIRONMENT / HA_HOTSPOT / SHARED_HOTSPOT / UNKNOWN`.

## 9. Immediate next coordinator action order

1. Read this handoff, latest #305, #554, #534 and #551.
2. Revalidate GitHub live; never trust these SHAs without checking.
3. Confirm the accepted product checkpoint ancestry from `27a9347e...` and identify any docs-only handoff tip above it.
4. Start/continue #554 CHECKPOINT 1 only if the branch still has no product work.
5. Keep #534 isolated under CODEX_LOCAL until an explicit final GitHub handoff is auditable.
6. Keep Mitsubishi/Panasonic research accepted/closed; no product branch until Main explicitly releases it.
7. When #554 finishes, Main audit -> correction if needed -> exact-head T1 -> integrate.
8. After S1 acceptance, choose the first event-capable protocol lane deliberately; do not fan out all HOME drivers at once.
9. Do not touch PR #362 unless Product Owner explicitly returns to Preview-workbench work.

## 10. Product Owner action

The Product Owner should only need to:
- send `SIGA` for continuation;
- make product/priority decisions when Main asks;
- perform hardware/commercial/vendor actions that cannot be automated.

Main/DEV chats must write their own corrections/checkpoints directly to GitHub.

Disposition:

`OUTGOING_MAIN_HANDOFF / S0_INTEGRATED / S1_RELEASED_UNSTARTED / HA_CODEX_LOCAL_NOT_INTEGRATED / MITSUBISHI_RESEARCH_ACCEPTED / PANASONIC_RESEARCH_ACCEPTED / NO_INDUSTRIAL_PRODUCT_BRANCH / NO_MERGE_PENDING`
