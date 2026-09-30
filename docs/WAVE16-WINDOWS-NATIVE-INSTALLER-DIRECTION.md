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



## 2.2 Canonical immutable release payload + host executor — strategic north

**Product Owner architecture north — 2026-09-30**

Refine the previous container-native hypothesis into a higher-level product distribution model:

`canonical EliteSCADA product -> architecture-specific immutable release payload -> host executor/adapter -> external durable state`.

The **immutable release payload** is the primary product-distribution abstraction. OCI/container packaging is one valid representation/host profile of that payload, not the identity of EliteSCADA itself.

Initial host/architecture profiles:

- `win-x64`;
- `linux-x64`;
- `linux-arm64`.

Possible future profiles remain evidence/homologation based.

### Immutable payload

For every supported target, Release Factory should produce an exact, self-contained, versioned product payload containing the application binaries/runtime and required static assets for that architecture.

Conceptually:

```text
EliteSCADA canonical source
        |
        v
Release Factory
        |
        +-- win-x64 immutable payload
        +-- linux-x64 immutable payload
        +-- linux-arm64 immutable payload
        +-- OCI representation/profile where useful
```

Rules:

- release payload is immutable after validation/signing;
- exact product version/SHA/manifest/hash/provenance belong to the payload;
- normal runtime must not mutate product/release files;
- config, licenses, certificates, logs, project/application durable state and database files stay outside the replaceable payload;
- upgrade installs a new payload side-by-side or atomically stages it, health-gates it, then changes the active-version pointer;
- rollback returns to the previous valid payload without reconstructing old product bytes;
- destructive purge is never implied by release replacement.

### Host executor

Each native host profile should provide a small host/deployment executor whose responsibility is operational, not domain/product semantics.

Conceptually:

```text
Windows:
SCM -> EliteSCADA.Host.exe -> active immutable win-x64 payload

Linux:
systemd -> elitescada-host -> active immutable linux-x64/linux-arm64 payload
```

Host executor responsibilities may include:

- exact release selection;
- manifest/hash/signature verification;
- host path/config/secret projection;
- host/deployment identity evidence collection;
- child-process/service lifecycle;
- readiness/liveness/startup gating;
- graceful stop/restart;
- upgrade/rollback coordination;
- sanitized diagnostics and exact active-version reporting.

Host executor must **not** become a second EliteSCADA domain authority. It must not own TAG/Alarm/Historian/Engineering/Runtime semantics, project state, Authority policy or commercial entitlement decisions.

Licensing remains behind the canonical Machine Request Code / license-validation boundary. The host executor may collect homologated machine/deployment evidence but does not replace licensing authority.

### .NET runtime packaging

Normal native distributions should not require a separately preinstalled .NET runtime on the customer host when technically suitable.

Preferred baseline:
- architecture-specific **self-contained .NET publish** for the canonical product payload;
- evaluate single-file/native-AOT only where they improve a bounded host component and do not constrain Drivers, reflection/dynamic loading, diagnostics or portability;
- a small host executor may independently qualify for Native AOT, but the EliteSCADA product core must not be forced to Native AOT merely for packaging convenience.

### Linux x64 / ARM64

Linux x64 and ARM64 are first-class future host profiles of the same distribution contract, not separate EliteSCADA products.

They must preserve:
- the same Engineering/Runtime/Authority/licensing/package semantics;
- systemd mapping of the same start/stop/restart/status/readiness intent used by Windows SCM;
- architecture-specific self-contained payloads;
- the same external durable-state boundary;
- the same release/upgrade/rollback semantics;
- explicit Driver/native-library compatibility matrix per architecture.

Do not claim an industrial Driver supported on ARM64 unless every required native dependency/device-access contract is homologated there.

ARM32 is not an initial target and may be considered later only by explicit homologation.

### OCI relationship

#363 is refined by this higher-level premise:

`OCI is a supported package/host representation of an immutable EliteSCADA release payload where technically appropriate; OCI is not the canonical identity of the EliteSCADA product.`

A future OCI profile may still be multi-arch (`linux/amd64`, `linux/arm64`) and signed/provenanced. Native Linux and native Windows profiles may coexist with OCI without creating product forks.

## 2.3 Managed database distribution and licensing guardrail

Database remains a **separate deployment service/profile**, never part of the immutable EliteSCADA application payload.

The standard installed-product experience may provision a local managed PostgreSQL/TimescaleDB service alongside EliteSCADA, while retaining the #366 Local Managed / Remote topology boundary.

Licensing/distribution guardrails:

1. **PostgreSQL**
   - PostgreSQL licensing is permissive and supports redistribution with required copyright/license notices;
   - Release Factory may therefore bundle/provision a pinned supported PostgreSQL distribution for Local Managed profiles, subject to platform packaging/upgrade/support validation.

2. **TimescaleDB Apache 2.0 components/edition**
   - Apache 2.0-licensed TimescaleDB components may be redistributed under their Apache notice/license obligations;
   - this is the preferred low-friction baseline for a bundled EliteSCADA Local Managed database if its feature set satisfies product needs.

