# Wave 16 — Windows Native Runtime + Installer Direction

**Status:** PRODUCT OWNER DIRECTION / PREPARATION ONLY / START AFTER WAVE 15 ACCEPTANCE  
**Decision date:** 2026-09-29  
**Execution authority:** future Main Coordinator after Wave 15 exact acceptance

## 1. Product objective

Wave 16 should turn the accepted post-Wave-15 EliteSCADA into a normal Windows-installed product.

Target experience:

`install EliteSCADA -> Windows service starts -> local Engineering/Runtime is available -> state survives restart/update`

This Wave is about host/productization maturity, not adding a second EliteSCADA implementation.

The principal durable output is **not one installer for one frozen EliteSCADA version**. It is a **reusable release/distribution capability** that can take a later accepted EliteSCADA version and regenerate the Windows distribution through a controlled, repeatable pipeline.

Binding release premise:

`PRODUCT VERSION -> CANONICAL BUILD -> HOST PACKAGE -> INSTALLER/DISTRIBUTION -> VALIDATION -> SIGN/TRUST -> RELEASE ARTIFACTS`

After Wave 16 is accepted, a normal later Windows release should primarily be:

`select exact accepted EliteSCADA version/SHA -> invoke release pipeline -> validate -> sign/approve -> publish`

and should **not** require redesigning the installer or manually rebuilding its file/service/configuration logic for every product version.

**Binding architecture premise: `WINDOWS-FIRST / CROSS-PLATFORM-CORE`.**

Windows x64 is the first host that Wave 16 will package and homologate. It is **not** permission to turn the EliteSCADA product core into a Windows-specific implementation.

The same canonical EliteSCADA product is intended to remain suitable for later supported deployment on:
- Linux x64;
- Linux arm64;
- industrial PCs / Edge computers;
- homologated OCI/container hosts;
- constrained controller/PLC-class platforms that expose a supported runtime/container environment and meet a validated capacity profile.

Host support remains evidence-based. This premise does not claim that every Linux/Edge/device is automatically supported.

## 2. Primary scope

- supported native `win-x64` product publish;
- native Windows Service / Service Control Manager lifecycle;
- deterministic startup/readiness and bounded graceful shutdown;
- packaged backend, Web Runtime, Web Engineering, Pyodide/static assets and supported Driver/runtime dependencies;
- installer with install / repair / upgrade / uninstall;
- explicit preserve-data default and separate destructive purge/reset path;
- protected program/config/data/license/certificate/log boundaries;
- exact product identity from the canonical EliteSCADA product-version authority;
- first-run/bootstrap behavior from the accepted neutral/product lifecycle;
- packaged health/status/diagnostics suitable for administrators;
- host reboot recovery;
- upgrade preserving supported application, Authority, licensing and durable state;
- no Visual Studio or Docker Desktop required on a customer installation;
- no internal development documentation/material shipped in customer artifacts.

## 2.1 Reusable packaging / release-factory capability

Wave 16 must commit the **capability to repackage future EliteSCADA versions**, not only the generated binaries of the first Windows release.

Required design:

1. **Version/SHA is an input**
   - packaging consumes one exact, already-accepted EliteSCADA source/product authority;
   - visible product identity comes from the canonical product-version authority;
   - installer/package scripts do not contain a second manually maintained product version.

2. **Repository-owned release definition**
   - package composition, required assets, host adapters, service installation, directory policy, upgrade rules, diagnostics and validation are versioned in the repository;
   - a future coordinator must not need tribal knowledge or a previous local machine to recreate a release.

3. **Deterministic staged pipeline**
   - canonical product publish/build;
   - host/architecture composition;
   - installer or image/package construction;
   - package-content validation;
   - fresh-install validation;
   - upgrade validation from supported predecessor versions;
   - uninstall / retained-data validation;
   - SBOM/dependency evidence;
   - signing/trust as a separable controlled stage;
   - final manifest, hashes and provenance.

4. **No one-off release mutations**
   - manual file copying, local path patching, hand-edited installer manifests or undocumented post-build fixes are not accepted as the normal release mechanism;
   - any workaround required to make a release must either become repository-owned automation or be an explicit external signing/publishing step with reproducible inputs/outputs.

5. **Clean product/distribution boundary**
   - the canonical EliteSCADA product is the input;
   - Windows installer metadata and Windows service integration surround it;
   - packaging must not rewrite application semantics to make the artifact installable.

6. **Upgrade is part of the reusable capability**
   - later EliteSCADA versions must be able to replace program binaries/assets while preserving supported external durable state;
   - compatibility/migration decisions belong to explicit product/schema/database contracts, not ad-hoc installer copying.

7. **Release invocation must be simple**
   - once the capability is accepted, producing a new package for an already-supported host/architecture should require only the exact product authority, release parameters and authorized signing/publishing inputs;
   - it should not require a new implementation project unless the product introduces a genuinely new packaging requirement.

