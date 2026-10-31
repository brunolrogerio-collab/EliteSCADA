# Database: configuration, migration and recovery

## Database: configure, migrate and recover

Understand the active database before validating another target or migrating.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-DATABASE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=database.topology; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Where to go and what this page does

This screen defines where SCADIVO stores its data. Open Engineering > Database with system administration permission (SystemAdmin).

Local Managed is the local database managed by the installation. Remote is a PostgreSQL database you prepare at another destination. If the local database is healthy and meets your needs, you can keep using it without filling Remote.

Before changing anything, read Current state and Next action. Choosing a database defines storage; it does not by itself configure HA redundancy between two servers.

### Historian usage and retention

In Historian usage and retention, review the measured sizes in the database used by the Historian. Historian database includes all data in that database; Historian total combines raw samples and aggregates the provider could measure. With separate databases, this page measures only the Historian database; it does not add the primary database or represent free space on the server volume. Server free space and daily growth appear only when a reliable measurement is available. If the page says they were not reported, that does not mean zero.

No automatic expiry means SCADIVO has not scheduled a date to remove raw samples. It does not mean disk space is unlimited. To keep raw samples for a period, clear No automatic expiry and enter the number of days. A shorter policy can remove old samples; check the confirmation only after reviewing the period and its impact. TimescaleDB runs the retention policy, so removal may not happen at the moment you save.

For the first application, when no SCADIVO policy is recorded, the form asks you to confirm before taking over Timescale retention and aggregate-refresh policies. Aggregate tiers with no recorded configuration will be disabled, and unrecorded external rules will be lost. Check with the database administrator before proceeding.

The GiB limit applies only to raw Historian samples and includes TimescaleDB chunks, indexes and auxiliary data. It does not limit aggregates, alarms, events, Audit, the primary database or server free space. Aggregates keep their own retention policy.

Under When the limit is reached, Warn only shows a warning without deleting data. Delete complete oldest chunks requires confirmation of permanent raw-sample loss. Every minute, maintenance can remove up to four complete old time intervals and keeps the newest interval. Chunk size makes this a target rather than an instant ceiling; usage may remain above it.

The size limit and retention by days can coexist; whichever condition removes samples first takes effect. Review the measurement after maintenance and plan with a margin. If samples must be kept outside the active database, configure and monitor archiving before a retention or size policy removes them.

Under Archive and restore samples, create an archive for a selected period or configure scheduled archiving. An archive contains raw Historian samples only. It does not include aggregates, alarms, events, Audit, the project, Authority, licensing, or HA state. Dates entered in the browser are converted to UTC.

In the schedule, Start archiving from sets the initial cursor. Archive only data at least this many days old leaves a buffer for recent data. Run every controls when the next batch may start; Archive up to per run limits the number of days handled in one batch. SCADIVO advances the cursor only after the archive is verified and completed. A blocked job needs the destination fixed and a manual resume; source data stays available.

Keep archive files without an expiry date to retain .scadhist packages. If you set a retention period, rotation removes completed archive files older than that many days; it does not remove database samples. In Docker, mount the destination folder on persistent storage. If the API has multiple nodes, all nodes must use the same shared persistent folder; different local folders cannot be a common archive destination.

Source removal is separate from file rotation and starts unchecked. It starts only after the complete package has been read back and verified. Progress is saved one segment at a time in the same transaction as that segment's source deletion; after a restart, already confirmed segments are skipped and later-arriving samples remain in the database. The batch is marked Completed only after every authorized segment is processed. The schedule requires its own confirmation. Check the cursor and job state, and test a restore before relying on archiving to free space. If the raw-sample size limit removes chunks before the cursor reaches them, those samples can no longer be archived.

To restore, choose a .scadhist file and select Verify and restore. SCADIVO validates the entire package before inserting samples. For each segment, it compares how many identical occurrences are already present and inserts only the missing rows, avoiding duplicates that remain in the database. Progress is recorded in the active Historian. Repeating the archive does not duplicate samples and can refill data removed later by a retention policy. Keep copies outside the rotated folder if you need the files longer.

