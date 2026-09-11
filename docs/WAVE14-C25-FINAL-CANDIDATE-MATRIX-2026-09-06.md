# Wave 14 C25 — Final Candidate Matrix

**Prepared:** 2026-09-06 BRT  
**Checkpoint:** C25.10  
**State:** **CANDIDATE MATRIX PREPARED / AWAITING EXPLICIT PRODUCT OWNER ACCEPTANCE / NOT INTEGRATED**

> This document prepares an acceptance decision. It does not itself accept C25 and does not authorize any merge.

## 1. Proposed exact product candidate

Exact product/test SHA proposed for Product Owner acceptance:

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd`

Branch: `wave14/c25-post-demo`  
PR: #283 -> `wave14/corrections-integration`  
Tracking issue: #282  
Integration base: `c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

Coordination-only C25.9 closeout above the product candidate:

`e2441382e589350bd9b13476fd359d70a7c4b1ac`

The compare from `5193b812...` to `e2441382...` changes only:

- `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`;
- `docs/CURRENT-COORDINATOR-HANDOFF.md`;
- `LAST CHANGE.md`.

Therefore `e2441382...` is coordination authority only and does not replace `5193b812...` as exact product/test authority.

## 2. Exact product/test evidence

### Wave 14 C25 Post-Demo

Candidate `5193b812...`: run #314 / `34072225644` — **SUCCESS**.

Coverage includes:

- Core and API/Driver restore/build;
- Engineering Lock package/crypto and backend/lifecycle;
- reusable-library package/catalog/incorporation/backend lifecycle;
- Authority backup crypto/validation;
- System Recovery;
- atomic Authority-store replacement;
- Distributed Runtime foundation;
- contextual multilingual Help contract;
- React/Vite build;
- integrated Chromium Engineering Lock / Restore-first / Runtime session / reusable library / Help flows.

### Wave 14 C03 DNP3 Adapter

Candidate `5193b812...`: run #301 / `34072225635` — **SUCCESS**.

All five jobs passed:

1. managed adapter build/tests;
2. native OpenDNP3 host build on Linux;
3. native OpenDNP3 host build on Windows x64 plus dependency inspection;
4. real OpenDNP3 ↔ dnp3py L3 interoperability;
5. Windows commercial publish dependency gate proving required OpenDNP3 helper inclusion and absence of restricted Step Function / `dnp3` 1.6.0 bytes and dependency graph.

No blind rerun or validation weakening was used to obtain this green candidate.

## 3. Coordination-head revalidation

The documentation-only C25.9 closeout `e2441382...` was also revalidated after publication:

- Wave 14 C25 Post-Demo #316 / `34072728643` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #302 / `34072728640` — **SUCCESS**.

This proves that the coordination-only closeout did not disturb the exact product contracts beneath it. It still does not replace `5193b812...` as the product/test candidate.

## 4. Checkpoint acceptance matrix

| Checkpoint | Result | Final-candidate evidence / retained authority |
| --- | --- | --- |
| C25.0 Architecture/code audit | COMPLETE | Full audit retained in `docs/WAVE14-C25-FULL-CODE-AUDIT.md`; later integrated gates remain green. |
| C25.1 Engineering Lock domain/package/security | COMPLETE | Canonical package metadata, verifier/registry, compatibility and fail-closed package validation remain covered by current package/crypto gates. |
| C25.2 Engineering Lock backend enforcement | COMPLETE | Retained validated SHA `493acedc12079b73c02312a2d8355291664226b4`; current integrated backend/lifecycle gates remain green. |
| C25.3 Engineering Lock UI/lifecycle/package | COMPLETE | Retained validated SHA `a5fb9959fa15c060f51417de7bea84a5eec11e5b`; current integrated browser flow remains green. |
| C25.4 Restore-first / System Recovery | COMPLETE | Retained validated SHA `06948450c365009531d584b8b9d1d45e05c1aec8`; current backend + browser recovery remain green. |
| C25.5 Runtime Session UX | COMPLETE | Runtime session / Viewer / Interactive / View Only contracts remain covered by current integrated backend/browser gates. |
| C25.6 Reusable Resource Libraries | COMPLETE | Retained final SHA `1f17367defa03f903e68f585d068b4f23f82bef9`; current package/catalog/incorporation/backend/browser contracts remain green. |
| C25.7 Distributed Runtime Foundation | COMPLETE | Retained SHA `233002ded306c971858b356e1d2a50a89921da37`; current distributed Runtime gates remain green. |
| C25.8 Contextual multilingual Help/manual | COMPLETE | Final replacement product candidate `5193b812...`; C25 #314 and C03 #301 SUCCESS. |
| C25.9 Integrated regression/audit | COMPLETE | Cross-checkpoint audit found no remaining C25 product-code blocker; documentation drift repaired in `e2441382...`, itself revalidated by #316/#302. |
| C25.10 Exact final candidate / PO acceptance | MATRIX PREPARED | Technical candidate is `5193b812...`; **explicit Product Owner acceptance still required**. |

