# Wave 15 — DEV-AUTHORITY-UX Control

> GitHub live is the sole authority.

`LANE: DEV-AUTHORITY-UX`
`MAIN_ORDER_REV: 0006`
`ORDER_ID: DEV-AUTHORITY-UX-CODEX-QUEUE-04`
`ORDER_STATE: MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`
`PLANNED_BRANCH: work/w15-dev-authority-ux`
`WORK_BRANCH: work/w15-dev-authority-ux`
`FC0A_RELEASE_APPROVED: YES`
`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`
`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`
`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: AUTHORITY_UX`

## Activation — ACTIVE

Main activated this lane after final FC0-A audit rev 0014 returned:
`ACCEPTABLE / FC0A_RELEASE_APPROVED`.

The work branch was created directly from the exact base above.

On `SIGA`:
1. re-read this control live;
2. revalidate the exact work-branch head;
3. inspect the FC0-A base and subtract already-integrated work;
4. begin implementation only inside this lane's owned scope;
5. do not merge or widen frozen contracts.

## Activation history

Historical prerequisite — now satisfied: Main recorded `FC0A_RELEASE_APPROVED`, wrote the exact base SHA/tree, created the branch and changed the order to ACTIVE.

## Mission after activation

Build the user-facing administration UX on top of frozen backend Authority:
- Users + role/profile assignment;
- protected role CRUD;
- capability grouping;
- hierarchy/scope selector;
- truthful effective-permission preview;
- truthful multi-role/inheritance UX;
- clear denial/error feedback without frontend authority shortcuts.

## Primary ownership

- `web/scada-web/src/engineering/UserAdministration.tsx`
- `web/scada-web/src/engineering/userAdministrationApi.ts`
- `web/scada-web/src/engineering/userAdministration.css`
- lane-local administration UI

## Frozen contracts to consume

- FND-02/AUTH-04 capability/scope/effective-permission evaluator;
- Identity/session authority.

## Forbidden

No role-name privilege, no backend evaluator redesign, no Runtime Session Class-as-role, no client authorization authority, no password/secret exposure.

## DEV delivery

Deliver isolated code PR, exact head/tree, changed files, implemented UX matrix, cheap focused evidence, backend-authoritative cases left `PENDING_FOR_CODEX`, known gaps and non-actions.

Main returns material product gaps to this DEV. After Main acceptance, CODEX owns direct-API tampering, denial/adversarial, mounted locale/effective-permission and exact-head T1 validation.

Required prefix:
`DEV-AUTHORITY-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Cross-lane control:
`docs/WAVE15-PARALLEL-DEV-CONTROL.md`.


## Main candidate review — CHANGES_REQUIRED / stable role-key identity

Exact reviewed candidate:
- PR `#346`;
- head `3986475b20e4a72969159bfaed003ad5d72626d4`;
- tree `50342dfc9f5b8716e7daa25fcbd4ee29d78b271e`.

Main accepts the overall Authority UX direction:
- canonical backend policy load/preview/versioned apply;
- capability grouping without role-name privilege;
- scope-node + IncludeDescendants authoring;
- configured multi-role preview explicitly labeled non-authoritative;
- current-session effective capability truth comes from backend;
- backend orphan/self-lockout/concurrency validation remains authoritative;
- existing Users/password/session UX is preserved;
- no evaluator/store/backend Authority redesign.

One UI identity defect must be corrected before CODEX.

Live exact-head evidence:
- local user assignments persist role **keys**;
- backend `ValidateMutationAsync` rejects any policy that would orphan an assigned role key;
- the UI labels `roleKey` as the stable key but renders it as an unconditional editable input for every persisted role;
- `selectedRoleUsers` is recomputed from the draft key, so changing an assigned role key immediately makes the UI appear as if no users are assigned even though the persisted assignments still reference the old key;
- Preview then necessarily fails with `AUTHORITY_POLICY_ORPHANED_ASSIGNMENT` unless assignments are separately changed first.

This is misleading and violates the stable-identity UX expected for protected role CRUD.

Disposition:

`DEV-AUTHORITY-UX -> MAIN COORDINATOR — CHANGES_REQUIRED`

### Historical correction order (SUPERSEDED)

`ORDER_ID: DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: 3986475b20e4a72969159bfaed003ad5d72626d4`

Required bounded correction:

