# Wave 16 — Windows Native Runtime + Installer Direction

**Status:** PRODUCT OWNER DIRECTION / PREPARATION ONLY / START AFTER WAVE 15 ACCEPTANCE  
**Decision date:** 2026-09-29  
**Execution authority:** future Main Coordinator after Wave 15 exact acceptance

## 1. Product objective

Wave 16 should turn the accepted post-Wave-15 EliteSCADA into a normal Windows-installed product.

Target experience:

`install EliteSCADA -> Windows service starts -> local Engineering/Runtime is available -> state survives restart/update`

This Wave is about host/productization maturity, not adding a second EliteSCADA implementation.

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

## 3. Reuse, do not restart from zero

Wave 16 must harvest validated work from:
- #205 / PR #207 — preserved Wave 13 Windows packaging/signing checkpoint;
- #361 — installed Windows Service operational contract;
- #360 — local lifecycle evidence where still applicable;
- #366 — database-topology direction when required for a usable installed product;
- current canonical licensing/Authority/package/runtime contracts.

The old Wave 13 product snapshot is historical technical input only. It must not become the post-W15 release authority by direct merge/revival.

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

## 7. Entry gate

Wave 16 starts only after:
1. Wave 15 correction/product scope is accepted;
2. final Wave 15 Preview/audit/recheck gates are closed;
3. an exact post-W15 product authority is chosen;
4. existing Wave 13/Windows artifacts are re-audited against that current product before reuse.

## 8. Non-goals for Wave 16 unless separately authorized

- EliteGO application;
- HA/failover implementation;
- Linux/Debian production distribution;
- canonical OCI/container-first migration;
- embedded/PLC deployment;
- parallel HMI/rendering engine;
- redesign of Engineering lifecycle merely for packaging.

