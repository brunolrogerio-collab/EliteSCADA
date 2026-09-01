# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-09-01 BRT**  
Operational status: **WAVE 12 #201 IN PROGRESS — AUDIT COMPLETE / IMPLEMENTATION STARTED**

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
9. `docs/LINUX-DEBIAN-DISTRIBUTION.md` when considering future packaging/distribution or dependency-license work;
10. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` only when Driver evidence is relevant.

Do not resume old Wave 11 feature branches. PR #195 is historical/superseded; PR #199 is the accepted Wave 11 implementation integration; PR #200 is the accepted owner-test application handoff. Issue #194 is closed/completed.

## 2. Accepted foundation through Wave 11

- Pre-Wave-11 issue #191: **COMPLETE / ACCEPTED / INTEGRATED** through PR #193.
- Repository/CI hygiene: **COMPLETE / ACCEPTED / INTEGRATED** through PRs #196 and #197.
- Wave 11 implementation PR #199: **MERGED** at main merge `57042b467471f4b1360e1642d5d160e6e66fc31c`.
- Wave 11 owner-test handoff PR #200: **MERGED** at product-code main `4ccc29cb4bb334dc473d8265f48a9c8601993413`.
- Wave 11 issue #194: **CLOSED / COMPLETED** after final evidence and continuity synchronization.

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

Final validated product-code main `4ccc29cb4bb334dc473d8265f48a9c8601993413`:

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

## 5. Wave 12 active state

Issue #201 — `Wave 12 — Hardening` is **IN PROGRESS**.

Preparation document: `docs/WAVE-12-HARDENING-PREPARATION.md`.
Active finding/slice ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.

Formal start evidence:

- live `main` was revalidated at `a2d865c017b8b8ad804f9270e5224ac1fa620ed0` after the mandatory document/GitHub review;
- branch `coordination/wave12-hardening` was created from that exact live `main`;
- no PR was open at start; issue #201 and intentionally deferred L4 issue #178 were the open issues;
- the latest product-code evidence remains successful Wave 11 workflow #14 / `33552016447` and EliteSCADA CI #1067 / `33552016454` at `4ccc29cb4bb334dc473d8265f48a9c8601993413`;
- no Wave 12 production-code acceptance or CI result is claimed yet.

Wave 12 is a hardening pass, not a feature-expansion wave. Its prepared scope covers fail-closed/recovery behavior, authorization/audit, persistence/restart, `.escadapkg` integrity, runtime resource/fault isolation, concurrency, diagnostic sanitization and regression/CI hardening.

The pre-implementation audit is complete. The first ordered slice covers realtime WebSocket client isolation, exact-snapshot/serialized Engineering saves, bounded JSON/CSV import ingress and `.escadapkg` export/import limit symmetry. Remaining findings cover persistence Apply concurrency, local-identity mutation races, audit-outage disposition and request/diagnostic consistency.

Explicit exclusions include new Drivers/protocols, Wave 13 Authenticode/release-signing implementation, Waves 14/15 owner validation/feedback and physical L4 claims.

## 6. Future Linux / Debian distribution direction

A future official **Linux x64 / Debian `.deb`** distribution is **SPECIFIED / NOT STARTED**. It is not part of Wave 12 and must not be started until the Development Lead explicitly requests the installable `.deb` version.

Authoritative specification: `docs/LINUX-DEBIAN-DISTRIBUTION.md`.

Prepared direction:

- Debian 12 `amd64` first, then Debian 13 homologation;
- `linux-x64` self-contained publish, initially multi-file inside one `.deb`;
- `/usr/lib/elitescada`, `/etc/elitescada`, `/var/lib/elitescada` layout;
- system user/group + `elitescada.service`;
- React/Pyodide included in the installed Kestrel-served product;
- PostgreSQL/TimescaleDB configured externally;
- Linux Machine Request Code and installed-license path using the existing signed-license contract; License Generator may remain Windows-only;
- clean-host install/upgrade/reboot CI plus installed Runtime/Web/licensing/Driver validation;
- SBOM and dependency-license audit.

Commercial gate: Step Function I/O `dnp3` 1.6.0 is publicly provided for non-commercial/non-production use. Before any commercial package includes/enables that Driver, obtain/record an appropriate commercial license or approve/revalidate an alternative. DNP3 licensing remediation is expected before the `.deb` front unless an explicitly authorized package excludes DNP3.

At this handoff there is **no Linux packaging branch, PR, implementation commit or Linux distribution CI**.

## 7. Exact next action

Continue the active Wave 12 branch:

1. implement and regression-test the first ordered slice recorded in `docs/WAVE-12-HARDENING-AUDIT.md`;
2. commit coherently and open a Wave 12 PR against `main`;
3. require EliteSCADA CI green on the exact PR head SHA and diagnose failures before any rerun;
4. disposition the remaining audit findings with fixes/evidence or explicit residual-risk records;
5. run specialized workflows according to actual impact, not as a substitute for universal CI;
6. keep Wave 13 signing, Linux `.deb` and physical L4 outside this branch.

## 8. Durable non-negotiable rules

- repository/CI state overrides stale chat/prose for implementation truth;
- no red universal CI into `main`;
- specialized path filters never excuse manual validation when architectural impact demands it;
- no test weakening to manufacture green evidence;
- Runtime presentation never reads mutable Working as Active truth;
- no Driver-to-Driver calls or canonical TAG/cache/event bypass;
- no plaintext protected material;
- licensing remains host-owned and private signing material never enters GitHub/CI/distributed product;
- Wave 13 retains mandatory Authenticode + trusted timestamp release signing;
- commercial packaging requires dependency-license review; DNP3 commercial inclusion requires an explicit license/alternative disposition;
- every material coordination transition is persisted before claiming completion.