1. Treat the role key as stable identity after the role exists in the loaded baseline policy.
2. Existing/baseline roles:
   - role key must be read-only/non-editable;
   - display a localized explanation that assignments use this stable key.
3. Newly created/duplicated roles that have never been applied may choose/edit their generated key before Preview/Apply.
4. After successful Apply, that key becomes baseline/stable and therefore immutable in the normal editor.
5. Delete protection/assigned-user summary must continue to resolve against the persisted stable key; draft edits must never make assigned users silently disappear from the protection UI.
6. Do not implement client-side role-key privilege semantics.
7. Do not add an atomic backend role-rename migration in this lane. If a future role-key migration is ever required, it needs a separate Main contract because user assignments are stored separately.
8. Add focused model/mounted regression:
   - baseline assigned role key cannot be edited;
   - new unapplied role key can be edited;
   - assigned-user protection remains truthful;
   - backend orphan/self-lockout remains the final authority.

The original T1 `36063513301` was metadata-invalid. Main fixed the PR declaration and re-opened the same SHA, producing new T1 `36067636970`; that run is allowed to finish but cannot close this product correction on the old head.

After correction return:

`DEV-AUTHORITY-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

No CODEX routing or merge yet.


### Additional exact-head T1 evidence — compile defect on reviewed head

Natural T1 after Main repaired PR metadata:

`36067636970`

Results on exact old head `3986475b20e4a72969159bfaed003ad5d72626d4`:
- classifier: SUCCESS;
- Common sanity: SUCCESS;
- focused .NET: SUCCESS;
- focused Chromium: SUCCESS;
- Web semantic build: **FAILURE**.

Exact TypeScript errors:
- `AuthorityPolicyAdministration.logic.ts(16,63) TS2345`;
- `AuthorityPolicyAdministration.logic.ts(18,99) TS2345`;
- a generic `number` is passed to a Map whose key type was inferred as the literal capability union `0 | 1 | ... | 14`.

This is candidate-causal and belongs to the already-open DEV correction.

Required correction also includes:
- make the capability lookup map/type accept the declared `number | string` wire-normalization path without weakening the canonical capability table;
- preserve unknown capability fail-closed/display behavior;
- Web build must be green on the corrected head.

Do not rerun `36067636970` unchanged. The corrected candidate requires a new natural T1.


## Successor Main review — corrected role-key candidate still has one Web compile defect

Exact corrected candidate reviewed live:
- head `9fd2462f43c74b085e58be91dbec9ebdf18514c5`;
- tree `bdd7ac76c3566aef81e249a9db91810674b7dfe8`;
- correction delta from `3986475b...`: 5 commits / 5 bounded Authority-owned files;
- natural T1 `36071729747`: classifier/Common/.NET/Chromium SUCCESS, Web semantic build FAILURE.

Main accepts the stable-role-key correction direction:
- persisted/baseline role keys are read-only;
- new unapplied role keys remain editable;
- successful Apply establishes the new stable baseline;
- assigned-user/delete protection resolves through persisted identity;
- generic capability lookup is explicitly typed without changing the canonical capability table.

The corrected head is not CODEX-ready because Web build fails at:
- `AuthorityPolicyAdministration.tsx(341,48) TS2345`;
- `AuthorityPolicyAdministration.tsx(345,38) TS2345`.

Both are candidate-causal nullability errors: `baseline: AuthorityPolicyDocument | null` is passed to helpers requiring a non-null `AuthorityPolicyDocument`.

### Historical correction order — rev 0004 (SUPERSEDED)

`ORDER_ID: DEV-AUTHORITY-UX-STABLE-ROLE-KEY-COMPILE-03`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: 9fd2462f43c74b085e58be91dbec9ebdf18514c5`

Required bounded correction:
1. close only the nullable-baseline compile boundary around assigned-user protection / role-key editability;
2. preserve fail-closed behavior while baseline is unavailable;
3. preserve editability of genuinely new unapplied roles after a baseline is loaded;
4. do not redesign Authority policy, role migration, capability semantics or backend authorization;
5. add/adjust focused regression only if needed to make the null/loading boundary explicit;
6. run a new natural exact-head T1; no rerun of `36071729747` on the unchanged head.

