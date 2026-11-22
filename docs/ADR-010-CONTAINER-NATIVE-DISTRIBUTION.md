# ADR-010 — Container-native EliteSCADA distribution and host profiles

Status: **PROPOSED / PRODUCT OWNER DIRECTION / TECHNICAL VALIDATION REQUIRED**

Date: 2026-09-27

Related:
- #363 — ARCH-CONTAINER-FIRST
- #360 — local container operator / Visual Studio AI bootstrap
- #361 — installed lifecycle / Windows Service / Linux systemd
- #205 / #207 — Windows packaging/signing checkpoint
- #306 — Productization
- #300 — complete-product Preview
- #307 — remote/WAN resilience

## 1. Context

EliteSCADA is already architecturally close to a container-friendly product:

- backend/runtime is .NET;
- Engineering and Runtime are Web surfaces;
- canonical Engineering/runtime authority is backend-owned;
- the product does not depend on a local desktop GUI;
- Linux is already an intended distribution target;
- local Preview work has proven a containerized DB/API/Web lifecycle;
- future installed operation requires common start/stop/restart/status/diagnose semantics.

The Product Owner raised two connected strategic directions:

1. industrial Edge/embedded devices that can run OCI/Docker containers may be valid EliteSCADA hosts even when their computational capacity is lower than a PC/server;
2. EliteSCADA may eventually benefit from **one canonical container artifact** rather than independent product distributions per operating system.

The goal is not to make a separate "Lite" product.

The preferred long-term model is:

`same EliteSCADA product + same .escadapkg + same product contracts + different deployment/capacity profiles`.

## 2. Decision candidate

EliteSCADA should evaluate a **container-native / OCI-first distribution architecture**.

The candidate canonical artifact is a Linux OCI image containing the common EliteSCADA application:

- backend/API;
- Web Engineering;
- Web Runtime;
- Runtime/Driver host;
- scripting/runtime support;
- static Web and Pyodide assets;
- licensing/Authority client-side host integration.

Mutable state is not stored in image layers.

Host-specific packaging becomes an adapter around the common product where technically supportable.

Candidate deployment targets include:

- Linux server/IPC;
- Windows server/IPC through a validated unattended container-host adapter;
- industrial Edge computers;
- PLC/controller platforms that expose a supported OCI runtime;
- future appliance/server topologies.

This ADR does **not** declare every OCI host supported and does not yet retire native Windows packaging.

## 3. Container-first does not mean "runs everywhere"

OCI solves packaging and process isolation. It does not erase host constraints.

Official support requires explicit validation of:

- CPU architecture;
- kernel/container runtime;
- CPU/RAM;
- persistent storage and endurance;
- industrial networking;
- multicast/broadcast/raw socket needs;
- serial/USB/device passthrough;
- native Driver dependencies;
- container restart/reboot behavior;
- database topology;
- licensing identity source.

The product should use the phrase **supported/homologated OCI host**, not imply universal compatibility.

## 4. Canonical image

The preferred artifact should be:

- Linux-based;
- multi-architecture;
- initially `linux/amd64` and `linux/arm64`;
- deterministic;
- immutable by digest;
- signed/provenanced;
- accompanied by SBOM and dependency-license evidence;
- distributable offline for isolated industrial networks;
- free of private signing keys/secrets;
- non-root where practical;
- minimally privileged;
- compatible with a read-only root filesystem where practical.

Normal EliteSCADA operation must not require a privileged container.

## 5. Persistent-state contract

Container replacement, restart and upgrade must preserve supported persistent state.

Persistent host volumes/configuration must own, as applicable:

- product/application durable state;
- configuration;
- installed license state;
- Authority-owned durable state according to its existing boundaries;
- certificates;
- logs/diagnostics;
- local runtime state explicitly defined as durable.

The application image is immutable product code, not project persistence.

Normal lifecycle:

`new image -> graceful stop -> same persistent state -> new image start -> readiness check`.

A destructive purge/reset remains explicit and separate.

## 6. Database profiles

The EliteSCADA application image must not embed PostgreSQL/TimescaleDB into the same application container.

Supported topology candidates:

### 6.1 External database profile

`EliteSCADA OCI -> external PostgreSQL/TimescaleDB`.

This is the preferred first profile for constrained Edge/embedded systems.

Advantages:
- lower CPU/RAM load on the controller;
- lower local write pressure;
- reduced flash/storage endurance risk;
- database can be sized/maintained independently.

### 6.2 Local composed database profile

