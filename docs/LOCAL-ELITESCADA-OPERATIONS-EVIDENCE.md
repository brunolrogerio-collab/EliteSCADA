# EliteSCADA local operations — evidence and lessons

This document preserves evidence from the local Wave 15 Preview harness and the Visual Studio local operator. It distinguishes what was actually exercised from implementation intent and unresolved questions. It is not product acceptance evidence and does not authorize a new Preview journey.

Evidence labels:

- `CONFIRMED` — reproduced by a regression or observed in a completed run with a recorded exact checkpoint.
- `OBSERVED` — directly reported by a run/operator, but not sufficient to establish the underlying cause or general behavior.
- `HYPOTHESIS` — plausible explanation that still needs discriminating evidence.
- `NOT_TESTED` — no valid exercise has established the behavior.

## Environment and provenance

- `CONFIRMED` — Environment A ran on a Windows host with Docker Desktop's Linux engine 29.8.0. Its accepted final readiness checkpoint was harness `preview/w15-first-project-env-harness@bb451fa6e07982ac12384895f6097d5833761d16`, tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`, on product base `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.
- `CONFIRMED` — the final accepted ENV_A state was `NOT_STARTED / PREPARATION=READY`: no dedicated product/audit containers or volumes remained, while the provenance-bound tool image and dependency volumes were retained. A second fresh session started from the same accepted SHA without `npm install`, `npm ci`, or `dotnet restore`.
- `CONFIRMED` — the local-operator delivery is a distinct infrastructure branch, `preview/w15-vs-local-runner`, based on the accepted harness. Always read the current HEAD before use; a development-profile session records the local launch request separately and is not an ENV_A acceptance SHA.
- `OBSERVED` — on an earlier attempt, status found a session recorded as `RUNNING` while dependency preparation was `REQUIRED` and its provenance manifest was absent. The session/preparation identity could not be reconciled; the attempt was sealed and not resumed as a Preview journey. The cause of the missing manifest was not established.
- `CONFIRMED` — the final replacement separates persistent, hash-keyed dependency preparation from resettable product/audit state. Provenance binds the harness/tree, product base, dependency Git blobs and tool image. Missing/mismatched preparation fails closed instead of silently restoring or reusing stale artifacts.

## Lifecycle commands and what has actually been proven

The supported Visual Studio interface is `scripts/preview/elite-local.ps1`:

| Command | Contract | Evidence / limit |
|---|---|---|
| `launch` | One-command local development entry point: inspects only the isolated development profile, resumes a matching saved session, or prepares exact dependencies then starts it. It never auto-resets or adopts the audit profile. | `CONFIRMED` — static/operator regressions cover the separate profile, unique evidence mount, safety checks and current local developer authorization marker. `NOT_TESTED` — full Docker lifecycle via `launch` on this updated SHA until this commit is installed and exercised. |
| `prepare` | Builds the pinned local tool image and resolves locked .NET/Web dependencies into external, provenance-keyed volumes. It does not create product state. | `CONFIRMED` — successful on accepted ENV_A SHA `bb451fa...`; repeated preparation returned `READY_REUSED`. Earlier TLS failure and recovery are recorded below. |
| `start -Profile audit -AcceptedHarnessSha <sha>` | Creates a new audit workbench only when the exact Main-accepted SHA matches HEAD and preparation is `READY`; reuses an existing saved workbench without resetting it. | `CONFIRMED` — clean start reached the supported first-run UI on the accepted ENV_A SHA. A later fresh start used `--no-build --pull never` and no package install/restore. |
| `launch` → internal development `start` | Creates a local developer workbench on exact current HEAD only after the Product Owner requests launch; records `LOCAL_OWNER_DEVELOPMENT`, never Main acceptance. | `NOT_TESTED` — full runtime on the updated profile implementation remains pending. |
| `status` | Read-only snapshot of Git/session/provenance identity, container and DB/API/Web health, resumability and local URLs; remains useful when Docker is unavailable. | `CONFIRMED` — repeated status stayed stable on the accepted session; status did not change the manifest hash. `CONFIRMED` — #360 operator check covered unavailable Docker. |
| `pause` / `stop` | Gracefully stop the application and database containers while preserving the workbench identity, product volumes, evidence and checkpoint. `stop` is an alias for `pause`. | `CONFIRMED` — repeated pause/resume cycles preserved the same session. #360 `stop` was checked with zero containers (`STOPPED_CONTAINERS=0`); active-session `stop` on the new wrapper is not separately proven. |
| `resume` | Reopens only the saved session with matching provenance; it never creates a new workbench or seeds/reset product data. | `CONFIRMED` — repeated resume returned the same session and UI state, including after Docker Desktop restart. |
| `restart` | Recreates application/database containers against the same named product volumes and waits for health; unlike reset, it is intended to preserve state. | `CONFIRMED` — the accepted predecessor harness stopped/restarted services during lifecycle checks. `NOT_TESTED` — a data-marker preservation proof using the new #360 `restart` command with an active workbench. |
| `diagnose` | Writes a local report with identity/health and recent service logs; common authorization/password/token patterns are redacted. Review the report before sharing. | `CONFIRMED` — redaction regression rejects leaked bearer, password, token and query-token values. `NOT_TESTED` — full diagnose capture from a live failing stack on the #360 branch. |
| `reset -Force` | Explicit destructive boundary: removes only the dedicated product/audit project resources, archives local evidence and preserves tool/dependency preparation. A fresh session receives a new identity. | `CONFIRMED` — reset returned `NOT_STARTED / PREPARATION=READY`; the next clean start did not restore packages. `CONFIRMED` — #360 wrapper refuses reset without `-Force` before Docker/state access. |