Return:
`DEV-AUTHORITY-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

No CODEX route, merge or T2 authorization yet.


## Replacement Main follow-up — corrected candidate still CHANGES_REQUIRED

Exact corrected candidate:
- head `9fd2462f43c74b085e58be91dbec9ebdf18514c5`;
- tree `bdd7ac76c3566aef81e249a9db91810674b7dfe8`;
- natural T1 `36071729747`: FAILURE.

Accepted correction direction:
- baseline/applied role keys are immutable;
- new unapplied role keys remain editable;
- assigned-user protection resolves against persisted identity;
- capability Map typing is now compatible with generic numeric wire values.

Remaining candidate-causal defect:
- Web semantic build fails at `AuthorityPolicyAdministration.tsx(341,48)` and `(345,38)`;
- nullable `baseline: AuthorityPolicyDocument | null` is passed to helpers requiring a non-null `AuthorityPolicyDocument`.

### Historical correction order — rev 0004 (SUPERSEDED)

`ORDER_ID: DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: 9fd2462f43c74b085e58be91dbec9ebdf18514c5`

Required delta is minimal:
1. preserve the accepted stable-role-key behavior;
2. make the nullable baseline boundary type-safe and truthful;
3. do not weaken helper contracts or invent fallback policy state;
4. return one new exact candidate SHA/tree;
5. run a new natural exact-head T1.

Do not rerun `36071729747` unchanged. No CODEX route or merge is authorized.


## CURRENT MAIN DISPOSITION — rev 0005

`ORDER_ID: DEV-AUTHORITY-UX-CODEX-QUEUE-04`

`ORDER_STATE: MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`

`CANDIDATE_PR: #346`

`CANDIDATE_HEAD: d96e685daf7ddb190cefc67c6ba975d71a779a52`

`CANDIDATE_TREE: 184f8a49bdf15a34ce7ba96bc99168f5152a4fef`

`CANDIDATE_T1: 36076143618 / SUCCESS`

This section is the **only current Authority order**. All earlier correction-order sections are historical/superseded evidence.

Main acceptance:
- stable persisted role keys remain immutable;
- genuinely new/unapplied roles remain editable after a real baseline exists;
- assigned-user/delete protection remains tied to persisted identity;
- generic capability lookup compile defect is closed;
- nullable baseline boundary is fail-closed without synthetic policy state;
- exact-head T1 is green across Web, Common, focused .NET, focused Chromium and final gate.

DEV-AUTHORITY-UX must now wait and must not mutate the branch unless Main explicitly returns a material defect.

The shared sequential CODEX executor remains assigned to Script Engineering PR #344. Authority is queued only; no implicit reroute, merge or T2 authorization exists.

When Main later publishes an explicit Authority CODEX route, validation emphasis remains:
1. unauthorized direct-API mutation/tampering;
2. backend orphan-assignment and self-lockout final authority;
3. 401/403/409 denial and concurrency behavior;
4. effective-permission truth across relevant identities/scopes;
5. mounted pt-BR/en/es behavior as applicable;
6. exact final-head T1 after any bounded validation-driven delta.


## Independent Main cumulative audit — rev 0006

Audited queued candidate:
- PR #346;
- head `d96e685daf7ddb190cefc67c6ba975d71a779a52`;
- tree `184f8a49bdf15a34ce7ba96bc99168f5152a4fef`.

Additional live checks:
- UserAdministration role picker reads `/api/auth/roles`;
- backend `/api/auth/roles` projects canonical `IAuthorityPolicyStore.Snapshot().Roles`, not Engineering package roles;
- canonical Authority store rejects persisted roles without stable IDs;
- policy preview/apply validates orphan assignments, self-lockout and expected-version concurrency on the backend;
- successful policy Apply refreshes Users + canonical role choices.

Finding:
`NO_NEW_BLOCKER_OR_MAJOR`.

State remains:
`MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`.

The earlier CODEX adversarial obligations remain mandatory before merge; this audit does not replace them.
## CURRENT MAIN ORDER — rev 0007

`ORDER_ID: DEV-AUTHORITY-UX-INTEGRATION-RECONCILE-05`

`ORDER_STATE: DEV_RECONCILIATION / AUTHORIZED`

PR:
- #346;
- current head `d96e685daf7ddb190cefc67c6ba975d71a779a52`;
- prior exact-head T1 `36076143618`: SUCCESS;
- prior state `MAIN_ACCEPTED_FOR_CODEX / QUEUED`.

