# Operations, Security, and Licensing

## Runtime

Runtime executes the Active revision under backend authority. The session receives effective capabilities and, where applicable, a Runtime lease.

Loss/expiration of interactive authority does not make the session authorized by default.

### Stop and start the operating application

In the Engineering overview, card **4 — Active** shows the Runtime's reported state. With `EngineeringModify`, select **Stop Runtime** and confirm. This stops the application's drivers, data collection, and Server Scripts. The revision stays Active; the Engineering/API service, installed license, and operator login are preserved. Runtime sessions and equipment connections may be interrupted and must reconnect when Runtime starts again. Stop does not unpublish the revision or revoke/remove the license.

After a complete stop, **Start Runtime** resumes the same Active revision and checks license and authority again. It does not renew or reset a bounded Demo period. If the interface reports an incomplete stop, review the affected components before starting. With HA enabled, local controls are unavailable because they would not stop the whole cluster; use coordinated cluster maintenance.

The screenshots below show both actions in the overview; they were captured during integrated local validation.

![Runtime running: the Active card offers Stop Runtime](../assets/runtime-active-overview.png)

![Runtime stopped: the Active card offers Start Runtime](../assets/runtime-stopped-overview.png)

An accepted automatic license renewal happens in the background and does not by itself stop Runtime. A rejected, revoked, or expired license follows the product's entitlement protections.

**Related Help:** `/help?topic=engineering.lifecycle`, `/help?topic=licensing.overview`, `/help?topic=ha.overview`.

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

<!-- HELP-REVIEW-301: Review online activation and quota-consumption display when delivered. Unlimited Demo duration is already described below. -->
**Upcoming updates:** account-based online activation and usage indicators are still being developed. Use the options available in your version; this text does not declare the online workflow complete.

Without an installed license, you can use Runtime in Demo with no time limit:

- Engineering may contain more than 200 TAGs, but Demo Run accepts up to 200 TAG definitions;
- Demo has no countdown: you do not need to stop and start Run periodically;
- Runtime can recover after a restart without requiring the start time of an old Demo session;
- quotas are 2 Interactive and 2 View Only clients, shared by Runtime clients; HA and Intelligence require their own entitlement;
- Licensing shows **Demo duration: No time limit**. User permissions and other operating requirements are still checked.

Above 200 TAGs, the product refuses the complete Run and explains the limit; it does not silently choose a subset of TAGs. Your Engineering project and data remain available.

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

Documented tiers: 500, 1000, 1500, 3000, 5000, and Unlimited. A valid license applies the TAG limit and signed features corresponding to the purchased tier.

## Runtime session quotas / Interactive / View Only

On the **Licensing** page, **In use** appears alongside each limit. TAGs are the definitions in the running application; Interactive and View Only are active logical sessions shared by Runtime clients. Reconnecting the same session does not consume another seat. Select **Refresh** to check again. When no Runtime is active or the reading is temporarily unavailable, the page shows **Unavailable** instead of a fabricated zero. Older licenses may not specify session limits.

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

## Network ports for the stable Docker package

In **Engineering > Installation > Network and services**, a `SystemAdmin` user can prepare ports for the stable Docker distribution. The page displays the Compose configuration reported by the current container, but cannot confirm by itself that the host computer actually published each port.

### Ports controlled by this profile

| Service | Host default | Inside container | Notes |
| --- | ---: | ---: | --- |
| Web | `127.0.0.1:18080` | `80/TCP` | Runtime and Engineering navigation |
| Direct API | `127.0.0.1:15120` | `5080/TCP` | Web forwards `/api` to this API |
| PostgreSQL | `127.0.0.1:15432` | `5432/TCP` | Host publication remains loopback-only |
| MediaMTX control | Not published | `9997/TCP` by default | Configurable internal port |
| HLS | Not published | `8888/TCP` by default | Configurable internal port |

The MediaMTX control and HLS internal ports can be changed in this profile and remain private on the Docker network. The same settings update the addresses the API uses to reach the media service. Runtime sends video through the API relay using short-lived tickets; do not publish MediaMTX directly on the host.

Driver protocol ports remain configured in each Data Source according to the connection and device. This profile does not publish Driver listeners or change ports on external equipment. See the guides for [connections and protocols](21-CONNECTIONS-AND-PROTOCOLS.md), [BACnet/IP](22-HOME-BUILDING-CONNECTIONS.md), and [Modbus TCP Server](25-TELECONTROL-AND-MODBUS-SERVER.md).

