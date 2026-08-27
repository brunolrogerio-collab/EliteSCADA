# Research convergence readiness

Status: **ARCHITECTURE REVIEWED / IMPLEMENTATION GATES PRESERVED**

This matrix records how every merged research stream maps to the current EliteSCADA architecture after the research-convergence review. It prevents future implementation work from rereading individual research documents in isolation and accidentally choosing contradictory platform boundaries.

## Summary

| Research | Common contracts now aligned | Deliberately pending before production |
| --- | --- | --- |
| MQTT industrial driver | event-driven acquisition; Data Source/session isolation; Driver descriptor/config schema; Engineering capability separation; normal TAG writes/Gateway; common diagnostics; source timestamps possible | exact canonical MQTT binding schema; secret/TLS resolver; library selection; Observe Topics UX/runtime adapter; Sparkplug profile |
| OPC UA discovery/import | Engineering discovery/browse separated from Runtime; subscription acquisition; portable identity rule; source/server timestamps; common quality/diagnostics direction | exact canonical OPC UA binding DTO; certificate trust store; official-stack dependency pin; subscription profiles; browse/import API/UI |
| BACnet/IP + BACnet/SC | Hybrid acquisition; one logical Data Source/device; shared transport permitted without losing DS isolation; discovery/browse interfaces; common diagnostics | exact BACnet binding/config schema; BACnet/IP adapter; COV planner; BBMD/FDR tooling; BACnet/SC certificate/hub infrastructure |
| Siemens S7 ISO Connection | polling acquisition; capability-specific connection test/file import direction; portable typed binding rule; module descriptor direction | S7 module loader/factory; exact S7 binding schema; TIA import adapter; dependency/hardware scorecard; protocol runtime |
| Allen-Bradley Logix EtherNet/IP/CIP | polling/explicit-message direction; symbolic portable identity; browse/file-import separation; route/identity diagnostics direction | exact Logix binding/route schema; library/native packaging decision; ODVA/licensing gate; real-hardware acceptance; runtime adapter |
| Client Visual Python sandbox/editor | existing narrow Script/API/sandbox contracts remain authoritative; scripts do not access drivers; architecture docs now cross-reference this boundary | canonical Script package/schema integration; pinned engine lab; sandbox host; editor; Preview runtime; packaged CSP/COOP/COEP proof |
| Graphical visual editor | existing typed visual-property/runtime contracts remain downstream of canonical Engineering; stable IDs and renderer independence remain locked | canonical visual DTO reconciliation; Script integration; assets/Dynamo public parameters; editor/runtime composition; renderer implementation |

## Code-level changes made by this convergence

### Driver SDK

Added protocol-neutral, library-independent foundations for:

- stable Driver type descriptor;
- versioned Data Source/TAG-binding configuration schema descriptors;
- independent Engineering capabilities for connection test, discovery, browse, file import and reconciliation;
- acquisition-mode declaration: Polling, Subscription, EventDriven, Hybrid;
- transient discovery/browse/import/reconciliation candidates that cannot mutate canonical Engineering by themselves.

### TAG value time semantics

`TagValue` preserves the existing constructor and local `Timestamp`, while adding optional `SourceTimestamp` and `ServerTimestamp` fields for protocols that genuinely provide them.

This avoids later forcing OPC UA or timestamped MQTT/device data into a single ambiguous timestamp field.

## Decisions intentionally not made

### No generic protocol settings dictionary as the final SDK

Current Schema v9 still stores Data Source `Settings`/`SecretReferences` and TAG `Address` for compatibility. The new Driver schema descriptors define the intended public shape, but persistence is not migrated until a dedicated canonical-schema task can include:

- migration from prior schema versions;
- JSON round-trip tests;
- Preview/Apply behavior;
- PostgreSQL revision persistence;
- `.escadapkg` backup/restore;
- missing-module representation;
- frontend editor generation;
- regression compatibility.

A partial schema migration now would be worse than leaving the current shape explicit and scheduled.

### No universal discovery algorithm

The host standardizes authorization, cancellation, limits, result handling and candidate lifecycle. Each protocol implements only its real Engineering mechanisms.

There is no architectural requirement that MQTT implement OPC UA browse, that S7 scan a network, or that BACnet perform TCP probing.

### No universal subscription object

Acquisition mode is public metadata; protocol subscription/session objects remain private adapters. OPC UA monitored-item objects, BACnet COV subscriptions and MQTT broker subscriptions do not become common serialized entities merely because all can deliver updates.

### No direct Script/visual access to drivers

Python and graphical runtime work continues through canonical TAGs, commands, memory and visual-property APIs. It never receives `ICommunicationDriver`, protocol sessions or device addresses as privileged objects.

## Required reading for future implementation tasks

Before implementing any new external protocol or Driver Module, read together:

1. `PROJECT GOAL.md`;
2. `docs/ADR-002-DRIVER-SDK-AND-REALTIME.md`;
3. `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`;
4. `docs/ADR-009-DRIVER-SDK-ENGINEERING-BOUNDARIES.md`;
5. `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`;
6. `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
7. `docs/TAG-GATEWAY.md`;
8. the protocol-specific research document.

For Python/visual implementation, also read `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md` plus the corresponding merged research document.

## Product gate

This convergence is architectural preparation only. Current product order remains:

`interface product development -> user interface validation build/package -> additional external protocols`

The presence of ready Driver SDK contracts does not reopen the production protocol gate and does not authorize the Python/visual implementation chain ahead of its canonical prerequisites.
