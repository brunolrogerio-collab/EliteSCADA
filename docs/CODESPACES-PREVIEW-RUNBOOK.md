# EliteSCADA — C11 Canonical Codespaces Preview Runbook

## Authority and scope

This runbook operates the temporary Product Owner browser Preview for the frozen Wave 14 C11 canonical EEE Demo.

- canonical implementation branch: `wave14/c11-canonical-eee-demo`
- accepted frozen C11 head: `a724ece64a292aa1d1dedd886a72fb28ff8d90fe`
- Preview successor branch: `preview/wave14-c11-canonical-preview`
- frozen application package: `preview/fixtures/EliteSCADA-EEE-Demo.escadapkg`
- frozen package SHA-256: `4be1ca2338094799a8bf3c989322e5488a2381ce65d92cac3871188639e215c6`
- canonical project key: `eee-demo`

The Preview branch is a homologation harness, not a product-authority branch and not a route to `main`. PR #212 remains OPEN/DRAFT and has no merge authorization. PR #266 remains validation-only and MUST NEVER MERGE.

## Product boundaries

The Preview must exercise the real product contracts. It does not get a Preview-only runtime path.

The bootstrap sequence is:

`First Project -> Project Package Inspect -> Import Preview -> Import Apply -> Save -> Publish -> Activate -> Active Runtime`

`/api/project-package/import/apply` must use the normal Engineering Modify authorization and the current `x-elitescada-workspace-version` concurrency contract.

Backend persisted Active Revision remains Runtime authority. The frozen `.escadapkg` must remain self-contained and may not depend on `.escadalib`.

Authentication, Engineering Lock, licensing, lifecycle, package validation and Runtime authority must not be weakened to make Codespaces work.

## Create a fresh Preview

1. Confirm the live Preview PR still points at `preview/wave14-c11-canonical-preview` and record its exact head SHA.
2. Confirm the specialized `C11 Canonical Test Preview` workflow is green for that exact SHA.
3. Ensure the repository Codespaces secret `ELITESCADA_PREVIEW_ADMIN_PASSWORD` is configured. Do not put its value in commits, comments or logs.
4. Create a Codespace from the Preview branch, not from `main`.
5. The devcontainer creates a disposable machine identity and mounts it read-only at `/etc/machine-id`; this preserves the normal fail-closed licensing path.
6. `postCreateCommand` restores .NET and frontend dependencies.
7. `postAttachCommand` automatically runs `bash scripts/preview/launch-test-preview.sh`.
8. Wait for port `5173`, **EliteSCADA Web — C11 Test Preview**, to open. Keep it Private.
9. API `5080` and TimescaleDB `5432` remain internal.
10. Sign in through the real EliteSCADA UI as `EliteSCADA` using the protected Codespaces secret.

A clean Codespace should not require manual database edits, authentication bypasses, package surgery or lifecycle shortcuts.

## What the launcher validates

Before presenting the browser UI, the launcher:

- verifies the frozen package against the committed `.sha256`;
- verifies package provenance against project `eee-demo`, the accepted post-C25 product SHA and the frozen digest;
- starts Local Identity bootstrap only long enough to persist the administrative identity;
- restarts the long-lived API without the bootstrap password in its process environment;
- authenticates through `/api/auth/login` and requires the real `developer` role;
- creates First Project only when persistence is empty;
- inspects and Import-Previews the frozen package;
- applies it with the normal workspace-version header;
- performs Save -> Publish -> Activate if no Active revision exists;
- preserves an existing Active revision on normal launcher restart;
- verifies `/api/runtime/application` is project `eee-demo`, has the expected Active revision, startup screen, six screens, two popups, eight built-in Dynamos and one application Dynamo;
- verifies the official `Demo` licensing state;
- starts Vite on port `5173` while the API stays internal.

## Recovery levels

Use the smallest recovery that matches the change.

- **Browser reload:** frontend HMR-only changes while API/Web remain healthy.
- **Launcher restart:** backend/launcher change or dead local process. Use VS Code task `Launch C11 Test Preview`.
- **Rebuild Container:** devcontainer, Compose, machine-id mount, SDK/Node contract or post-create/post-attach changes.
- **Fresh Codespace:** stale identity/database state, ambiguous prior environment, or final clean homologation evidence.

Never use destructive Git cleanup as routine Codespace recovery.

## Diagnostics

If forwarded port `5173` returns 502, the forwarding proxy exists but the Web process is not ready. Inspect:

```bash
git rev-parse HEAD
dotnet --version
docker compose -f .devcontainer/docker-compose.yml ps
tail -n 160 .preview/api.log
tail -n 160 .preview/web.log
curl -I http://127.0.0.1:5080/health
curl -I http://127.0.0.1:5173/
```

If Local Identity login fails after changing the protected Preview password, prefer a fresh disposable Codespace/database rather than weakening password policy.

If machine identity is missing or invalid, fix the devcontainer environment. Do not add a licensing bypass.

## Product Owner homologation checklist

For the exact Preview head under acceptance, record:

1. exact running Git SHA;
2. `C11 Canonical Test Preview` workflow SUCCESS;
3. fresh/rebuilt Codespace starts from repository configuration without required manual surgery;
4. port 5173 opens and remains Private;
5. real login succeeds;
6. Engineering is usable;
7. canonical EEE Overview is the startup Runtime screen;
8. all six canonical screens are reachable and visually coherent;
9. P01/P02 popups and the application pump Dynamo render;
10. representative TAG values update;
11. alarms/events remain distinct and usable;
12. historian/trend behavior is usable;
13. Save -> Publish -> Activate behavior remains coherent if exercised;
14. Demo licensing is reported;
15. launcher restart preserves the same persisted Active project;
16. any defect is recorded against the exact running SHA before correction.

CI proves reproducibility. Product Owner browser homologation proves usability. Humanity has spent enough years learning that those are not the same thing.