8. **Evidence is regenerated per release**
   - packaging capability may be stable, but every new EliteSCADA release still gets exact-version install/upgrade/smoke/security/package evidence;
   - a previous package's green result is never silently inherited by later product bytes.

### Future host/architecture enablement model

The same principle applies when Linux, Edge or another architecture is enabled later.

For each new supported host/architecture, the **first enablement** may require development of:
- host lifecycle adapter;
- architecture-specific product publish;
- native/runtime dependency resolution;
- directory/permissions/secrets integration;
- Driver/native-library compatibility;
- package/image format;
- install/upgrade/uninstall semantics;
- architecture-specific qualification/capacity matrix;
- signing/trust mechanism where applicable.

That work must end by creating another reusable release target/profile in the common distribution system.

After a target is established, subsequent EliteSCADA versions should be repackaged by feeding the new accepted product version into the same target/profile rather than creating a new one-off distribution project.

Desired long-term model:

`one EliteSCADA product authority + reusable distribution pipeline + host/architecture profiles`

Examples:

`EliteSCADA vX -> windows-x64 profile -> installer`

`EliteSCADA vX -> linux-x64 profile -> package/image`

`EliteSCADA vX -> linux-arm64/edge profile -> package/image`

and later:

`EliteSCADA vY -> same supported profiles -> regenerated validated artifacts`

A packaging implementation that produces a working artifact but does not leave behind a repeatable path for the next EliteSCADA version is **incomplete**.

## 2.1 Cross-platform architecture constraints

Wave 16 Windows implementation decisions must preserve future Linux/Edge portability.

Freeze these rules:

1. **One product core**
   - no Windows-only fork of Engineering, Runtime, Authority, licensing, Historian, Driver contracts or `.escadapkg`;
   - Windows-specific behavior belongs in bounded host/deployment adapters.

2. **Host lifecycle adapter**
   - Windows SCM/service installation is a host implementation of generic product lifecycle semantics such as start/stop/restart/status/readiness;
   - core services must not require SCM APIs to function;
   - later systemd/OCI/Edge hosts must be able to provide equivalent lifecycle intent without changing product-domain logic.

3. **Filesystem / configuration / secrets**
   - do not hard-code Windows drive letters or Program Files paths in product-domain contracts;
   - resolve program/config/data/log/license/certificate locations through host-owned configuration/path authorities;
   - durable state remains outside replaceable product binaries/images;
   - secrets remain outside Engineering and `.escadapkg`.

4. **Machine identity / licensing**
   - licensing remains machine/installation authority, not Windows Registry identity;
   - do not make a Windows-only identifier the universal license contract;
   - host-specific machine-evidence collection must sit behind the canonical Machine Request Code / license-validation boundary so Linux/Edge can supply homologated evidence later;
   - ephemeral container identity must never become the durable license identity.

5. **Runtime/network boundaries**
   - backend/API/Runtime contracts remain OS-neutral;
   - canonical renderer/Web assets remain shared;
   - Drivers access OS/device capabilities through bounded adapters where native access is necessary;
   - do not let UI/runtime clients depend on Windows IPC or local-only assumptions when a transport-neutral authority already exists.

6. **Native dependencies**
   - every Windows-native dependency introduced in Wave 16 must have its portability impact recorded;
   - dependencies required only by the Windows host adapter must not become mandatory product-core dependencies;
   - Driver/native-library support remains a homologated host/architecture matrix.

7. **Database topology**
   - installed Windows must support the same deployment-level separation already intended by #366;
   - product core must not assume the durable database is permanently local to the Windows host;
   - local managed and supported remote database profiles must remain architecturally possible.

8. **Capacity, not Lite forks**
   - Edge/constrained hardware should be handled through validated deployment/capacity profiles where practical;
   - do not create an unrelated EliteSCADA Lite/Edge fork merely because resources differ.

9. **Build/package separation**
   - the Wave 16 installer is a Windows distribution artifact around the canonical product;
   - later Linux/OCI packaging should reuse the same product contracts and as much of the same build output/source graph as technically valid;
   - packaging differences must not change application semantics.

A Wave 16 implementation that makes Linux/Edge support materially harder without a documented technical necessity is an architecture regression and must be reviewed before acceptance.

## 3. Evidence hierarchy — do not restart from zero, but do not inherit stale assumptions

Wave 13 is **cancelled** and is not a resumable implementation line.

Wave 16 must prioritize evidence in this order:

1. **accepted post-Wave-15 product authority** — current code/contracts are the implementation truth;
2. **real local CODEX/workbench operational evidence** from #360/#362 and `docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md`;
3. **first real browser Preview evidence** from #208/#210, especially environment/startup/login/reproducibility lessons exposed by real use;
4. #361 installed Windows Service operational contract;
5. #366 database-topology direction;
6. current canonical licensing/Authority/package/runtime contracts;
7. **Wave 13 #205/#207 only as historical technique/reference**, never as a design authority or branch to revive.

