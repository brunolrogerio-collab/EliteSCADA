# Wave 15 Media Source Core

Issue: #495 (`C-MEDIA-SOURCE-CORE-01`)

## Current foundation

Engineering schema v21 adds the optional `mediaSources` collection. Schema v20 and older
packages normalize a missing collection to empty. A media source currently has stable
Engineering identity, a key/name, an enabled flag, a typed protocol (`http`, `hls`,
`mjpeg`, `rtsp`) and a portable endpoint. It participates in preview/apply, project
Engineering JSON export/import and Workspace dirty/version tracking.

The endpoint validator requires an absolute URI whose scheme matches the selected
protocol. It rejects URI user-info, query strings, fragments, malformed identifiers and
oversized endpoints. These restrictions keep passwords/tokens out of project packages and
avoid browser-facing credential URLs. The portable DTO intentionally has no credential field.

The `/api/engineering/media-sources` collection can be listed, created, updated and deleted
through Workspace-authorized endpoints. Writes require `EngineeringModify` and the current
`x-elitescada-workspace-version`; reads use the existing Workspace Engineering read guard.
This API edits configuration only. It does not probe or proxy the target URL. A write-only
credential lifecycle is now connected to `IProtectedMaterialAuthority`: Basic credentials or a
Bearer token are stored as an encrypted payload, while a host-only sidecar under a dedicated
subdirectory persists the opaque reference keyed by project owner and stable source ID. Keeping
the index out of the ciphertext envelope root preserves the authority's health scan. The public
credential-state endpoint returns only configured/not-configured status. Delete/replacement
updates the reference mapping
without exposing secret bytes or host references in the DTO/package. Trusted server code can
resolve a zeroing lease through `MediaSourceProtectedCredentialService`; no player/relay consumes
it yet.

Engineering now has a Media Sources workspace for source CRUD plus Basic/Bearer credential
provisioning. It shows only configured/not-configured status, clears entered credentials after
submission, and explicitly distinguishes Working changes from Publish/Activate. A mounted
Chromium flow creates a temporary MJPEG source, provisions a Bearer credential with an ephemeral
E2E-only protection key/store, proves the token is absent from Engineering export, then removes
the source and temporary store. This is configuration/secret-lifecycle coverage, not stream
playback or endpoint reachability evidence.

The existing content-addressed VisualAsset authority now also recognizes bounded PDF,
MP4 and WebM uploads by file signature (PDF header/EOF, ISO-BMFF `ftyp`, WebM EBML),
preserving the existing hash/package/restore path rather than introducing a second blob
store. Images remain capped at 16 MiB, PDFs at 32 MiB and video containers at 64 MiB;
these media payloads remain inert resources in Engineering. Initial local-asset Video and PDF
visual objects now consume them from the canonical renderer; these objects do not consume
network Media Sources or implement IP-camera playback. PDF content responses receive
`nosniff` and a restrictive sandbox CSP.

Focused local verification: media/asset contract tests and PDF/MP4/WebM project-package
round-trips pass; `Scada.Api` Release build passes with 0 warnings / 0 errors. Earlier
credential-lifecycle tests passed 12/12, including configure, replace, trusted resolve, delete,
cross-project isolation and audit non-disclosure. No HTTP authorization integration or live
camera connection is claimed. This is not the #495 acceptance gate; network probes and RTSP
relay remain incomplete.

The local L3 lab uses host-network BACnet health on port 18081 so it cannot collide with the
stable Docker Runtime mapped to 18080. The corresponding L3 workflow health checks use
18081 as well. Local isolated-Linux L3 evidence is currently partitioned by test profile:

- seven-driver activation/acquisition and the S7-to-CIP gateway runtime passed (1/1);
- seven-driver first-version writes passed (1/1) after resetting the dedicated CIP simulator
  to remove state left by an earlier run;
- serial peer-loss/recovery across the seven-driver runtime passed (1/1).

These are successful local profile runs, not a claim that the exact GitHub workflow or exact-head
T1 has passed. The Windows-host attempt could not start because this checkout does not contain
the Windows `EliteScada.Dnp3Host.exe`; that is a test-host prerequisite, not a driver assertion.
The stable web/API/PostgreSQL containers were not modified; all resets were limited to the
dedicated L3 simulators.

## Security boundary

An endpoint URI is configuration, not permission to fetch it. Any future probe, player or
relay must enforce destination policy at connection time (including DNS/IP resolution and
redirect handling), bound response size/time/concurrency, and prevent access to loopback,
link-local and metadata-service destinations unless an explicit deployment policy allows
them. RTSP playback must go through a server-side browser-compatible relay; never expose
camera credentials or direct RTSP URLs to the browser.

Protected credentials use `IProtectedMaterialAuthority` with owner
`project:<project-key>`, resource kind `MediaSource`, stable source identity and purpose
`ConnectionCredential`; the protected payload distinguishes Basic credentials from Bearer
tokens. Persisted host references must be kept separate from
portable project DTOs. Package export may carry dependency/configured-state only; imported
hosts must require credential reprovisioning. The existing protected-material foundation
and its portability rules are documented in `WAVE15-PROTECTED-MATERIAL-AUTHORITY.md`.

## Still required for #495

- consume the protected-credential service from the future media connection/relay and export a
  portable dependency descriptor so cross-host imports report `CREDENTIAL_REQUIRED`;
- add mounted API authorization/content tests for Media Source and media payload routes;
- implement controlled HTTP(S), HLS and MJPEG readiness/probe behavior;
- implement an RTSP relay with a pinned, licensed deployment dependency and health/readiness;
- add cancellation, reconnect/backoff and per-project/source concurrency limits;
- sanitize diagnostics and cover auth/offline/unsupported/fault-recovery scenarios;
- prove full project/package portability and run focused tests plus exact-head T1 after the
  feature is complete.

The local-asset Video/PDF editor and renderer slice is present in this working tree for #496.
It still needs mounted Screen/Popup authoring and Runtime coverage, and the #495 network/IP-camera
consumer before #496 can be accepted. The asset picker and upload accept filters are already
scoped to the selected Video/PDF/Image object in this working tree.

## Runtime mobile slice (#503)

Runtime presentation now carries a validated `mobileOrientation` (`landscape` by default,
`portrait` optional) through the Engineering package. On a coarse-pointer device held in
portrait, the landscape mode rotates the fixed logical canvas and fits it without changing its
aspect ratio; desktop layout behavior is unchanged. The orientation setting is now in a
dedicated Mobile Engineering section, separate from Branding. Its mounted Chromium test
validates a changed orientation through Preview and verifies that Preview alone leaves the
Workspace revision and saved project unchanged. There is still no per-screen mobile variant
mapping, custom mobile header, or app-install decision in this slice.

Focused verification on this checkout: the production web build passes; the mounted Chromium
mobile viewport test passes (1/1), and the Mobile Engineering Preview flow passes (1/1) using
the isolated local E2E TimescaleDB on port 25432; the
stable runtime/API/database containers are untouched. Media signature/package tests pass 31/31
and the API Release build passes with no warnings/errors. No GitHub Actions run was started.