Current live integration:
`wave15/corrections-integration@3819715ba3a015a182c97b2a4ebcb4de447da717`.

GitHub now reports PR #346 `mergeable=false` after the Foundation integration sequence.

Main diff audit:
- Authority lane owns 7 Web Authority/UserAdministration files;
- integration advanced 121 commits from the old shared baseline;
- **no Authority-owned file overlaps the integration-changed file set**.

Therefore this is classified as:
`STALE_BRANCH_INTEGRATION_RECONCILIATION / NO_KNOWN_FUNCTIONAL_OVERLAP`.

DEV-AUTHORITY-UX is authorized to:
1. reconcile/rebase its branch onto exact integration SHA `3819715ba3a015a182c97b2a4ebcb4de447da717`;
2. preserve the previously Main-accepted Authority UX delta exactly unless compilation/test adaptation is mechanically required by the new baseline;
3. do not redesign Authority semantics;
4. do not absorb Licensing/FND-05/FND-07 behavior;
5. resolve only mechanical ancestry/build/test effects from the new integration;
6. run a new natural exact-head Wave 15 T1;
7. return durable handoff on PR #346 with exact SHA/tree, ancestry/reconciliation summary and T1.

If the new baseline exposes a material semantic conflict, stop that semantic change and return the exact evidence to Main.

After reconciliation, DEV must WAIT. CODEX adversarial validation remains mandatory before merge.
## CURRENT MAIN DISPOSITION — rev 0008

`ORDER_ID: DEV-AUTHORITY-UX-RECONCILED-CODEX-QUEUE-06`

`ORDER_STATE: MAIN_ACCEPTED_FOR_CODEX / RECONCILED / DEV_WAIT`

Exact reconciled candidate:
- PR #346;
- head `e3c646b5f0e6fb06b509f44b0ed866ec61fd1db6`;
- exact base `3819715ba3a015a182c97b2a4ebcb4de447da717`;
- ancestry: ahead 1 / behind 0;
- PR: OPEN / MERGEABLE.

Exact-head T1:
- `36280754990` / #111: SUCCESS;
- classifier SUCCESS;
- Web SUCCESS;
- Common SUCCESS;
- focused .NET SUCCESS;
- focused Chromium SUCCESS;
- final gate SUCCESS.

Main reconciliation verification:
- the 7 Authority/UserAdministration owned blobs at the reconciled head are byte-for-byte identical to prior Main-accepted head `d96e685daf7ddb190cefc67c6ba975d71a779a52`;
- no semantic adaptation was introduced;
- the new candidate is therefore the previously accepted Authority UX delta transplanted exactly onto the frozen Foundation integration baseline;
- no FND-05/FND-07/Licensing behavior was absorbed.

Prior Main findings remain closed:
- stable persisted role-key identity;
- assigned-user/delete protection;
- generic capability lookup typing;
- nullable baseline fail-closed boundary;
- no client-side authorization authority.

DEV-AUTHORITY-UX returns to WAIT.

CODEX adversarial validation remains mandatory before merge, emphasizing:
1. direct API tampering/unauthorized mutation;
2. orphan-assignment and self-lockout final backend authority;
3. 401/403/409 + expected-version concurrency;
4. effective-capability truth and scoped/multi-role behavior;
5. mounted pt-BR/en/es behavior;
6. exact final-head validation after any CODEX-owned bounded correction.

No merge/T2 is authorized.
## CURRENT MAIN ORDER — rev 0009

`ORDER_ID: DEV-AUTHORITY-UX-CODEX-ADVERSARIAL-SCARCE-07`

`ORDER_STATE: CODEX_ACTIVE / ADVERSARIAL_VALIDATION / SCARCE_BUDGET`

Exact candidate:
- PR #346;
- head `e3c646b5f0e6fb06b509f44b0ed866ec61fd1db6`;
- base `3819715ba3a015a182c97b2a4ebcb4de447da717`;
- T1 `36280754990` / #111: SUCCESS;
- PR: OPEN / MERGEABLE.

CODEX availability is restored with reduced usage budget.

Mission priority is **adversarial evidence per unit of CODEX time**, not redundant broad retesting.

CODEX should reuse the existing local CI parity harness, installed dependencies, cached Playwright/browser artifacts and already-green T1 evidence where safe.