### Prepare the target

Ask the administrator for the destination details: server address, database, account, permissions and certificates. Make a backup and agree a maintenance window before migrating. Migration may pause writes.

The connection comes from the SCADIVO service. Opening an address in a browser therefore does not prove the service can reach it. In Docker, localhost means the container itself; use a name or address the service can actually access.

Likewise, the certificate file must exist on the server running the service, whether Windows, Linux or a container. A path available only on the browser computer does not meet that requirement.

### Migrate data to another database

1. Check which database is active and have a backup available.
2. Fill the Remote destination and use Validate destination. Validation checks connection, authentication, TLS protection and compatibility; connection alone is not enough to permit switching.
3. Prepare migration and review the plan: source, destination and included data. Configuration becomes locked after preparation.
4. During the maintenance window, use Start migration. Durable writes may be paused during the operation.
5. Wait for copying and automatic verification. Read the result before proceeding.
6. Only after successful verification, use Activate Remote database and confirm the destination.
7. If restart is requested, restart or reopen the service as appropriate for the installation. Before resuming, check active database, health, project and revision.

### Connecting an existing database is not migration

Use Connect existing database (without copying) only when the destination is already prepared with the same project and active revision required by the installation. This path does not copy or merge records. Use migration to take current database data to a new destination.

Read the confirmation before switching. If project or revision is incompatible, the operation is blocked. Do not empty or modify the database to bypass this check.

### Rollback and return to local storage

If Recovery/Rollback required appears, the operation needs recovery or reversal. Read diagnostics and use Roll back operation when available.

Returning to Local Managed checks and reactivates the preserved local database; restart may be required. It does not automatically merge data written to Remote after the switch. The previous database is preserved, but this is not the same as restoring a backup. It does not delete Remote either.

After returning, check which data, project and revision are actually active before resuming operation.

### Basic and advanced fields

To prepare Remote, start with the address, username and password supplied by the administrator. Other fields can keep their filled values when suitable for the installation. Open Advanced settings to review or change those details. The table below explains each choice; examples are not real credentials.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Host / FQDN | Service-reachable PostgreSQL DNS name/address, not the web UI URL. | db.example.invalid |  |
| User / identity | PostgreSQL application account; not your SCADIVO Authority login. | Account supplied by the administrator |  |
| Password / secret | Sent without being returned in status. Clearing after submission does not mean the credential was lost. Never paste into tickets or captures. | Enter only in the protected form |  |
| Database | Target database name, not project name. Initial value is elitescada; confirm with the administrator. | elitescada |  |
| Port | PostgreSQL port. Initial value 5432 differs from the application HTTPS port. | 5432 |  |
| TLS mode | TLS protects the connection. Require demands encryption; VerifyCa also checks the certificate authority chain; VerifyFull additionally checks the server name. Prefer and Disable do not guarantee the same protection. Use the mode agreed with IT; do not disable TLS just to hide an error. | Require or VerifyFull with valid CA |  |
| CA certificate path | CA file readable by the service; required by the form for VerifyCa/VerifyFull. A browser-computer path does not serve another host/container. | Path provisioned on the server |  |
| Timeout (s) | Connection wait; initially 15 seconds. Increasing it does not fix credentials, DNS or certificates. | 15 s |  |
| Historian uses Primary / dedicated | When checked, recorded history, called Historian, uses the primary database. Clear only if you prepared a dedicated history database. Then fill its complete connection details and validate that destination’s compatibility too. | Use Primary for a simple setup |  |

### Errors requiring different actions

Start with the message received. For no connection, check name/address, port and access from the service. Authentication refusal calls for checking the PostgreSQL account and password. A TLS error calls for checking the certificate authority, server name and certificate validity.

For incompatibility, read PostgreSQL version, TimescaleDB requirements when applicable and schema, the data structure the product expects. For invalid phase, refresh state and follow Next action; do not start another copy while one is in progress.

When the database is unavailable, Runtime can be degraded. The product must not present memory writes as database persistence. For support, record time, phase and operation ID; omit passwords.

[Open this guide in installed Help](/help?topic=database.topology)
