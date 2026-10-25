# High Availability: configuration and operation

## HA: configure two servers and monitor operation

Distinguish saved configuration, active server, ready partner and duplicate-execution protection.

### What to prepare first

HA means High Availability: two servers can take over Runtime in a controlled way. Open Engineering > High Availability. HA Runtime licensing and HighAvailabilityObserve are needed; saving/operations also require HighAvailabilityTransfer. Check service networking, authentication, clocks and each node storage. Shared or separate databases need topology verification; enabling HA alone neither creates replication nor proves storage availability.

### Enter only installation-dependent settings

The page distinguishes This server and Partner server. Operators do not need to invent internal identities or automatic transport.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Each server main address | Endpoint reachable by services. A browser-detected address is useful only if the partner reaches it too; localhost on two hosts is not one server. | Actual addresses of both services |  |
| External address override | Open only when the network requires a different advertised address. Otherwise keep automatic values to avoid inconsistent targets. | No override on a simple network |  |
| Preferred server | Startup/failback preference; does not force promotion or establish readiness. | This server or partner |  |
| Inter-server authentication | Configure the shared secret through the protected flow on both nodes. Submitted values clear from the form; inspect authentication state without capturing the secret. | Configure authentication |  |
| Protection and shared reference | Fencing prevents two Active executors. Both nodes must see the same external shared reference; two local files with equal names are not shared authority. | Shared resource provisioned for the installation |  |
| Automatic failover | Allows eligible automatic takeover and requires protection. It does not replace partner readiness, available data or safety conditions. | Enable after checking readiness |  |

### Preparation procedure

1. Check Running mode.
2. Prepare HA, enter both addresses and select preference.
3. Configure authentication/shared protection and resolve HA readiness items.
4. Save desired configuration; Saved is not Running.
5. If required, plan service restarts and inspect what actually loaded.
6. Refresh and check effective Active, partner, database and protection. Standby is not Ready Standby.
7. Before process use, conduct the manual trial agreed with the installation owner; documentation checks do not replace it.

### Read states before operating

Effective Active is authorized to execute, not merely preferred. Synchronizing is still receiving/checking state. Ready Standby denotes backend-assessed readiness; Standby alone does not prove it. Promoting/Demoting are transitions: await the result. Stale means old observation; Isolated, Faulted, degraded or ambiguous authority require diagnosis. Database availability differs from peer reachability. Do not hide storage failure by increasing network tolerances.

### Choose the right operation

Switchover is planned transfer from effective Active to an eligible partner. Failback requests return to preference when allowed. Recovery reconciles a failure/block. Force resynchronization is specific recovery when no Active exists and previous authority was fenced; it does not declare a partner ready. Read target, confirmation and result; check history and resulting Active. Do not repeat a rejected request until its cause changes.

### Advanced settings: meaning and bounds

Use prefilled values as the starting point. Timing changes affect protection and failure response; assess network, clocks and operations first. Form bounds are not performance recommendations.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Freshness (s) | Maximum age of an observation considered recent. | Keep prefilled value | 1–120 s |
| Lease (s) | Execution-authority validity before expiry; not a commercial license. | Keep prefilled value | 3–120 s |
| Poll (ms) | Shared-reference polling interval; smaller means more frequent reads. | Keep prefilled value | 100–10000 ms |
| Maximum witness age (s) | Maximum age of partner-ready evidence; does not create readiness. | Keep prefilled value | At least Lease; up to 600 s |
| Clock margin (s) | Allowance for clock difference; not a substitute for time synchronization. | Keep prefilled value | 0 to floor(Lease/3) |

### If there is a problem

403: inspect observe/operate permissions. Saved differs from running: check pending restart. Unreachable peer: inspect service addresses, network and authentication. Unavailable reference: investigate external shared storage. Partner not ready: inspect synchronization, database and diagnostics without forcing promotion. Record time, action, result and effective node, never the secret.

[Open this guide in installed Help](/help?topic=ha.configuration-operation)
