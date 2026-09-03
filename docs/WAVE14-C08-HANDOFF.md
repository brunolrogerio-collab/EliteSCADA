# W14-C08 — Coordinator Handoff

**Package:** W14-C08 — Python Script Assistant / Project Object Browser  
**State:** **READY FOR COORDINATOR INTEGRATION**  
**Branch:** `wave14/c08-python-script-assistant`  
**Validation PR:** draft PR #228 — validation only / **DO NOT MERGE DIRECTLY TO `main`**  
**Mandatory package base:** `375519e8d87fe76f74121bf9e84f99b98c5ee23a`  
**Validated product candidate:** `ff3460507fae3a90995bd4360f776f1bf045fad1`

> This handoff document is committed **after** the exact product candidate was validated. The documentation-only handoff commit does not supersede `ff3460507fae3a90995bd4360f776f1bf045fad1` as the validated product-code baseline.

## 1. Delivered scope

C08 now provides a Script Engineering assistance surface that consumes the existing canonical project/runtime contracts rather than introducing parallel authorities.

Delivered behavior:

- searchable Project Object Browser for canonical project objects;
- TAG discovery using stable TAG identity and `DataSourceId` metadata when available;
- read-only vs writable TAG awareness in generated snippets;
- safe mediated `elite_scada.tag_write(reference, value)` through the existing authorized Runtime/backend TAG-write path;
- separate Client Memory discovery and read/write snippets;
- Screen and Popup object discovery from the Engineering project model;
- canonical visual-property discovery through the existing visual-property schema/registry;
- generic visual property read/write/clear/tween snippets without one-off Python methods per property;
- Dynamo discovery through public interfaces only, without exposing internal authoring children;
- capability/API Help catalog derived from the actual Client Visual Python bridge contract;
- Monaco insert-at-cursor with selection/undo boundaries rather than whole-script replacement;
- basic guided Button Click -> Add Action insertion flow;
- pt-BR, English and Spanish Script Assistant copy;
- live Python reference diagnostics connected to the current Monaco `source`, including unsaved edits;
- regression coverage for stable TAG identity, source identity, visual schema exposure, Dynamo encapsulation, Client Memory, public/reserved capability separation, TAG write and live editor integration.

## 2. Security and architecture invariants preserved

C08 preserves the existing product authority boundaries:

- backend remains the canonical authority for TAG write authorization and validation;
- Pyodide receives no direct Driver, database, filesystem, shell/process, arbitrary-network, browser-DOM, browser-storage or credential authority;
- direct shared TAG mutation and server-memory write remain unavailable;
- Engineering Preview keeps process TAG writes disabled (`tagWriter: null`) so Preview execution stays side-effect-safe;
- normal Runtime TAG writes reuse the existing `/api/tags/{id}/write` mediated path instead of adding a bypass;
- canonical visual-property APIs and Runtime precedence remain intact;
- Dynamo internals remain encapsulated behind their public Engineering interface;
- no direct React/DOM mutation path was introduced;
- backend authorization remains the security boundary even when the Assistant marks a TAG as writable.

### Public API vs reserved bridge protocol

The final contract deliberately distinguishes two surfaces:

- `CLIENT_VISUAL_PYTHON_CAPABILITIES` is the **official product capability list** exposed to Script Assistant/API Help;
- `CLIENT_VISUAL_PYTHON_PROTOCOL_CAPABILITIES` is the complete internal reserved protocol vocabulary used for fail-closed bridge validation.

`tag.write` is an official product capability.  
`backendOperation.request` remains protocol-reserved and fail-closed, but is **not** advertised as a supported Python product API because the official provider does not expose it.

## 3. Final closure after the previous candidate

The previous product candidate `3d6087994926b75583320ddd966cadba53908395` exposed three deterministic Chromium failures in the Playwright report. They were classified from the report/trace rather than rerun blindly.

Final test-alignment commits:

