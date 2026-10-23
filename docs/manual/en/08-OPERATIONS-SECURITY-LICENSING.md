# Operations, Security, and Licensing

## Runtime

Runtime executes the Active revision under backend authority. The session receives effective capabilities and, where applicable, a Runtime lease.

Loss/expiration of interactive authority does not make the session authorized by default.

## Interactive and View Only

Interactive and View Only represent session classes. View Only reduces operational authority; in particular, it does not grant write/command access even if the user has permissions that could be used in an interactive session.

Seat/capacity grants come from the license entitlement and the server; sockets or visual controls are not the unit of authority.

## Commands

Commands represent operator intent and go through server-side validation/authorization. A rendered button does not prove that the action will be accepted.

**Related Help:** `/help?topic=bindings-commands.overview`.

## Diagnostics and communication quality

Use layered diagnosis: backend, identity/capabilities, Active, Data Source, Driver, connection, binding, quality/timestamp, and presentation. Run a Driver's Engineering capabilities only when declared by its actual descriptor.

**Related Help:** `/help?topic=diagnostics.overview`.

## Authority, Users, Roles, Capabilities, and Scopes

Authority manages identities and assignments. Roles feed authorization; effective capabilities are calculated/enforced on the server.

Do not treat a role name as sufficient proof of privilege. The product must resolve effective capabilities.

## Engineering Lock

Engineering Lock protects the application's development content and is independent of:

- the user's account password;
- the Authority backup password;
- Licensing.

When the application is locked, protected editors must not be exposed. Permitted administrative functions remain subject to normal backend authorization.

Engineering Lock changes are part of Working and must follow Save/Publish/Activate to become durable Active state.

### Procedure: unlock the current application

**Prerequisites:** permitted access to restricted administration and the correct Engineering Lock secret.

**Steps:**
1. Open the application's protection surface.
2. Enter the Engineering Lock secret.
3. Run Unlock.
4. Confirm that Engineering is available again according to the session's capabilities.

**Expected result:** only the current application is unlocked; this operation does not authenticate a user or change the license.

**Expected error:** an incorrect secret keeps Engineering locked.

## License concepts

When no license is installed, the state is Demo. A valid license is asymmetrically signed and machine-bound.

The license controls commercial entitlement; it does not replace Authority, Engineering Lock, lifecycle, or package validation.

### Demo/no-license

Under the implemented operational contract:

- Engineering may contain more than 200 TAGs;
- Demo Run is allowed for projects with up to 200 TAGs;
- each successful Demo Run starts a continuous session of up to 300 minutes;
- expiration ends industrial Runtime in a controlled manner and retains Engineering;
- a new explicit Run starts a new Demo session.

An installed but invalid/tampered/wrong-hardware license blocks Run; it must not be silently treated as if no license were installed.

## Machine binding and Request

Installation generates a versioned Machine Request Code from the machine's stable identity. The normal workflow does not need to expose raw hardware identifiers to the operator.

### Procedure: obtain the request

1. Open Licensing.
2. Check the current status.
3. Copy the Machine Request Code.
4. Send only the request to the licensing authority.

**Expected result:** the code can be used by the controlled issuance tool without distributing the private key.

## Install / Replace / Remove

License installation validates the signature, key ID, hardware binding, and validity before replacing the installed state.

To replace a license, install the new license validated by the normal workflow. An invalid attempt must not replace an existing valid license.

Remove deletes the installed license; future activations return to Demo rules, provided no invalid external state blocks Run.

## TAG tiers

Documented tiers: 500, 1000, 1500, 3000, 5000, and Unlimited. A valid license removes the Demo timeout and applies the corresponding TAG limit.

## Runtime session quotas / Interactive / View Only

Licensing status exposes View Only and Interactive seat entitlements when present. Counting and enforcement belong to the server; a transport connection must not be confused with a licensed logical session.

## Redundancy entitlement

License status may expose an HA Runtime entitlement. Entitlement enables the surface; it does not replace correct configuration, Authority, peer transport, fencing, or operational verification.

## High Availability: configure and monitor

The **High Availability** page distinguishes Standalone from HA and presents desired and running configuration, readiness, node and peer state, effective Active, health, freshness, fencing reference, and operation history. HA uses two redundant servers; database topology is a separate decision and must be checked for each installation.

### Prepare the configuration

1. Confirm an HA Runtime license, two reachable servers, and an account with `HighAvailabilityObserve`; saving or operating also requires `HighAvailabilityTransfer`.
2. In Engineering > High Availability, select HA to prepare the topology. Standalone continues running while preparation is pending.
3. Enter each server's primary endpoint. Keep the automatic endpoint unless the network actually requires a different external address. Configure peer authentication; never place the shared secret in documentation or support tickets.
4. Review the preferred node. If the topology uses protection, enable it and enter the shared fencing reference. Automatic failover depends on this protection.
5. Resolve the items under **HA readiness** and select **Save desired configuration**.
6. If the page indicates that a restart is required, desired configuration is not running yet. Schedule the restart according to the installation's operating procedure; then refresh and confirm both nodes, peer, effective Active, and protection state.

### Transfers and recovery

Use **Switchover** for a planned transition when this server is the effective Active; the backend still validates eligibility and may reject the request. Use **Failback** only when the backend permits return to the preferred node. **Recovery** and **Force resynchronization** are recovery actions for the conditions shown on the page, not shortcuts to force a node Ready. Read the confirmation and check the operation result/history.

Before relying on automatic failover, exercise the scenario on the actual topology and verify Runtime, database, and duplicate-command consequences. A stale, degraded, or ambiguous state requires investigation of connectivity, freshness, and fencing; do not force promotion to clear the warning.

**Related Help:** `/help?topic=ha.overview`, `/help?topic=licensing.overview`, `/help?topic=security.users-roles-capabilities`, `/help?topic=diagnostics.overview`.

## License Generator

The License Generator is an offline/controlled licensing-authority tool. The signing private key must never be placed in GitHub, normal CI, a client installer, or Runtime.

End users of EliteSCADA use request/install; only authorized issuance personnel should operate the signing environment.

**Related Help:** `/help?topic=licensing.overview`.