The local CODEX/workbench evidence is particularly important because it exercised real Windows-hosted operation and exposed concrete requirements/failures including:
- exact SDK/toolchain compatibility;
- stable machine identity requirements;
- dependency provenance/cache invalidation;
- Windows PowerShell 5.1 vs PowerShell 7 behavior;
- LF/CRLF identity instability;
- Docker inspection/parsing differences;
- TLS interception/trusted-root handling without disabling certificate validation;
- DB/API/Web readiness rather than container-start assumptions;
- pause/resume/restart/reset boundaries;
- Docker Desktop restart recovery;
- separation of persistent preparation from resettable product state;
- fail-closed orphan/provenance handling;
- diagnostics/redaction;
- explicit gaps still not tested for installed SCM/systemd behavior.

Wave 16 must convert these observations into installed-product requirements and tests rather than copy the Preview harness itself.

The useful Wave 13-era outcome to preserve is historical learning and the path that led to the first Preview. The old unsigned Windows candidate, signing branch and CI matrix do not establish the future installed architecture.

## 4. Trust/signing boundary

Authenticode/signing remains required for a production Windows release, but packaging/runtime installation should be validated before coupling progress to external signing credentials.

Required final release evidence should include:
- authorized publisher identity;
- SHA-256 Authenticode;
- trusted timestamp;
- deterministic signed-return verification;
- installer and shipped executable signature verification;
- dependency-license/SBOM review.

DNP3 commercial licensing remains an independent distribution gate if that dependency is shipped/enabled.

## 5. EliteGO relationship

EliteGO is not a Wave 16 requirement.

Wave 16 must, however, avoid host-specific coupling that makes future EliteGO expensive. Preserve inside EliteSCADA:
- canonical Server Runtime/public Runtime contracts;
- backend Authority/effective capabilities;
- topology-neutral application packages;
- canonical Runtime/rendering implementation;
- logical Runtime Session semantics;
- remote-capable transport boundaries where already established;
- truthful freshness/reconnect states.

A future EliteGO should become a client of these server contracts rather than causing a server redesign.

## 6. Linux / Edge / Container relationship

#363 remains a long-term architecture program and `docs/LINUX-DEBIAN-DISTRIBUTION.md` remains preserved product direction.

Wave 16 explicitly chooses **native Windows installation as the next execution priority and first homologated installed host**, not Windows as the permanent architecture boundary.

Wave 16 does not require the Windows product to run inside a container. Conversely, native Windows packaging must not close the door to:
- native or packaged Linux operation;
- Linux systemd lifecycle;
- `linux/amd64` and `linux/arm64` OCI images;
- IPC/Edge installations;
- controller-class OCI hosts where hardware/runtime capability is explicitly homologated.

The intended long-term shape remains:

`same EliteSCADA core + same Engineering/package/Authority/Runtime contracts + host/deployment adapters + validated capacity/driver matrix`.

Windows SCM, filesystem conventions, code signing and installer mechanics are host concerns, not canonical application semantics.

## 6.1 Wave 16 reusable-release acceptance

Wave 16 is not complete merely because one installer can be installed once.

Acceptance must prove, on repository-owned automation, at least:
1. one exact post-Wave-15 EliteSCADA version can produce a Windows release from a documented clean release environment;
2. package identity is derived from the canonical product version/build authority;
3. fresh install reaches truthful service/application readiness;
4. restart/reboot preserves supported durable state;
5. upgrade from at least one controlled predecessor package preserves supported state and replaces product bytes deterministically;
6. uninstall preserves data by default and explicit purge is separately destructive;
7. rebuilding/repackaging the same exact product input under the same release inputs produces equivalent package composition/provenance within documented signing/timestamp nondeterminism;
8. the release definition does not depend on an operator's machine-local undocumented files or paths;
9. signing can be applied to the exact validated release inputs without rebuilding different product bytes;
10. a second product-version input can pass through the same packaging definition without editing version strings or installer file lists manually, unless a deliberate product/package contract change requires it.

The final item is the essential proof that Wave 16 created a **release capability**, not merely a one-version artifact.

## 7. Entry gate

Wave 16 starts only after:
1. Wave 15 correction/product scope is accepted;
2. final Wave 15 Preview/audit/recheck gates are closed;
3. an exact post-W15 product authority is chosen;
4. #360/#362 local operational evidence and #208/#210 Preview evidence are re-read against the exact post-W15 product;
5. any isolated Wave 13 technique considered for reuse is independently re-derived/revalidated rather than inherited from its cancelled branch.

## 8. Non-goals for Wave 16 unless separately authorized

- EliteGO application;
- HA/failover implementation;
- Linux/Debian production distribution;
- canonical OCI/container-first migration;
- embedded/PLC deployment;
- parallel HMI/rendering engine;
- redesign of Engineering lifecycle merely for packaging.