1. `0a613e6882404d16c68e0df12cbe3b38627c1ad6` — removes an unrelated/incorrect Project Browser filter expectation from the capability-regression test while retaining the public/reserved capability assertions;
2. `acec1537d7b5dc243c7e3a9cfd8dbe689173e3e5` — updates the foundation contract test so `tag.write` is expected as public and `backendOperation.request` is verified as protocol-only;
3. `ff3460507fae3a90995bd4360f776f1bf045fad1` — updates the real Pyodide sandbox probe so `tag_write` is required as an official API instead of incorrectly treated as denied.

These closure commits modify only test files. They do not relax the product sandbox or worker policy.

Immediately before them, `3d6087994926b75583320ddd966cadba53908395` fixed worker protocol validation after the public capability list was narrowed, preserving the complete reserved protocol vocabulary internally.

## 4. Exact-head validation evidence

All required gates below completed **SUCCESS against the same product SHA**:

| Gate | Run | Result |
| --- | --- | --- |
| EliteSCADA CI | #1258 / `33778759245` | SUCCESS |
| Web build inside EliteSCADA CI | job `100727100586` | SUCCESS |
| Backend build, test and Runtime smoke inside EliteSCADA CI | job `100727100274` | SUCCESS |
| Chromium end-to-end inside EliteSCADA CI | job `100727576288` | SUCCESS |
| Wave 11 Active HMI Runtime | #188 / `33778759352` | SUCCESS |
| Preview Licensing CI | #210 / `33778759328` | SUCCESS |
| L3 Seven-Driver Lab | #165 / `33778759272` | SUCCESS |
| Interop Lab Smoke | #87 / `33778759264` | SUCCESS |

Validated product SHA for every row above:

`ff3460507fae3a90995bd4360f776f1bf045fad1`

No `[skip ci]` was used for product-code changes.

## 5. Final ancestry / base check

Final candidate ancestry was revalidated immediately before handoff:

- base: `375519e8d87fe76f74121bf9e84f99b98c5ee23a`;
- candidate: `ff3460507fae3a90995bd4360f776f1bf045fad1`;
- compare status: `ahead`;
- ahead: **38 commits**;
- behind: **0 commits**;
- merge-base: **exactly** `375519e8d87fe76f74121bf9e84f99b98c5ee23a`.

There is therefore no hidden rebase or integration-branch drift in the validated C08 candidate.

## 6. Known limits / deliberate fallbacks

These are not unresolved C08 blockers, but should remain explicit during integration and owner validation:

- `backendOperation.request` is intentionally not a public Script API until an official provider/product contract exists;
- Script Assistant reference diagnostics validate canonical project/API references; they are not intended to replace Python/Pyodide semantic analysis or runtime execution diagnostics;
- TAG write snippets are generated only through the supported mediated capability, while the backend remains authoritative for permissions/interlocks/final acceptance;
- Engineering Preview intentionally cannot perform process TAG writes;
- Dynamo internals are intentionally hidden; scripts should target public Dynamo parameters/canonical visual properties rather than internal children;
- guided action authoring is intentionally bounded to the implemented helper flow rather than becoming a second visual scripting engine;
- where older project data lacks stable Source identity metadata, the Assistant must surface that identity condition rather than invent a free-text canonical identity.

## 7. Coordinator integration checklist

When integrating C08 into `wave14/corrections-integration` / PR #212:

1. use `ff3460507fae3a90995bd4360f776f1bf045fad1` as the validated C08 product baseline;
2. preserve C04 authority for canonical TAG/DataSource identity and communication contracts;
3. preserve C05 authority for canonical visual-property schemas;
4. preserve C07 authority for Screen/Popup/Dynamo models and Dynamo public interfaces;
5. preserve C09 ownership of application shell/operator Runtime presentation;
6. if integration conflicts touch capability lists, keep the public-product vs reserved-protocol distinction described above;
7. if integration conflicts touch TAG writes, preserve the existing backend-authorized write path and Preview side-effect boundary;
8. rerun universal `EliteSCADA CI` on the integration head and every specialized workflow required by the combined changes;
9. do not treat PR #228 as permission to merge C08 directly to `main`;
10. record the final integrated exact SHA/evidence in the Wave 14 coordination memory.

## 8. Handoff status

No known C08 functional blocker remains after exact-head CI validation. C08 is ready for Coordinator integration subject to cross-package conflict resolution and post-integration validation on the Coordinator-owned integration head.
