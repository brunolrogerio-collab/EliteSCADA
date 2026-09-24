# Wave 15 — DEV-AUTHORITY-UX Control

> GitHub live is the sole authority.

`LANE: DEV-AUTHORITY-UX`
`MAIN_ORDER_REV: 0003`
`ORDER_ID: DEV-AUTHORITY-UX-FC0A-01`
`ORDER_STATE: DEV_CORRECTION / STABLE_ROLE_KEY_IDENTITY`
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

### CURRENT CORRECTION ORDER

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