3. **TimescaleDB Community / TSL components**
   - TSL-licensed binaries have additional conditions for redistribution with a Value Added Product, including customer-license notice requirements and restrictions around customer schema-definition interfaces;
   - do not silently bundle TSL components merely because they are free for self-hosted use;
   - any EliteSCADA distribution that includes TSL binaries requires explicit legal/license compliance review and a documented product-control model satisfying the applicable TSL terms, or a separate commercial agreement where appropriate.

4. **Release manifest / third-party notices**
   - every bundled database/runtime dependency must be represented in SBOM/third-party notices/license inventory;
   - package build must fail closed when a requested database artifact/license profile is not approved for that target/release.

5. **Platform support**
   - permission to redistribute does not imply technical support on every OS/CPU;
   - PostgreSQL/TimescaleDB versions and binaries must be pinned and homologated per `win-x64`, `linux-x64`, `linux-arm64` or other supported profile.

This section records architecture/product direction, not legal advice. Final commercial releases must revalidate the exact third-party versions and license texts actually shipped.



### Current TimescaleDB 2.29.2 / PostgreSQL 18 feature-license audit — 2026-09-30

Current EliteSCADA CI/development database image:
`timescale/timescaledb:2.29.2-pg18`.

Timescale publishes a separate Apache-only image:
`timescale/timescaledb:2.29.2-pg18-oss`.

The standard non-`-oss` image is built without `APACHE_ONLY` and is therefore capable of loading the TSL module; the `-oss` image is built with `-DAPACHE_ONLY=1`.

Exact EliteSCADA source audit on `wave15/corrections-integration@1d9e3f9123bea8e27c362ee680a4ba70f864becd`:

**Raw/current production Historian path**
- `TimescaleDbHistorian` initializes only `TimescaleHistorianInfrastructure.EnsureRawAsync`;
- raw infrastructure uses `CREATE EXTENSION timescaledb`, `create_hypertable`, ordinary table/index DDL, INSERT and SELECT;
- TimescaleDB 2.29.2 source places `create_hypertable`/hypertable core and `time_bucket` implementation outside `tsl/` under Apache 2.0;
- therefore the currently mounted raw Historian path appears compatible with the Apache-only TimescaleDB build, subject to an explicit product test before changing the pinned image.

**Implemented retention/downsampling capability**
`TimescaleDbHistorianRetentionDownsamplingStore` uses:
- continuous aggregates via `WITH (timescaledb.continuous)`;
- `refresh_continuous_aggregate`;
- `add_continuous_aggregate_policy` / `remove_continuous_aggregate_policy`;
- `add_retention_policy` / `remove_retention_policy`.

TimescaleDB 2.29.2 source implements continuous aggregate creation/refresh and background retention/policy behavior in files under `tsl/`, explicitly licensed under the Timescale License. Timescale's own Apache-license regression test confirms continuous aggregates and `refresh_continuous_aggregate` are rejected when the extension runs under the Apache license.

**Current production wiring**
Repository search shows `IHistorianRetentionDownsamplingStore` / `TimescaleDbHistorianRetentionDownsamplingStore` currently referenced only by their implementation/abstraction and Timescale-focused tests; they are not registered in the normal API production composition. Thus:
- TSL-dependent retention/downsampling is implemented/tested capability today;
- it is not presently a mounted normal-runtime service;
- the normal `TimescaleDbHistorian` raw capture/query path does not itself require those continuous-aggregate/policy features.

Wave 16 packaging consequence:
1. do not assume the current non-`-oss` CI image is legally equivalent to an Apache-only redistributable bundle;
2. before Local Managed DB packaging, run the complete installed-product test matrix against `2.29.2-pg18-oss`;
3. if raw Historian + required installed-product behavior pass without TSL, Apache-only is the preferred bundled baseline;
4. if retention/downsampling becomes a required mounted product feature, either:
   - redesign that feature using PostgreSQL/Apache-only mechanisms,
   - accept TSL redistribution with explicit compliance/legal review and customer restrictions,
   - or use a separately licensed/commercial Timescale distribution;
5. CI may continue using the current Community/TSL-capable image for development evidence, but CI convenience must not silently define the commercial distribution license boundary.



### Host prerequisites and offline bootstrap model — 2026-09-30

Wave 16 must distinguish three dependency classes. This classification is binding for the release factory and host profiles.

#### A. Bundled inside the immutable EliteSCADA payload

These are product/runtime libraries and assets that ship with the architecture-specific self-contained product publish and do **not** receive separate machine-wide installers.

Examples:
- .NET runtime for the selected RID when using self-contained publish;
- EliteSCADA managed assemblies;
- Npgsql and other managed NuGet dependencies;
- Web Runtime / Engineering static assets;
- pinned Pyodide assets;
- managed Drivers and product libraries;
- approved native libraries that are intentionally app-local to the immutable payload.

Npgsql is therefore a bundled application dependency, not a separately installed Windows/Linux component.

Customer hosts must not require Node.js, npm, TypeScript, Visual Studio, .NET SDK or CPython merely to run an installed EliteSCADA release.

#### B. Host-profile prerequisites

