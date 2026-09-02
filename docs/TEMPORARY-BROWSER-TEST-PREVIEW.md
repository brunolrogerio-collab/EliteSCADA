# EliteSCADA — Temporary Browser Test Preview

**Status:** PLANNED / NEXT  
**Tracking issue:** #208  
**Ordering:** after accepted Wave 12 and before resuming Wave 13

## Purpose

Provide a temporary development/homologation environment where the real EliteSCADA can be started remotely and used from a browser without requiring a local product installation.

Target operator flow:

`Open Codespace / Launch Test Preview -> automatic environment startup -> temporary authenticated Web URL -> use the real EliteSCADA`

This environment is not a production deployment, permanent public hosting model, SLA-backed service or supported customer deployment target.

## Preferred architecture to evaluate

Use GitHub Codespaces as the orchestration surface, with repository-controlled automation for:

- the EliteSCADA .NET backend;
- the React frontend and pinned Pyodide static assets;
- PostgreSQL / TimescaleDB required by the application;
- idempotent database/application initialization;
- automatic import of a validated Demo `.escadapkg`;
- normal Save/Publish/Activate lifecycle so HMI Runtime derives from persisted Active Engineering;
- forwarding only the Web port needed by the browser;
- database and internal service ports kept private inside the Codespace.

The initial Demo fixture should preferably be `EliteSCADA-Wave11-Demo.escadapkg` after its current location, checksum and compatibility with the exact Preview source SHA are revalidated.

## Product behavior to preserve

The Preview must run the actual product, not a parallel mock implementation. Representative validation should cover the product surfaces available at the selected source SHA, including:

- local login and Administration;
- Engineering;
- Active HMI Runtime;
- simulated TAGs;
- alarms;
- trends;
- screens, popups, Dynamos and static assets;
- Python/Pyodide-dependent browser functionality;
- Demo/licensing behavior;
- temporary configuration and persistence expected inside the Codespace lifecycle.

Accepted authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

The Preview must not drive Runtime directly from mutable Working state.

## Administrative test account

A dedicated Preview administrative account is required with username:

`EliteSCADA`

The Development Lead supplied the desired password separately. Because this repository is public, that password must not be committed to source control, workflow YAML, devcontainer configuration, documentation, logs, artifacts, Docker image layers or `.escadapkg` fixtures.

Implementation should obtain the password from a protected Codespaces/GitHub secret or equivalent ephemeral environment variable, for example:

`ELITESCADA_PREVIEW_ADMIN_PASSWORD`

Bootstrap must fail clearly when the protected credential is absent. There must be no repository-embedded fallback password.

## Exposure and security constraints

- Prefer private/authenticated Codespaces forwarded-port visibility.
- Expose only the EliteSCADA Web port required by the browser.
- Keep PostgreSQL/TimescaleDB and internal service ports private unless an audited technical requirement proves otherwise.
- Do not weaken backend authentication/authorization for Preview convenience.
- Do not place Authenticode or licensing private signing material in the Preview environment.
- Do not claim production security, durability, uptime or recoverability.
- Deleting/stopping the Codespace is an acceptable cleanup boundary for this temporary environment.

## Automation target

Once the environment is stable, expose one short, repeatable operator path named approximately **Launch Test Preview**.

Implementation should audit existing product start/import/activation surfaces before adding new mechanisms. Likely repository surfaces include, but are not preselected as mandatory:

- `.devcontainer/devcontainer.json`;
- a Compose/devcontainer service for PostgreSQL/TimescaleDB;
- idempotent bootstrap and launch scripts;
- existing EliteSCADA APIs/services for application import and activation;
- Codespaces port metadata;
- concise operator documentation.

Do not create a second product architecture merely to fit Codespaces.

## Acceptance direction

From a known exact source SHA, a fresh Preview environment should demonstrate that:

1. a Codespace can be created from repository configuration;
2. required dependencies and database services initialize without local-machine setup;
3. the actual EliteSCADA backend and Web frontend start successfully;
4. database/internal service ports remain private;
5. the `EliteSCADA` administrative test account can authenticate using the protected injected password;
6. the validated Demo package is imported automatically, or any replacement fixture is explicitly documented and verified;
7. the application is activated through the normal persisted Engineering lifecycle;
8. the temporary Web URL opens the actual EliteSCADA UI;
9. representative Engineering, Runtime, simulated TAG, alarm and trend operations are usable from the browser;
10. Pyodide/static browser assets load correctly;
11. startup is repeatable enough to be documented as `Launch Test Preview`;
12. changes to product code remain subject to universal EliteSCADA CI.

## Relationship to release waves

Wave 12 remains COMPLETE / ACCEPTED / CLOSED and is not reopened by this requirement.

Wave 13 release-engineering work already exists in draft PR #207 but is paused. Its branch and audit evidence are preserved; no additional Wave 13 implementation or merge should proceed while issue #208 is the active coordination direction.

Current planned sequence:

`Wave 12 accepted -> Temporary Browser Test Preview (#208) -> resume Wave 13 (#205/#207) -> Wave 14 owner validation -> Wave 15 feedback/corrections`

## Explicit non-goals

- permanent or public EliteSCADA hosting;
- production deployment through GitHub Codespaces;
- production database persistence/SLA;
- replacing the Windows release/package track;
- reopening Wave 12 architecture without a demonstrated defect;
- unrelated HMI/Driver feature expansion;
- physical Driver L4 validation.