`EliteSCADA OCI + PostgreSQL/TimescaleDB OCI`.

This may be supported on IPC/server/Edge hardware with sufficient capacity.

The EliteSCADA application image remains the same.

A different embedded persistence engine is not introduced solely to make Edge deployment easier without a separate architecture decision.

## 7. Full product with deployment-capacity envelope

Edge deployment should not imply a separate product fork.

The same product may run on constrained hardware with an evidence-based **Deployment Capacity Profile**.

Commercial license entitlement and physical platform capacity are separate concepts.

Conceptually:

`effective workload allowed = license entitlement AND deployment capacity envelope`.

Capacity must not be reduced to TAG count alone.

Evidence-based capacity dimensions should include at least:

- TAG count;
- scan/update frequencies;
- Data Source/Driver count and type;
- Historian write/sample rate;
- retention/I/O pressure;
- alarm/event rate;
- Server Script execution budget;
- realtime/Web client count;
- visual/runtime workload;
- CPU/RAM;
- storage capacity/endurance;
- database topology.

A controller may have a license authorizing more capacity than its hardware profile can safely execute.

That is valid and must not be confused with licensing failure.

## 8. Activation-time capacity preflight

EliteSCADA should reuse its fail-closed activation philosophy.

Before replacing Active Runtime, a candidate project should be checked against:

- product feature support;
- current Deployment Capacity Profile;
- current license entitlement;
- Driver/platform compatibility.

Possible outcomes include:

- `VALID`;
- `WARNING_NEAR_CAPACITY`;
- `CAPACITY_EXCEEDED`;
- `UNSUPPORTED_DRIVER_ON_PLATFORM`;
- `INSUFFICIENT_STORAGE_BUDGET`.

If capacity validation fails, the previous valid Active Runtime remains unchanged.

Capacity thresholds must be derived from benchmark/homologation evidence. Do not invent arbitrary limits merely to create product tiers.

## 9. Container-aware licensing identity

The current product binds licenses to a machine fingerprint.

That contract remains valid in principle, but an ephemeral container identity must not become the machine identity.

Do not bind a license to:

- container ID;
- random container hostname;
- virtual container MAC;
- image digest;
- transient namespace identity.

Recreating/upgrading the container must not require a new license.

The existing machine identity abstraction should evolve toward a versioned **deployment/host identity provider**.

Provider candidates include:

1. native host machine identity;
2. stable read-only host identity injected by the deployment adapter;
3. industrial-device identity exposed through a supported vendor/host interface;
4. protected deployment key persisted outside the container;
5. TPM/secure-element/hardware-backed identity where available.

One provider need not fit every host.

The licensing system must preserve:

- asymmetric offline signatures;
- versioned machine/deployment requests;
- fail-closed verification;
- private signing authority outside the product;
- explicit controlled re-host/reissue.

A future license schema may carry a binding/deployment identity type.

Commercial entitlements and hardware capacity profiles remain separate.

## 10. Common operational lifecycle

The container-native product should expose a common operational intent compatible with #361:

- start;
- stop;
- restart;
- status;
- diagnose;
- graceful shutdown;
- readiness;
- liveness.

Pause/resume is optional for installed production and should exist only if its semantics are proven useful and safe.

Host adapters map the common lifecycle to:

- OCI/Docker/Podman/containerd;
- systemd;
- Windows service/host controller;
- industrial controller container manager;
- future appliance tooling.

The application must handle graceful termination deterministically, including Runtime/Driver shutdown and durable-state flush.

## 11. Health model

Container process state is not product readiness.

The container distribution needs explicit:

- startup state;
- liveness;
- readiness;
- database health;
- API health;
- Web readiness;
- Runtime/Engineering readiness;
- degraded/recovering state;
- version/build identity;
- actionable startup failure reason.

This model should feed both container healthchecks and admin-facing status/diagnostics.

## 12. Driver/platform capability model

A Driver may be supported on one OCI host and not another.

Driver/platform support should eventually describe required capabilities such as:

- normal TCP/UDP;
- multicast/broadcast;
- raw sockets;
- serial/USB device access;
- native library/architecture;
- certificate/key mounts;
- host networking/macvlan.

Do not make `--privileged` the generic solution.

If a platform cannot safely expose the required capability, activation/deployment validation should report the Driver as unsupported on that host.

## 13. Windows distribution hypothesis

A possible long-term architecture is:

`Windows installer -> supported unattended container host -> canonical EliteSCADA OCI image`.

