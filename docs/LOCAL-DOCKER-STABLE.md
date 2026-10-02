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
   created. The API is also available at `http://localhost:15080` and the
   database at `localhost:15432` for local tools.

The default ports are bound to `127.0.0.1` and do not occupy the usual test
ports 5173, 5080, 5081 or 5432. Override the three `*_PORT` values in `.env` if
one of the alternate ports is already in use.

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
