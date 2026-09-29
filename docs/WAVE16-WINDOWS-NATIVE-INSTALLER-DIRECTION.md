# Wave 16 — Windows Native Runtime + Installer Direction

**Status:** PRODUCT OWNER DIRECTION / PREPARATION ONLY / START AFTER WAVE 15 ACCEPTANCE  
**Decision date:** 2026-09-29  
**Execution authority:** future Main Coordinator after Wave 15 exact acceptance

## 1. Product objective

Wave 16 should turn the accepted post-Wave-15 EliteSCADA into a normal Windows-installed product.

Target experience:

`install EliteSCADA -> Windows service starts -> local Engineering/Runtime is available -> state survives restart/update`

This Wave is about host/productization maturity, not adding a second EliteSCADA implementation.

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

## 6. Container/OCI relationship

#363 remains a long-term architecture program.

Wave 16 explicitly chooses **native Windows installation as the next execution priority**. It does not delete future OCI/Linux/Edge work and does not require the Windows product to wrap the application in a container.

Host/deployment boundaries should remain clean enough that later OCI packaging can reuse the same product core.

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

