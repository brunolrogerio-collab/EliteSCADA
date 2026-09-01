# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-09-01 BRT**  
Operational status: **WAVE 11 ACCEPTANCE GATE SATISFIED / WAVE 12 #201 PREPARED — NOT STARTED**

> Repository/CI state is the continuity source. Read live refs and Actions before acting. Stable product intent is governed by `PROJECT GOAL.md`; exact mutable state belongs in `LAST CHANGE.md`.

## 1. Mandatory resume protocol

Read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/ROADMAP.md`;
5. `docs/WAVE-12-HARDENING-PREPARATION.md`;
6. issue #201;
7. `docs/CI-VALIDATION-POLICY.md`;
8. live `main`, open PRs/issues and exact Actions state;
9. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` only when Driver evidence is relevant.

Do not resume old Wave 11 feature branches. PR #195 is historical/superseded; PR #199 is the accepted Wave 11 implementation integration; PR #200 is the accepted owner-test application handoff.

## 2. Accepted foundation through Wave 11

- Pre-Wave-11 issue #191: **COMPLETE / ACCEPTED / INTEGRATED** through PR #193.
- Repository/CI hygiene: **COMPLETE / ACCEPTED / INTEGRATED** through PRs #196 and #197.
- Wave 11 implementation PR #199: **MERGED** at main merge `57042b467471f4b1360e1642d5d160e6e66fc31c`.
- Wave 11 owner-test handoff PR #200: **MERGED** at product-code main `4ccc29cb4bb334dc473d8265f48a9c8601993413`.

Accepted Wave 11 lifecycle authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Accepted behavior includes:

- protected `/api/runtime/application` from persisted Active Engineering, never mutable Working;
- fail-closed project/revision/persistence/package consistency boundaries;
- canonical Screen/Popup/Dynamo renderer/navigation mount;
- Active visual-asset authority with SHA-256/media-type/length validation;
- stable mount while Active project/revision identity is unchanged;
- Runtime `View` authorization without granting operator Working Engineering access;
- explicit Simulation fallback only when no Engineering Runtime is active;
- protected Slider/TAG writes through `/api/tags/{id}/write`;
- lifecycle proof for Active A -> Working isolation -> Active B;
- real imported PNG served from the Active persisted revision.

## 3. Exact Wave 11 acceptance evidence

Implementation head `a03237feed578066a8a62f5837adb60f100f412a`:

- dedicated Wave 11 validation before integration: **SUCCESS**;
- EliteSCADA CI #1062: **SUCCESS**.

Post implementation merge `57042b467471f4b1360e1642d5d160e6e66fc31c`:

- Wave 11 Active HMI Runtime #11 / `33548047016`: **SUCCESS**;
- EliteSCADA CI #1064 / `33548047037`: **SUCCESS** including Chromium E2E.

Owner-test handoff head `cc37be24ad8c8dc4594d99c5a3fd232dbf685d6f`:

- Wave 11 Active HMI Runtime #13 / `33551000846`: **SUCCESS**;
- EliteSCADA CI #1066 / `33551000852`: **SUCCESS**.

Final product-code main `4ccc29cb4bb334dc473d8265f48a9c8601993413`:

- Wave 11 Active HMI Runtime #14 / `33552016447`: **SUCCESS**;
- EliteSCADA CI #1067 / `33552016454`: **SUCCESS** including backend build/tests/runtime smoke, Web build and Chromium E2E.

No test/security/lifecycle boundary was weakened.

## 4. Owner-test Demo application

Final post-main artifact:

- artifact: `EliteSCADA-Wave11-Demo`;
- GitHub Actions artifact id: `9817878392`;
- artifact retention expiry: 2026-11-30;
- artifact ZIP digest: `sha256:2944b946bf0085e260aa147eb7da1711ba1ef9f496961724ebfe8053c1368f96`;
- application: `EliteSCADA-Wave11-Demo.escadapkg`;
- application size: 5,394 bytes;
- application SHA-256: `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`.

Independent inspection confirmed package format v2, Engineering schema v15, project `e2e-wave11`, active revision 2, screen `demo.overview`, `REVISION B ACTIVE`, one real PNG sidecar asset, and matching manifest SHA-256/length metadata.

This is the owner-test application package required by Wave 11. It is not a Windows installer or Preview executable.

## 5. Wave 12 handoff state

Issue #201 — `Wave 12 — Hardening` exists as **PREPARED / NOT STARTED**.

Preparation document: `docs/WAVE-12-HARDENING-PREPARATION.md`.

At this handoff point:

- no Wave 12 implementation branch exists;
- no Wave 12 PR exists;
- no Wave 12 production-code change has been made;
- no Wave 12 CI result is claimed;
- issue #201 is preparation-only until issue #194 is formally closed in this coordination cycle.

Wave 12 is a hardening pass, not a feature-expansion wave. Its prepared scope covers fail-closed/recovery behavior, authorization/audit, persistence/restart, `.escadapkg` integrity, runtime resource/fault isolation, concurrency, diagnostic sanitization and regression/CI hardening.

Explicit exclusions include new Drivers/protocols, Wave 13 Authenticode/release-signing implementation, Waves 14/15 owner validation/feedback and physical L4 claims.

## 6. Exact next action for the next Coordinator

After issue #194 is confirmed closed and issue #201 is marked **READY / NOT STARTED**:

1. re-read live `main`; documentation-only commits may have advanced it beyond product-code `4ccc29cb...`;
2. read all documents in the mandatory resume order above;
3. inspect current open issues/PRs and exact Actions state;
4. audit current failure/test surfaces before choosing hardening slices;
5. only then create a dedicated Wave 12 branch from the live `main`;
6. persist material findings and next actions before coding large slices;
7. use EliteSCADA CI as universal acceptance gate and run specialized workflows according to actual impact.

## 7. Durable non-negotiable rules

- repository/CI state overrides stale chat/prose for implementation truth;
- no red universal CI into `main`;
- specialized path filters never excuse manual validation when architectural impact demands it;
- no test weakening to manufacture green evidence;
- Runtime presentation never reads mutable Working as Active truth;
- no Driver-to-Driver calls or canonical TAG/cache/event bypass;
- no plaintext protected material;
- licensing remains host-owned and private signing material never enters GitHub/CI/distributed product;
- Wave 13 retains mandatory Authenticode + trusted timestamp release signing;
- every material coordination transition is persisted before claiming completion.