## 5. Binding candidate invariants

### Authority / security / lifecycle

- Backend Authority remains primary identity/capability authority.
- Engineering Lock is an additional application-content admission boundary and does not rewrite Authority capabilities.
- FullAccess does not bypass Engineering Lock.
- Working -> Save -> Revision -> Publish -> Activate remain distinct lifecycle transitions.
- Backend Active Revision is canonical Runtime application authority.
- Failed activation does not replace current Active authority.
- Restore/recovery uses validated atomic product contracts rather than direct store mutation.
- Licensing remains distinct from authentication, authorization, Engineering Lock, lifecycle and Runtime authority.

### Packages and recovery

- `.escadapkg` remains portable/self-contained and topology-neutral.
- Inspect/Preview/Apply do not silently become Activate.
- Authority backup, application package and Licensing remain separate authorities/artifacts.
- Restore-first never anonymously grants application authority after Authority restore; restored identities authenticate normally.

### Reusable libraries

- `.escadalib` is Engineering-only and follows Engineering Lock visibility/authority.
- Association != import and association alone does not mutate canonical Working content.
- `Usar` selectively incorporates a resource plus validated dependency closure.
- Incorporated content becomes project-owned canonical content.
- Disassociation removes catalog availability without deleting incorporated content.
- Final `.escadapkg` is self-contained.
- Runtime/Active never depends on an external `.escadalib`.

### Distributed Runtime / session

- Viewer/Interactive and voluntary View Only are effective-capability reductions, never replacements for Authority.
- Command/write enforcement remains server-side.
- Runtime Session Lease governs interactive authority.
- C25.7 is foundation only; production HA/replication/failover is not claimed by this candidate.

### Help/manual

- Installed Help is local/offline and same-origin.
- Stable language-neutral Topic IDs have pt-BR/en/es structural/semantic parity.
- All mandatory manual topics are present.
- Exactly 8 production communication Drivers are presented, including Modbus.
- `builtin.simulation` is excluded from production Driver count.
- Internal Memory and TAG Gateway are separate concepts.
- Driver detail is derived from shipped descriptors/configuration schemas.
- Server Script documentation is constrained to shipped APIs and actual lifecycle/sandbox behavior.
- Alarm / Operational Event / Audit remain separate domains.

### DNP3 commercial distribution

- Current commercial path is OpenDNP3-based.
- Native Linux and Windows hosts are validated.
- Independent dnp3py interoperability is validated.
- Commercial Windows publish includes the required native helper/notices.
- Restricted Step Function / `dnp3` 1.6.0 is absent from commercial publish bytes and dependency graph.

## 6. Out-of-scope / future work explicitly not claimed

C25 does **not** claim completion of production-grade HA, replication or automated failover features such as node replication, TAG Mirror, ReadyStandby, reference-device quorum, epoch/fencing, automatic/seamless failover or advanced commercial connection tiers.

These remain future-Wave work and are not C25 acceptance blockers unless the Product Owner explicitly changes scope.

## 7. External guard rails at matrix preparation

- #283: OPEN/DRAFT, not merged, targets only `wave14/corrections-integration`.
- #212: OPEN/DRAFT, not merged, target `main`; no merge authorization.
- C11 #263: OPEN/DRAFT, frozen exact head `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #266: OPEN/DRAFT, validation-only, **MUST NEVER MERGE**.
- Wave 13 issue #205: OPEN/paused.
- Wave 13 PR #207: OPEN/DRAFT, preserved head `fda87ba4445127c174f6ea533a6bcabaabc7bb20`.

## 8. Acceptance decision boundary

### Technical recommendation

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd` is the current exact C25 product candidate presented for Product Owner decision.

### Current decision state

**AWAITING EXPLICIT PRODUCT OWNER ACCEPTANCE.**

Do not infer acceptance from green CI, this matrix being committed, C25.9 being complete, PR mergeability, coordinator comments or absence of objections.

Acceptance must explicitly identify or unambiguously authorize the exact C25 candidate.

## 9. Authorized sequence only after explicit acceptance

After explicit Product Owner acceptance of the exact C25 candidate:

1. merge accepted C25 only into `wave14/corrections-integration`;
2. exact-SHA validate the resulting integration head;
3. synchronize/adapt frozen C11 to accepted C25 contracts using normal history-preserving integration;
4. revalidate canonical EEE application behavior through generic product paths;
5. export/version/freeze `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance;
6. run exact C11 gates;
7. launch/keep the canonical package in Preview Codespace for Product Owner homologation;
8. close #266 without merge after its validation-only role is complete;
9. #212 may merge to `main` only after a later explicit Product Owner authorization;
10. validate resulting `main`;
11. only then resume Wave 13 release/signing work from that new mainline.

No step above is authorized early by this document.
