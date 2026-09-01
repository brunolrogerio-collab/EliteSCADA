# EliteSCADA — Future Linux x64 / Debian Distribution

**Status:** SPECIFIED / NOT STARTED  
**Trigger:** Development Lead request only  
**Initial target:** Debian 12 amd64, followed by Debian 13 homologation

This document records the future official Linux distribution direction. It does not start implementation, does not create a branch or PR, and does not alter the current Wave 12 handoff.

## Product premise

The Linux distribution is expected to be a packaging/integration effort rather than a port of the EliteSCADA product architecture.

The current product baseline already favors this direction:

- main backend/runtime targets .NET 10;
- backend and frontend are built/tested on Ubuntu in CI;
- the L3 Seven-Driver Lab runs on Ubuntu with the accepted seven-Driver integration surface;
- Runtime hardware-bound licensing already has a Linux machine identity path using `/etc/machine-id`;
- the Machine Request Code contract remains platform-independent;
- the offline License Generator may remain Windows-only because it is an authority-side issuance tool, not a target-runtime dependency.

These facts are useful prerequisites, but they do not by themselves constitute an accepted Linux distribution. The `.deb` packaging/integration stage still requires its own exact validation.

## Distribution contract

When the Development Lead explicitly authorizes the Linux packaging front, the first official target is:

- Linux x64;
- Debian package format `.deb`;
- architecture `amd64`;
- Debian 12 as first supported/homologated baseline;
- Debian 13 as the next homologation target.

Initial packaging should remain multi-file. Do not use single-file publish merely to make the installation appear simpler. The `.deb` is already the single distribution artifact, while an internal multi-file layout better accommodates native libraries, Pyodide/browser assets and future installable Driver modules.

## Required technical scope

The future Linux distribution front must include at least:

1. `linux-x64` self-contained publish for the product runtime/host;
2. incorporation of the React build and required Pyodide assets into the product served by Kestrel;
3. Linux filesystem layout with explicit separation of immutable product, configuration and mutable state, initially:
   - `/usr/lib/elitescada` — installed product binaries/static assets;
   - `/etc/elitescada` — host/operator-managed configuration;
   - `/var/lib/elitescada` — mutable application/runtime state, including durable licensing state where appropriate;
4. Linux-appropriate persistent license path/configuration without embedding or weakening the existing licensing trust contract;
5. dedicated system user/group and a hardened `elitescada.service` systemd unit;
6. externally configurable PostgreSQL and TimescaleDB endpoints/credentials through protected configuration/secret mechanisms rather than package-embedded credentials;
7. an `amd64` Debian `.deb` package with deterministic install, upgrade and removal behavior;
8. clean-host CI that installs/upgrades the package on a fresh Debian environment;
9. post-install validation of Runtime, Web UI, licensing and supported Drivers;
10. Linux Machine Request Code generation and acceptance of a correctly machine-bound license using the same signed-license contract as Windows;
11. reboot/restart/upgrade validation proving configuration, license and durable product data are preserved;
12. SBOM generation and dependency-license auditing as release evidence.

## Acceptance gate

The Linux `.deb` is accepted only when an exact package build can be installed on a clean supported Debian machine and demonstrates all of the following:

- package installation succeeds without manual copying of application files;
- `elitescada.service` starts automatically through systemd and can be cleanly stopped/restarted;
- the EliteSCADA Web interface is reachable through the installed service;
- PostgreSQL/TimescaleDB connectivity works from external configuration;
- a Linux Machine Request Code can be generated;
- a license issued through the normal authority-side License Generator is accepted only on the bound machine;
- supported Drivers operate through the normal product runtime boundaries;
- a reboot preserves configuration, installed license and durable state;
- a package upgrade preserves configuration, installed license and durable state unless an explicit compatible migration says otherwise;
- package integrity, SBOM and dependency-license evidence are retained for the accepted build.

## DNP3 commercial-distribution gate

The current DNP3 dependency line uses Step Function I/O `dnp3` 1.6.0. Its public license is non-commercial / non-production and requires a commercial license for for-profit/product use.

Therefore:

- no commercial EliteSCADA distribution may include or enable that DNP3 dependency under only the public non-commercial license;
- before a commercial Linux `.deb` (or any other commercial EliteSCADA package) ships with this Driver, the Development Lead must either obtain/record an appropriate commercial license from Step Function I/O or replace the dependency with an approved alternative and revalidate the Driver;
- DNP3 licensing remediation is expected to be evaluated before the Linux `.deb` front is authorized, unless the approved package deliberately excludes DNP3;
- SBOM/license audit must make the final disposition visible rather than silently treating the dependency as ordinary permissive open source.

This is a commercial-distribution gate, not a claim that the currently accepted non-production/test evidence is invalid.

## Explicit non-goals at specification time

- no Linux packaging implementation is authorized by this document alone;
- no current Wave 12 branch or code may be created merely because this future direction is documented;
- no Windows License Generator port is required for the first Linux product distribution;
- no single-file publish requirement exists for the first `.deb`;
- no Debian 13 support claim exists until it receives its own homologation evidence;
- no DNP3 commercial-use right is implied by source availability or current CI success.

## Start rule

Implementation begins only when the Development Lead explicitly requests the installable `.deb` version. At that time the Coordinator must re-read live `main`, licensing behavior, current Driver dependencies, the exact DNP3 licensing disposition, supported Debian versions and current CI before creating a dedicated packaging branch.