### Required adversarial focus

1. direct API tampering / unauthorized Authority policy mutation;
2. 401/403 denial behavior;
3. expected-version / 409 concurrency behavior;
4. orphan-assignment rejection;
5. self-lockout rejection;
6. stable persisted role-key identity under mounted UI;
7. scoped + multi-role effective-permission truth;
8. current-session effective-capability truth remains backend-owned;
9. mounted pt-BR/en/es behavior where cheap to cover;
10. verify no selected-user configured-grant preview is presented as authoritative authorization.

### Scarce-budget execution guidance

Prefer, in order:
- existing focused tests already in the repo;
- targeted API-level probes;
- targeted Playwright specs;
- existing local harness components;
- only the cheapest additional test needed to close a real evidence gap.

Do not spend CODEX time rerunning the entire Linux parity battery unless:
- a CODEX-owned correction changes shared product/runtime infrastructure;
- a focused failure suggests cross-cutting breakage;
- Main/T1 evidence becomes stale because the exact head changes materially.

If no product defect is found:
- do not mutate the candidate;
- return a durable adversarial handoff on PR #346 with exact head, commands/specs executed, outcomes and residual risk;
- keep the existing exact-head T1 as the final lane gate unless a new commit is introduced.

If a bounded Authority UX/test defect is found:
- CODEX may correct it on the same branch only if it stays inside the Authority UX/test boundary;
- add focused regression;
- obtain a new natural exact-head T1;
- return the new exact head to Main.

If a backend/Foundation/security contract defect is discovered, do not broaden; return exact evidence to Main.

No merge is authorized by this order.
## CURRENT MAIN ORDER — rev 0010

`ORDER_ID: DEV-AUTHORITY-UX-CODEX-CONCURRENCY-FEEDBACK-08`

`ORDER_STATE: CODEX_BOUNDED_CORRECTION / AUTHORIZED / SCARCE_BUDGET`

Exact reviewed candidate:
- PR #346;
- head `e3c646b5f0e6fb06b509f44b0ed866ec61fd1db6`;
- T1 `36280754990`: SUCCESS.

CODEX adversarial evidence accepted:
- anonymous policy GET/preview/apply -> 401;
- authenticated viewer policy GET/preview/apply -> 403;
- orphan assignment -> rejected with `AUTHORITY_POLICY_ORPHANED_ASSIGNMENT`;
- self-lockout -> rejected with `AUTHORITY_POLICY_SELF_LOCKOUT`;
- policy version unchanged after rejected probes;
- focused/mounted Authority coverage otherwise passes, aside from one transient Vite/API ECONNRESET that passed on immediate isolated rerun.

### Main classification

`AUTHORITY_UX_CONCURRENCY_FEEDBACK_MISMATCH / BOUNDED_UX_TRUTH_DEFECT`

Observed frozen backend contract:
- stale `expectedVersion` is rejected by `ValidateMutationAsync`;
- preview and apply both return HTTP 400 with:
  `AUTHORITY_POLICY_CONCURRENCY_CONFLICT`;
- a later `TryReplaceAsync` race may still return HTTP 409.

No authorization or integrity bypass exists.

Current UX only appends the localized conflict/reload guidance when `AdministrationHttpError.status === 409`, so the normal stale-version 400 path renders generic rejection plus raw error code.

### Required bounded correction

Do **not** change the frozen backend status mapping in this lane.

Within Authority UX/test ownership:
1. recognize concurrency conflict by semantic server code `AUTHORITY_POLICY_CONCURRENCY_CONFLICT`, independent of whether HTTP status is 400 or 409;
2. preserve existing 409 handling for replacement-time races;
3. show the existing localized `conflictHint` for both stale-version paths;
4. preserve the raw/server error truth; do not fabricate success or automatically overwrite/reload unsaved edits;
5. add the cheapest focused regression proving:
   - 400 + `AUTHORITY_POLICY_CONCURRENCY_CONFLICT` -> conflict guidance;
   - 409 conflict -> conflict guidance;
   - unrelated 400 rejection -> no concurrency guidance;
6. if practical within the existing focused browser/model tests, prove preview and apply surface the same truthful guidance.

Expected implementation is small, likely in `AuthorityPolicyAdministration.tsx` and a focused test. CODEX may choose an equivalent minimal design.