### Provenance-manifest loss / orphaned state

- `OBSERVED` — during the sealed attempt, a running session record existed without its expected preparation manifest; status reported `PREPARATION=REQUIRED`. No product interaction or destructive recovery was attempted in that attempt.
- `HYPOTHESIS` — the session metadata and separately keyed preparation state became orphaned across worktree/runtime identity changes. The evidence did not identify the exact deletion or mutation that removed the manifest.
- `CONFIRMED` — the current harness has explicit fail-closed behavior: resume rejects saved sessions lacking required provenance; start refuses to reuse dedicated containers/volumes when the session manifest is missing (`ORPHANED_PREVIEW_STATE`). It directs the operator to inspect and explicitly reset rather than guessing identity.
- `NOT_TESTED` — destructive fault injection that removes a live session or dependency manifest while preserving all related resources. Do not conduct it against the Product Owner's active workbench.

## Dependency identity and shell line endings

- `CONFIRMED` — the original dependency identity used materialized working-tree bytes. Two clean checkouts of the same Git tree with LF vs CRLF could therefore calculate different input hashes and miss the matching manifest/cache. Main accepted this as the root cause of the cross-checkout instability.
- `CONFIRMED` — identity now uses committed Git blob IDs, then hashes the ordered dependency inputs. The regression creates separate LF and CRLF clones, asserts distinct working bytes but identical tree and dependency identity, then commits an actual input change and asserts that the identity changes.
- `CONFIRMED` — embedded Bash text is normalized to LF before invocation. The same regression verifies CRLF/CR input becomes LF-only before execution.
- `OBSERVED` — a PowerShell-to-Docker Bash invocation also exposed argument/quoting fragility during preparation. The wrapper was changed to transport the verification payload as base64 before decoding inside Bash; the final preparation/provenance checks passed with TLS verification intact.
- `CONFIRMED` — final dependency preparation succeeded with pinned Node 24.19.0, .NET SDK 10.0.400 and Playwright 1.62.1 assets; a second prepare reused the same prepared state.

## Readiness, ports, Docker and failure evidence

- `CONFIRMED` — Environment A publishes Web only on host loopback, normally `http://localhost:5173/`. API health is checked through the Web same-origin `/health` route and internally at `127.0.0.1:5080`; 5080 is not published to the host. TimescaleDB uses its Compose network on 5432 without publishing that port to the host.
- `CONFIRMED` — readiness requires healthy TimescaleDB plus healthy Web and API checks. A Compose “container started” message alone is not the readiness gate.
- `CONFIRMED` — final reset left zero dedicated preview containers/volumes and host port 5173 free. `NOT_TESTED` — deliberate host-port collision and resource-exhaustion scenarios; no memory/disk threshold has been validated here.
- `CONFIRMED` — one earlier start brought DB/API/Web healthy, then the orchestration script threw because it tried to assign optional `lastTransitionMessage` on a new/deserialized PowerShell object. This was a harness failure, not an application health failure.
- `CONFIRMED` — the add-or-update transition helper was regression-tested on new, JSON-reloaded and repeatedly updated session objects. On the exact repaired candidate, fresh start/status/pause and multiple independent-process pause/resume cycles passed.
- `OBSERVED` — a later fresh startup failed npm TLS validation with `UNABLE_TO_VERIFY_LEAF_SIGNATURE` under local Norton HTTPS inspection. No certificate validation was disabled and no global trust store was changed.
- `CONFIRMED` — the bounded fix permits an operator to opt into a root certificate already trusted by Windows for the preparation container only. Public certificate material is mounted read-only for dependency preparation and then removed; its thumbprint/hash are recorded. The accepted preparation and restore then passed. The trust root is not carried into product start/resume.
- `CONFIRMED` — Docker Desktop was paused/restarted, its engine returned healthy, and the same session resumed. The database and initial UI state were retained. This proves the tested ENV_A Docker Desktop recovery path, not Windows reboot recovery or a native installed service.

## Persistence and destructive boundaries