Remote database ports and HA peer addresses belong to their own integrations. This page does not open firewalls or change external services.

### Change ports for the same installation

1. Open **Engineering > Installation > Network and services** and check the current ID, binds, and ports.
2. Enter integer ports from 1 to 65535. Web, API, and database host ports must differ; media control and HLS internal ports must differ from each other. Web and API can bind to `127.0.0.1` (host only) or `0.0.0.0` (all interfaces). The database remains on `127.0.0.1`; media remains private on the Docker network.
3. Review the new address and restart impact. For example, setting Web to `28080` moves navigation to `http://localhost:28080`.
4. Download the PowerShell script (Windows/Docker Desktop) or Bash script (Linux), place it beside `docker-compose.stable.yml` and `.env`, review it, and run the displayed command in a terminal on the Docker host.
5. The script checks host-port availability, conflicting internal media ports, and `docker compose config` before the change. It then backs up `.env`, applies the profile, and checks Web, API, database, and private media listeners. It does not remove volumes. If a check fails, it attempts to restore the prior `.env` and services.
6. Open the new Web address. The previous tab may disconnect while the container is recreated.

Do not put secrets in the command: it contains only the instance ID, bind addresses, and port numbers. `.env` remains on the host; its `.env.network-backup-*` backup and `network-profile-audit.log` also stay in the installation folder.

### Create another installation

Use another installation folder, create an independent `.env` with its own passwords/keys/identity, and set its new `SCADIVO_INSTALLATION_ID` before running the command. Do not copy the first installation's `.env`. In second-installation mode, the script requires the ID to be set in the new `.env` and refuses an ID that already has containers or volumes. The ID separates the Compose project and volumes, so the new installation starts with its own database and data; it does not duplicate the first installation's data.

Binding Web/API to `0.0.0.0` allows them to respond on every network interface. For remote access, explicitly configure TLS/reverse proxy and firewall policy outside SCADIVO. If HA is enabled, update peers and check health after changing URLs; this is not done automatically.

### Recover access

If the page no longer opens, use the new address shown in the profile. If the script reports rollback, keep the backup and check host logs/health. To restore manually, copy the correct `.env.network-backup-*` to `.env`, then run `docker compose -f docker-compose.stable.yml up -d` in that installation folder. Do not use `down -v`; that deletes persistent data.

**Related Help:** `/help?topic=installation.network-ports`, [stable Docker installation](../../LOCAL-DOCKER-STABLE.md).

## Ports on native Windows and Linux

**[IN DEVELOPMENT — review during installer qualification]** Adapters exist in this candidate; the installer must still supply the protected administrative profile and qualify real services. If the profile is missing, do not invent file paths or service names.

In **Installation > Network and services**, review current addresses. Web is where you open the screens; API is direct backend access. Their ports are independent. New installations use Web `18080`, API `15120`, and locally managed PostgreSQL `15432` automatically. Existing installations retain their values; older Docker versions could use API port `15080`.

To change Web/API, edit only the desired ports and check the preview. A command appears only when the installation supplies a profile matching its effective configuration. Run the preview in the administrative directory on the server, not in the browser; applying requires a separate explicit action. After changing Web to `28080`, for example, reopen `http://localhost:28080`.

Database and media use separate administrative procedures; the Web/API command shown on screen does not run them. Change only locally managed PostgreSQL, never a remote server through this procedure. Media ports must match the API relay addresses; control and HLS must not be published directly to users.

After interruption, preserve backups and the recovery record. Run the relevant command with `--recover` on Linux or `-Recover` on Windows, without new ports. Pending recovery blocks changes by other adapters. Do not delete the record to continue. After recovery, open the interface, use **Check database**, and verify Runtime and video. Process health alone does not prove these functions.

Full commands and prerequisites are in `installation/NATIVE-PORTS.md` in the administrative package. Firewall, Drivers, and HA peers are not changed automatically. Protected directory provisioning and complete authenticated verification in the adapters still require qualification.

## License Generator

The License Generator is an offline/controlled licensing-authority tool. The signing private key must never be placed in GitHub, normal CI, a client installer, or Runtime.

End users of EliteSCADA use request/install; only authorized issuance personnel should operate the signing environment.

**Related Help:** `/help?topic=licensing.overview`.