After the correction:
- run only relevant focused Web/Playwright tests;
- obtain a new natural exact-head T1 because the candidate head changes;
- no full Linux parity battery is required unless focused evidence exposes a cross-cutting issue.

Return durable handoff on PR #346. No merge is authorized yet.
## CURRENT MAIN DISPOSITION — rev 0011

`ORDER_ID: DEV-AUTHORITY-UX-MAIN-FINAL-ACCEPTANCE-09`

`ORDER_STATE: MAIN_ACCEPTED / MERGE_AUTHORIZED / CODEX_WAIT / DEV_WAIT`

Exact accepted candidate:
- PR #346;
- head `f82a5662234e42a73b77f15fbdfd730872cc5cc1`;
- base `3819715ba3a015a182c97b2a4ebcb4de447da717`;
- PR: OPEN / MERGEABLE;
- exact-head T1 `36288094878` / #112: SUCCESS.

Main accepts the CODEX adversarial evidence:
- anonymous policy GET/preview/apply -> 401;
- viewer policy GET/preview/apply -> 403;
- orphan assignment rejected fail-closed;
- self-lockout rejected fail-closed;
- rejected mutations do not alter policy version;
- scoped/multi-role/effective-capability focused evidence remains green;
- no backend authorization or integrity bypass was found.

Main accepts the bounded CODEX correction:
- stale-version HTTP 400 with `AUTHORITY_POLICY_CONCURRENCY_CONFLICT` now receives the same localized concurrency guidance as HTTP 409;
- unrelated HTTP 400 remains an ordinary rejection;
- raw server error remains visible;
- frozen Authority backend was not changed;
- no automatic overwrite/reload of unsaved edits was introduced.

Exact CODEX delta from reconciled candidate:
- 1 commit;
- 3 lane-owned files;
- no backend, Foundation, workflow or integration mutation.

Focused local evidence:
- Web build PASS;
- focused model Playwright 11/11 PASS;
- one broader local mounted fixture had local-auth environment limitations, while the profile-owned exact-head Chromium job in T1 is SUCCESS.

Final disposition:
`MERGE: AUTHORIZED`

No further branch mutation before protected merge.
## CURRENT MAIN DISPOSITION — rev 0012

`ORDER_ID: DEV-AUTHORITY-UX-POSTMERGE-CI-10`

`ORDER_STATE: INTEGRATED / POSTMERGE_CI_PENDING / DEV_WAIT / CODEX_WAIT`

PR #346 merged successfully.

Integrated SHA:
`27412e4fbe47dbbb6573229364ee8319369ae076`.

Live compare confirms:
`wave15/corrections-integration == 27412e4fbe47dbbb6573229364ee8319369ae076`.

Accepted pre-merge evidence:
- exact candidate `f82a5662234e42a73b77f15fbdfd730872cc5cc1`;
- T1 `36288094878` / #112: SUCCESS;
- targeted CODEX adversarial validation: ACCEPTED;
- bounded concurrency-feedback correction: ACCEPTED.

Post-merge exact integrated CI:
- EliteSCADA CI `36288961537`;
- run #1583;
- event: push;
- exact head `27412e4fbe47dbbb6573229364ee8319369ae076`;
- currently queued.

No further Authority mutation is authorized.

Authority closeout waits only on classification of this exact integrated CI.
## CURRENT MAIN DISPOSITION — rev 0013

`ORDER_ID: DEV-AUTHORITY-UX-INTEGRATED-COMPLETE-11`

`ORDER_STATE: INTEGRATED / VERIFIED_COMPLETE / NO_FURTHER_MUTATION`

Final integrated SHA:
`27412e4fbe47dbbb6573229364ee8319369ae076`.

Post-merge EliteSCADA CI:
- run `36288961537` / #1583;
- Backend build/test/smoke: SUCCESS;
- Web build: SUCCESS;
- Chromium end-to-end: SUCCESS.

Pre-merge exact-head evidence remains:
- candidate `f82a5662234e42a73b77f15fbdfd730872cc5cc1`;
- T1 `36288094878` / #112: SUCCESS;
- CODEX adversarial validation accepted;
- bounded concurrency-feedback correction accepted.

Final disposition:
`DEV-AUTHORITY-UX = INTEGRATED / VERIFIED_COMPLETE`.

No further DEV/CODEX mutation is authorized without a new Main-classified defect.