- `CONFIRMED` — the lifecycle proof retained the same session after pause/resume and a real Docker Desktop restart. After the user created only the initial Administrator through the normal UI, the same “create project / no persisted project” state remained after resume. No project was created.
- `CONFIRMED` — database/runtime state and preparation resources are separate named/external volumes. Reset removes only the dedicated product/audit stack and archives evidence; preparation resources survived and were reused by a subsequent clean start.
- `NOT_TESTED` — project, license, Historian data and other populated product records surviving restart/reset. No project was created in the accepted ENV_A proof; reset is intentionally destructive to the local product database and must never be described as preserving product records.
- `HYPOTHESIS` — committing evidence or changing the active runtime checkout can contaminate an audit because the container mounts the repository at `/workspace` and session identity is tied to exact committed inputs. The positive isolation test used a separate evidence worktree/commit and confirmed that runtime HEAD, dependency key and READY status stayed unchanged. Keep audit notes/screenshots outside the runtime worktree or in the ignored evidence directory.

## Diagnostic and evidence-sharing rules

- `CONFIRMED` — operator diagnostics are local-only by default, contain recent logs only after redaction, and do not intentionally collect database rows, cookies, signing keys or credentials. The automated redaction check covers common bearer/password/token/query-token patterns, not every proprietary secret format.
- `OBSERVED` — detailed attempt-1 findings and screenshots were embargoed; the database snapshot was preserved, checksum-verified, and restore-tested in a disposable TimescaleDB container before the dedicated old stack was reset. The attempt remained sealed/inconclusive and was not treated as a product defect or a new black-box run.
- `NOT_TESTED` — exhaustive secret-redaction against arbitrary application payloads. Always inspect a generated report before posting it to GitHub or sending it outside the machine.

## Implications for the next Preview and installed services

- `CONFIRMED` — local ENV_A readiness is infrastructure evidence only. It does not certify Codespaces forwarding, remote latency, or the Product Owner's end-to-end Preview journey. ENV_B and human Preview keep their own exact branch, session and acceptance gates.
- `CONFIRMED` — the local Preview operator is development infrastructure, not an installer/service contract. Issue #361 owns future Windows Service and Linux systemd operations. The intended installed-service surface is `start | stop | restart | status | diagnose`; ordinary service lifecycle must preserve project/database/Authority/license/Historian/configuration. Destructive purge/reset is not a normal service command.
- `CONFIRMED` — Main recorded container-native OCI distribution as a preferred architecture candidate in #363, but explicitly marked it as requiring technical validation and **not authorized for implementation**. #360 remains local operator/evidence only; Windows native packaging remains until a qualified Windows container-host spike establishes otherwise. Do not infer that the Preview Compose files are an installer or a supported production deployment.
- `CONFIRMED` — the candidate architecture forbids binding license entitlement to ephemeral container identity; replacing an image on the same authorized host must not force license reissue. It prefers an external PostgreSQL/TimescaleDB topology for constrained Edge, and targets `linux/amd64` plus `linux/arm64`, subject to an explicit host/runtime/Driver/resource qualification matrix.
- `NOT_TESTED` — Windows Service Control Manager or Linux systemd installation, identity/permissions, startup ordering, readiness integration, crash recovery, upgrade/rollback, log rotation, and machine reboot continuity.
- `NOT_TESTED` — whether pause/resume has safe, useful semantics for an installed service. Do not copy the Preview operator's `pause`/`resume` behavior into SCM/systemd by assumption.

## Open questions

1. What exact event removed/orphaned the provenance manifest in the sealed attempt, and should the operator support a non-destructive manifest reconstruction from immutable labeled assets?
2. What are the supported behavior and operator message when port 5173 is already occupied, Docker Engine is stopped mid-transition, or storage/memory is exhausted?
3. Should `restart` on the #360 operator get a dedicated populated-state marker regression before the local operator mission closes?
4. What bounded timeouts/retry policy define DB/API/Web readiness on a cold Windows Docker Desktop start without masking a real failure?
5. Which diagnostic redaction formats are required beyond the current bearer/password/token regression, and what is the review/retention policy for local reports?
6. For #361, what service account, directory ACLs, secret storage, upgrade/rollback and recovery contracts apply separately to Windows SCM and Linux systemd?
7. Is `pause`/`resume` meaningful for an installed service, or should only stop/start/restart exist there?

## Evidence references

- Main's ENV_A acceptance and final proof: issue #305 comments `5856654198`, `5856722029`, `5856739345`.
- Manifest mismatch and sealed-attempt disposition: issue #305 comments `5857641422`, `5857659128`.
- Final repaired-harness lifecycle, Docker Desktop restart, dependency-preserving reset, fresh start and final cleanup: issue #305 comments `5857900488`, `5858036695`.
- #360 local operator mission and #361 service boundary: issue #305 comments `5858162988`, `5858878923`.
- New container-native distribution boundary (candidate only; no implementation authorized): issue #305 comment `5859084494`.
- Local operator and regressions: `scripts/preview/elite-local.ps1`, `scripts/preview/local-audit.ps1`, `scripts/preview/test-local-operator.ps1`, `scripts/preview/test-dependency-identity.ps1`.
