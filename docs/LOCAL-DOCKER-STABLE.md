# EliteSCADA local stable container

This Compose stack builds the accepted EliteSCADA source in this repository,
then starts the Web UI, API and TimescaleDB together. The package includes the
E3-import cleanup: trial Elipse E3 dynamos are not seeded into new workspaces.
The TimescaleDB data is stored in a named Docker volume and survives container
restarts, image rebuilds and `docker compose down`.

## First start

1. Copy `docker-stable.env.example` to `.env` in the repository root.
2. Replace all secret placeholders with unique values. Use a long random
   database password without semicolons; set the history cursor key to Base64
   for 32 random bytes; and set the JWT signing key to at least 32 random
   bytes. Set a unique machine ID and keep it unchanged across restarts and
   upgrades so the license identity remains stable. The issuer and audience
   defaults are suitable only for this local instance. Do not commit `.env`.
3. From the repository root, run:

   ```powershell
   docker compose -f docker-compose.stable.yml up -d --build
   ```

4. Open [http://localhost:18080](http://localhost:18080). The first visit
   starts the secure first-administrator setup; no default user or password is
   created. The API is also available at `http://localhost:15120` and the
   database at `localhost:15432` for local tools.

The default Web and API ports are bound to `127.0.0.1`; PostgreSQL remains
loopback-only. The installation ID defaults to `elitescada-stable` and names
the Compose project and its three persistent volumes. Keep that ID stable to
reuse the same data. Override `ELITESCADA_WEB_PORT`, `ELITESCADA_API_PORT`, and
`ELITESCADA_DB_PORT` in `.env` when a host port is already in use.

### Prepare another isolated installation

Use a separate directory with its own `.env`, unique database/JWT/history keys
and machine identity. Never copy the first installation's `.env`. Choose a
different `SCADIVO_INSTALLATION_ID` and set it in the new `.env` before running
the helper. The helper does not change an existing installation ID and, in
second-installation mode, refuses IDs that already have containers or volumes.
The Compose project ID also namespaces the persistent volumes, so the second
installation cannot silently reuse the first one's database or protected
material.

In Engineering > Installation > Network and services, compare the current
Compose values, prepare a profile, then download the PowerShell or Bash helper.
Place the reviewed helper beside `docker-compose.stable.yml` and `.env`, and run
the displayed command from that directory on the Docker host. The helper checks
port availability and `docker compose config` before applying, backs up `.env`,
checks Web/API/database health, and attempts to restore the prior profile if
health fails. It never runs from the browser, changes firewall rules, prints
secrets, or deletes volumes. Keep the generated backup and local audit log.

The default port publication is local-only. Binding Web or API to `0.0.0.0`
allows the Docker host to listen on all its network interfaces. The product
does not configure TLS, reverse proxies or host/cloud firewall rules. Put a
remote deployment behind an explicitly configured TLS proxy and firewall.
Database publication remains fixed to loopback.

This helper covers the stable Docker Compose package on Windows/Docker Desktop
and Linux. Native Windows Service/systemd installations, remote databases, HA
peer URLs, and protocol/device ports are separate configuration surfaces.
MediaMTX's internal control and HLS ports can be changed together with the API
relay settings, but neither listener is published on the host. Driver ports
belong to their Data Source settings.

## Stop, restart and data

```powershell
docker compose -f docker-compose.stable.yml stop
docker compose -f docker-compose.stable.yml start
docker compose -f docker-compose.stable.yml ps
docker compose -f docker-compose.stable.yml logs -f
```

`docker compose ... restart` also stops and starts the stack. The named
`elitescada-stable-database` volume preserves accounts, projects, assets, audit
and historian data across all these operations and ordinary `down`/`up`
cycles. `docker compose ... down -v` deletes that persistent database and is
the explicit destructive reset; do not use it unless a clean reinstall is
intended.

## Scope and security

This is a stable local evaluation stack, not an internet-facing production
deployment. Ports are loopback-only and the browser session cookie is
non-Secure solely because this local stack uses plain HTTP. Do not expose it to
the LAN or Internet without putting it behind TLS and changing the cookie
setting. The first administrator is created through the product's first-run
flow. The database credential, Historical Query cursor key, JWT signing key
and stable machine ID live in the ignored `.env` file, not in the image or
source.

The API starts only after the database health check succeeds; its startup
foundation creates/upgrades the required EliteSCADA schemas. TimescaleDB is
enabled for durable historian samples and Historical Query. Docker health
checks expose only service health, not plant/project information.

### Access-port defaults

New profiles use Web 18080, API 15120 and PostgreSQL 15432. Existing installations should keep their explicit values in `.env`. Before updating an older installation that omitted `ELITESCADA_API_PORT`, record its current port explicitly (legacy package default 15080) to preserve its address. The installer scripts retain the legacy old-port fallback for preflight/recovery. No running installation is changed by editing the template.
