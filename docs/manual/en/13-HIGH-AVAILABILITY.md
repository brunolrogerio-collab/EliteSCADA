# High Availability: configuration and operation

## HA: configure two servers and monitor operation

Distinguish saved configuration, active server, ready partner and duplicate-execution protection.

### What to prepare first

HA means High Availability. Two servers are prepared so Runtime can move between them with protection against duplicate execution. In this guide, node means either of those servers.

Open Engineering > High Availability. Observation requires the HA Runtime license and permission to monitor the servers. Saving or operating also requires transfer permission. Check service-to-service networking, authentication, clocks and storage on both nodes.

Databases may be shared or separate according to the prepared installation. Enabling HA does not create data replication or guarantee database availability. Check that part with the installation administrator before configuring the pair.

### Enter only installation-dependent settings

The screen calls the nodes This server and Partner server. Start with addresses that allow their services to communicate. Use the picker for preferred server and the protected authentication flow. The product handles internal identities and automatic transport; do not invent those values. The table details the choices.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Each server main address | Endpoint reachable by services. A browser-detected address is useful only if the partner reaches it too; localhost on two hosts is not one server. | Actual addresses of both services |  |
| External address override | Open only when the network requires a different advertised address. Otherwise keep automatic values to avoid inconsistent targets. | No override on a simple network |  |
| Preferred server | Startup/failback preference; does not force promotion or establish readiness. | This server or partner |  |
| Inter-server authentication | Configure the shared secret through the protected flow on both nodes. Submitted values clear from the form; inspect authentication state without capturing the secret. | Configure authentication |  |
| Protection and shared reference | This reference supports protection against two Active servers at once, called fencing. Both must see the same shared external storage. Two local files with matching names are not one common reference. | Shared resource provisioned for the installation |  |
| Automatic failover | Allows eligible automatic takeover and requires protection. It does not replace partner readiness, available data or safety conditions. | Enable after checking readiness |  |

### Preparation procedure

1. Check Running mode before editing. It reports the configuration the service is using now.
2. Prepare HA, enter both service addresses and choose the preferred server.
3. Configure authentication and shared protection. Resolve items shown under HA readiness.
4. Use Save desired configuration. This stores the choice but does not prove it is already Running.
5. If restart is pending, plan it and then check the configuration actually loaded by the services.
6. Refresh state. Check effective Active, partner readiness, database and protection. Standby alone does not mean Ready Standby.
7. Before process use, perform the manual exercise agreed with the installation administrator. This guide’s checks do not replace that evaluation.

### Read states before operating

Effective Active is the server authorized to execute now. It may not be the preferred one. The partner may merely be in Standby, waiting, or in Ready Standby, with readiness evaluated by the server. Read that distinction before transferring.

Synchronizing means synchronization is still in progress. Promoting and Demoting are transitions: wait for the result. Stale warns that the observation is old. Isolated, Faulted, degraded state or ambiguous authority call for diagnosis.

Check database availability separately from partner communication. Reaching the partner does not prove the database works. Increasing network tolerances to hide a database fault does not resolve it.

### Choose the right operation

Choose the action for the situation. Switchover is a planned transfer from effective Active to an eligible partner. Failback requests return to the preferred server when allowed. Recovery reconciles a failure or blocked condition.

Force resynchronization has a specific condition: there must be no Active, and the previous authority must be fenced, meaning prevented from continuing execution. The action does not let the operator declare a partner ready.

Before confirming, read action and target. Afterwards check result, history and which node is Active. If the request is refused, investigate the cause before trying again.

### Advanced settings: meaning and bounds

Initially keep the filled timing values. They control how long information is recent and how protection responds to failures.

Adjust only after assessing network, clocks and operation. A number fitting the form’s limit does not make it a recommendation for your installation. The table explains each timing’s effect.

For checking permissions in Security: HighAvailabilityObserve allows observation; HighAvailabilityTransfer is also required for saving and operating. These are reference identifiers for the administrator.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Freshness (s) | Maximum time for treating an observation as recent. After that it may be too old to guide operation; this is not a TAG acquisition interval. | Keep prefilled value | 1–120 s |
| Lease (s) | Lifetime of the temporary authorization for the server to execute as Active. Once expired, that authority is no longer valid. Lease is not a commercial license. | Keep prefilled value | 3–120 s |
| Poll (ms) | Time between checks of the shared reference. A lower value checks more often. This field uses milliseconds, not seconds. | Keep prefilled value | 100–10000 ms |
| Maximum witness age (s) | Maximum age of partner-ready evidence; does not create readiness. | Keep prefilled value | At least Lease; up to 600 s |
| Clock margin (s) | Allowance for clock difference; not a substitute for time synchronization. | Keep prefilled value | 0 to floor(Lease/3) |

### If there is a problem

For a 403 response, check permission to observe or operate. If Saved differs from Running, check for pending restart. For an unavailable partner, examine addresses, networking and authentication between services.

If the protection reference is unavailable, investigate shared external storage. If the partner is not ready, read synchronization, database and diagnostic information; do not force promotion to bypass the condition.

Investigate one problem at a time. Record time, action, result and effective server for support, without the authentication secret.

[Open this guide in installed Help](/help?topic=ha.configuration-operation)