This could turn the Windows installer into a host-integration package rather than a separately built product implementation.

However this is only acceptable if a spike proves:

- unattended startup before user login;
- deterministic host reboot recovery;
- Windows service administration;
- offline install/update;
- required Driver/network/device access;
- acceptable security/support/resource overhead;
- no dependence on interactive Docker Desktop UX;
- compatibility with release trust/signing requirements.

Until then, native Windows packaging remains preserved.

Container-native does not mean forcing an operationally poor container layer onto Windows.

## 14. Linux distribution hypothesis

Linux is the most natural first production OCI target.

Future `.deb` packaging may become one of:

1. a host-adapter package that installs configuration/systemd integration and a pinned OCI artifact/runtime; or
2. a native Linux package maintained in parallel with OCI during transition.

The existing Linux distribution contract remains valid until exact technical validation selects the supported path.

## 15. Edge/embedded profile

The preferred first Edge topology to benchmark is:

`industrial Edge/controller host -> EliteSCADA OCI -> external PostgreSQL/TimescaleDB`.

Then evaluate local composed DB only for hardware with sufficient CPU/RAM/storage endurance.

Do not create device-specific product forks.

Individual hardware families become **homologated deployment profiles** with:

- architecture;
- container runtime/version;
- resource envelope;
- supported Driver matrix;
- network/device requirements;
- storage policy;
- capacity benchmark evidence.

## 16. Update and rollback

A production OCI distribution should support:

1. obtain/import new signed image;
2. verify digest/signature/provenance;
3. graceful stop;
4. preserve external state;
5. start new image;
6. health/readiness gate;
7. rollback to prior image if startup/migration fails inside the supported migration contract.

Offline industrial sites require deterministic image/archive import with manifest/checksums.

No automatic destructive migration.

## 17. HA and orchestration boundary

Containerization does not make EliteSCADA stateless and does not authorize arbitrary replication.

Existing Active Runtime authority, Authority state, licensing, persistence and HA fencing remain authoritative.

A scheduler being able to create multiple containers does not mean those replicas are safe.

Any future orchestrated multi-node topology must consume the existing HA contract.

Kubernetes is neither required nor implied by this ADR.

## 18. Implementation sequence

### Phase 0 — consolidate evidence

Consume #360 local-operation evidence and current Windows/Linux distribution contracts.

### Phase 1 — amd64 OCI architecture spike

Build the exact common product as a Linux amd64 OCI image.

Prove:
- start/stop/restart;
- readiness/liveness;
- external database;
- persistence across container replacement;
- diagnostics;
- no product-state storage in image layers;
- current Driver smoke.

### Phase 2 — arm64 multi-arch

Build `linux/arm64`.

Run:
- backend/runtime tests;
- Web tests;
- Driver/native dependency audit;
- representative Runtime smoke.

Record unsupported dependencies explicitly.

### Phase 3 — Edge benchmark/homologation model

Use representative Edge/controller hardware.

Measure:
- CPU;
- memory;
- TAG scan workload;
- Historian throughput;
- scripts;
- Driver mix;
- clients;
- storage I/O.

Create capacity profiles only from measured evidence.

### Phase 4 — deployment identity / licensing

Implement the versioned deployment/host identity provider model.

Prove:
- container recreate/update preserves valid license;
- same host accepts the license;
- unauthorized different host fails closed;
- explicit re-host process works;
- no ephemeral container identity is authoritative.

### Phase 5 — production Linux OCI

Produce signed/provenanced multi-arch image and offline bundle.

Add supported systemd/OCI host adapter, upgrade/rollback and diagnostics.

### Phase 6 — Windows container-host spike

Determine whether Windows can consume the same canonical image with acceptable unattended industrial operation.

Only after this spike decide whether the native Windows product artifact can be retired.

### Phase 7 — homologated Edge profiles

Publish explicit supported hardware/runtime/Driver/capacity matrices.

## 19. Current decision boundary

This ADR records a strategic direction, not immediate implementation authorization.

Current disposition:

`CONTAINER-NATIVE PREFERRED ARCHITECTURE CANDIDATE / VALIDATION REQUIRED`.

The architectural preference is:

- one common product implementation;
- one canonical OCI artifact where operationally supportable;
- host adapters rather than product forks;
- multi-architecture;
- explicit capacity/hardware profiles;
- container-independent stable licensing identity;
- externalized state;
- native operational lifecycle integration.

#360 must not widen into this implementation; it only collects evidence useful to it.