A Host Profile owns operating-system prerequisites that cannot or should not be treated as normal EliteSCADA product files.

For Debian/Ubuntu-family Linux profiles:
- preflight must detect the exact supported distribution/architecture;
- required native OS packages must be expressed by the profile;
- the installer may use the distribution package manager (for example `apt`) when online/package sources are available;
- offline packages/repository material must be part of an explicit offline host profile rather than silently depending on internet access;
- do not use one generic unqualified Linux dependency list for all distributions.

For Windows:
- prerequisite handling must be deterministic and suitable for offline industrial installation;
- approved prerequisite installers/binaries should be acquired and validated by Release Factory, then embedded in or shipped beside the signed offline setup;
- setup must use `DETECT -> COMPARE -> INSTALL/REPAIR IF NEEDED -> VERIFY`;
- do not download required prerequisites from the internet during normal customer installation;
- prerequisite versions/hashes/signatures/licenses must be part of release evidence/SBOM.

**Microsoft Visual C++ Redistributable**
- if required by a supported Windows component/profile, use the official redistributable package rather than hand-copying CRT DLLs;
- detect an already-installed compatible/newer runtime and reuse it;
- otherwise run the approved redistributable silently with bounded restart handling;
- never treat the VC++ runtime as EliteSCADA durable state;
- Release Factory must retain the exact redistributable artifact, signature/hash and applicable redistribution-rights evidence used for that release.

**OpenSSL**
- OpenSSL required by a Windows Local Managed database profile belongs to that database/host dependency graph, not automatically to the EliteSCADA product core;
- prefer an app-local/private runtime layout for the managed database stack if the exact PostgreSQL/TimescaleDB Windows build is validated to support it;
- avoid modifying global PATH or creating an unbounded machine-wide OpenSSL authority merely to satisfy EliteSCADA;
- if the homologated upstream distribution requires a machine-wide installation, that becomes an explicit Host/Database Profile prerequisite and must be detected, version-gated, installed and verified by the setup;
- exact OpenSSL version and license evidence must be pinned in the release manifest/SBOM.

Any other native prerequisite introduced later must be classified explicitly as:
`BUNDLED_APP_LOCAL`, `HOST_PREREQUISITE`, or `DATABASE_PROFILE_PREREQUISITE`.
Unclassified native dependencies fail release review.

#### C. Components with their own service/lifecycle authority

PostgreSQL/TimescaleDB Local Managed deployments are not ordinary payload files. They remain a separate Database Profile with their own service, version, persistent data, upgrade and rollback boundaries.

Conceptually on Windows:

```text
EliteSCADA Offline Setup
|
+-- bootstrapper
+-- prerequisites/
|   +-- approved VC++ redistributable when required
|   +-- approved OpenSSL/runtime material when required by Local Managed DB
+-- product/
|   +-- immutable EliteSCADA win-x64 payload
+-- database/
    +-- pinned PostgreSQL distribution
    +-- approved TimescaleDB distribution/profile
```

Selecting `Remote` database mode must not force installation of Local Managed PostgreSQL/TimescaleDB/OpenSSL components that are not otherwise required.

#### Offline-first industrial installation

The primary Windows artifact should be capable of full installation without internet access.

Release Factory, not the customer machine, owns acquisition of approved third-party prerequisites:

`official upstream artifact -> verify provenance/signature/hash -> license/SBOM inventory -> include in release bundle -> install from local media`.

A future optional Web/bootstrap installer may exist, but it must not become the only supported installation path.

#### Reusable profile + bounded scripts

Do not encode the whole distribution architecture in one-off PowerShell/Bash scripts.

Each supported host should converge on a repository-owned declarative profile plus bounded adapters/scripts, conceptually:

```text
distribution/
  profiles/
    win-x64/
      profile.*
      preflight.ps1
      install.ps1
      configure.ps1
      verify.ps1
      upgrade.ps1
      rollback.ps1
      uninstall.ps1

    linux-x64-<distro>/
      profile.*
      preflight.sh
      install.sh
      configure.sh
      verify.sh
      upgrade.sh
      rollback.sh
      uninstall.sh

    linux-arm64-<distro>/
      ...
```

The profile is the source of truth for:
- architecture/RID;
- supported OS/distro/version;
- product payload type;
- required host packages/prerequisites;
- service manager;
- Local Managed / Remote database capabilities;
- prerequisite version ranges;
- expected paths/permissions;
- readiness/verification commands;
- third-party license/SBOM classification.

Scripts execute the profile; they must not become a second hidden product-definition authority.

#### Standard install sequence

All native profiles should map to the same high-level sequence:

`DETECT -> PREFLIGHT -> SATISFY HOST PREREQUISITES -> INSTALL HOST EXECUTOR -> INSTALL IMMUTABLE PAYLOAD -> INSTALL OR CONNECT DATABASE -> CONFIGURE -> INITIALIZE/MIGRATE -> REGISTER SERVICE -> START -> READINESS -> VERIFY EXACT PRODUCT/DB IDENTITY`.

This sequence must remain reusable across EliteSCADA versions for an already-supported host profile.

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

