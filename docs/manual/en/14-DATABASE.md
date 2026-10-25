# Database: configuration, migration and recovery

## Database: configure, migrate and recover

Understand the active database before validating another target or migrating.

### Where to go and what this page does

Open Engineering > Database. SystemAdmin is required. Local Managed uses installation-managed local storage; Remote uses configured PostgreSQL. This selects storage, not HA server redundancy. Read Current status and Next action first. Healthy Local Managed needs no Remote profile if it suits your installation.

### Prepare the target

Agree on server, database, account, certificates and privileges with the administrator. The address must be reachable by the SCADIVO service, not merely the browser. In Docker, localhost inside a container means that container; use a service-reachable address. On Windows/Linux certificate paths belong to the service host. Back up and plan maintenance before migration.

### Basic and advanced fields

Start with host, user and secret. Open Advanced settings only to change supplied values. Examples are illustrative, not installation credentials.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Host / FQDN | Service-reachable PostgreSQL DNS name/address, not the web UI URL. | db.example.invalid |  |
| User / identity | PostgreSQL application account; not your SCADIVO Authority login. | Account supplied by the administrator |  |
| Password / secret | Sent without being returned in status. Clearing after submission does not mean the credential was lost. Never paste into tickets or captures. | Enter only in the protected form |  |
| Database | Target database name, not project name. Initial value is elitescada; confirm with the administrator. | elitescada |  |
| Port | PostgreSQL port. Initial value 5432 differs from the application HTTPS port. | 5432 |  |
| TLS mode | Require demands encryption. VerifyCa validates the CA chain; VerifyFull also checks the server name. Prefer and Disable do not provide equivalent protection. Use the IT-approved mode, not disabled TLS to hide an error. | Require or VerifyFull with valid CA |  |
| CA certificate path | CA file readable by the service; required by the form for VerifyCa/VerifyFull. A browser-computer path does not serve another host/container. | Path provisioned on the server |  |
| Timeout (s) | Connection wait; initially 15 seconds. Increasing it does not fix credentials, DNS or certificates. | 15 s |  |
| Historian uses Primary / dedicated | Selected, history uses the primary database. Clear only for a dedicated Historian target and fill its endpoint completely; it must also pass compatibility. | Use Primary for a simple setup |  |

### Migrate data to another database

1. Check the active database and back up.
2. Fill Remote and Validate target. Connection, authentication, TLS and compatibility are tested; reachability alone is insufficient.
3. Prepare migration and review source, target and included data. Configuration locks after preparation.
4. During maintenance, Start migration; durable writes may pause.
5. Wait for copy and automatic verification.
6. Only after successful verification select Activate Remote database and confirm.
7. If restart is required, restart/reopen the service for your installation. Recheck active topology, health, project and revision before resuming operations.

### Connecting an existing database is not migration

Connect to existing database (without copy) does not copy or merge records. Use it only for a prepared target with the same project and active revision required by the installation. Read confirmation. Project/revision mismatch blocks switching; do not empty or alter the database to bypass the check. Use migration to transport current data.

### Rollback and return to local storage

If Recovery/Rollback is required, review diagnostics and use Roll back operation when available. Return to Local Managed validates and reactivates preserved local storage and may need restart. It does not automatically merge later Remote writes into local storage. The previous database is preserved; returning is neither backup restoration nor Remote deletion. Check actual active data and revision afterward.

### Errors requiring different actions

No connection: check service DNS/port/access. Authentication rejected: verify PostgreSQL account/secret. TLS: inspect CA, certificate name and validity. Incompatibility: read PostgreSQL version, required TimescaleDB capability and schema. Invalid phase: refresh and follow Next action rather than starting another copy. Unavailable storage may degrade Runtime; memory must not pretend to be durable storage. Record time, phase and operation ID for support without passwords.

[Open this guide in installed Help](/help?topic=database.topology)
