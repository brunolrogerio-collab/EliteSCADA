# Wave 08 — Visual Asset Storage and Revision Contract

Status: **LOCKED COORDINATOR-OWNED WAVE 08 CONTRACT**  
Date: 2026-08-28  
Wave: `GRAPHICAL-EDITOR-WAVE-08`

This document defines the coordinator-owned persistence, revision and package authority for raster image assets introduced by Wave 08. It complements `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md` and `docs/VISUAL-ASSETS-AND-IMAGES.md`.

## Decision

Visual image assets are first-class project Engineering entities whose metadata participates in canonical Engineering, while binary payloads are stored and transported as integrity-checked content-addressed blobs.

The canonical visual property reference remains unchanged:

`assetRef = null | { assetId }`

No filesystem path, arbitrary URL, data URL or base64 property value becomes project authority.

## Why payload is not embedded in Engineering JSON

The canonical Engineering JSON is frequently loaded, diffed, Previewed, saved and revisioned. Embedding full image payloads inside ordinary visual properties or metadata would make unrelated Engineering operations carry large binary content and would couple visual-property editing to binary transport.

The public JSON therefore owns asset **metadata and stable references**, not the raw raster bytes.

## Engineering Schema v13

Wave 08 advances canonical Engineering to Schema **v13** for first-class visual asset metadata.

A visual asset entity has, at minimum:

- stable `Id`;
- developer-facing `Key`/name;
- original filename for presentation/audit only;
- canonical media type;
- byte length;
- SHA-256 content hash;
- pixel width and height when deterministically available;
- optional description/metadata.

The payload hash is immutable content identity. Asset ID remains project/reference identity.

Historical Schema v1-v12 packages remain readable. v12 has no first-class visual assets and therefore migrates with an empty visual asset collection.

## Supported Wave 08 raster families

Required:

- JPEG/JPG — `image/jpeg`;
- PNG — `image/png`, preserving source bytes including alpha;
- BMP — `image/bmp`.

Validation uses file signatures/structure, not extension alone. Filename and extension are presentation metadata and never override detected media type.

Initial import limits are intentionally bounded:

- maximum one asset payload: **16 MiB**;
- maximum pixel dimension: **16384 × 16384**;
- metadata strings are bounded by normal Engineering validation rules.

These limits may be tuned later by a deliberate product decision, not bypassed per request.

## Working-state authority

Imported asset payloads live in an Engineering Working asset registry owned by the backend.

Asset import/removal/replacement:

- occurs under the existing Engineering Workspace mutation lock;
- requires the same expected Workspace version / CAS semantics as other Engineering mutation;
- marks Working dirty only after successful canonical mutation;
- is authorization-protected as `EngineeringModify` and auditable;
- cannot silently mutate Published or Active revisions.

The frontend receives a project-controlled asset catalog and stable content endpoint. It never reads the engineer workstation path after import.

## Content-addressed blob storage

Binary payloads are keyed by lowercase SHA-256 hex.

The PostgreSQL implementation stores:

1. deduplicated immutable asset blobs keyed by hash;
2. revision-to-asset links identifying the asset ID/hash set belonging to each immutable Engineering revision.

Metadata remains in the revision's canonical Engineering JSON. Revision links prove which exact payloads accompany that metadata.

Blob deduplication is an implementation optimization only. A revision never points to mutable bytes.

## Transactional revision rule

Saving an Engineering revision containing visual assets must atomically persist:

- the canonical Engineering JSON revision;
- every required blob not already present;
- the complete revision-to-asset link set.

A revision must not become visible as successfully saved if required asset persistence fails.

Stores that do not implement asset-aware persistence may continue to support asset-free projects, but must fail closed when asked to save a revision that contains asset payloads.

## Revision checkout / activation / restart

Before Preview/Apply of a stored revision containing visual assets, the persistence layer verifies:

- every canonical asset metadata entry has one revision payload link;
- linked payload exists;
- payload length matches metadata;
- payload SHA-256 matches metadata;
- no conflicting duplicate asset ID exists.

Only a fully validated asset snapshot may replace the Working asset registry for a successful revision Apply/checkout.

The Active Revision therefore remains reconstructible after process restart without the original source image file.

## Canonical JSON import/export

Canonical JSON export includes visual asset metadata and stable references, but not raster bytes.

A JSON import that introduces asset metadata whose referenced payload hash is unavailable in the current project asset store fails closed before Apply. It must not create a dangling image asset.

Metadata-only JSON remains useful for inspection, diffing and controlled updates when the required content already exists.

Full portable transfer of new binary assets uses `.escadapkg`.

## `.escadapkg` format v2

Wave 08 introduces project package format **v2**.

A v2 package contains:

- `manifest.json`;
- `engineering.json`;
- one content-addressed asset sidecar per required hash, under `assets/<sha256>`.

The manifest lists each file with:

- path;
- SHA-256;
- byte length;
- media type where applicable.

Package inspection verifies sidecar names, hashes, lengths, duplicates, unexpected files and the canonical Engineering asset metadata relationship before reporting the package as valid.

Package restore/Apply uses the existing protected Project Package CAS/Audit boundary.

Historical `.escadapkg` format v1 with only `manifest.json` + `engineering.json` remains accepted.

## Inspect/Preview must not mutate Working

Package inspect/Preview may validate in-memory sidecar payloads and expose them as an import validation context, but must not stage permanent blobs or alter Working merely because a user inspected a package.

Blob persistence and Working replacement happen only during an authorized Apply/restore after CAS succeeds.

## Asset content serving

Asset bytes are served through explicit EliteSCADA endpoints by stable asset ID and project/revision context as appropriate.

Rules:

- no local source path exposure;
- response media type comes from validated canonical metadata;
- immutable revision content may use strong cache validators based on SHA-256;
- missing/corrupt content returns an explicit failure, never an arbitrary external fallback;
- Runtime Active-revision content must not accidentally resolve against newer Working bytes.

Wave 08 only needs the Engineering/editor seam. Full Runtime Screen navigation/rendering remains later-wave scope, but the storage contract must already permit Active-revision-safe lookup.

## Image import safety

Raster input is untrusted.

Importer requirements:

- enforce payload and dimension limits before expensive processing where possible;
- validate PNG/JPEG/BMP signatures and bounded structural metadata;
- preserve original raster bytes after validation; do not silently transcode or flatten PNG alpha;
- reject malformed/truncated payloads;
- never execute embedded content;
- ignore untrusted metadata as application configuration;
- compute SHA-256 from the exact persisted bytes.

## Reference integrity

For built-in `core.image` objects:

- non-null `assetRef.assetId` must resolve to a canonical visual asset in the prospective Engineering model;
- deletion of a referenced asset is blocked by Preview unless references are removed in the same prospective change;
- unknown asset IDs fail closed;
- asset metadata does not get copied into `assetRef`.

## Scope boundary

This contract authorizes only the Wave 08 foundation required for practical image authoring and portability.

It does **not** authorize:

- arbitrary filesystem browsing as project authority;
- arbitrary remote URL images;
- SVG execution/content;
- image transcoding pipelines;
- CDN/object-storage product architecture;
- advanced asset library/version UI;
- Wave 09 navigation/Popup/Dynamo product semantics;
- Wave 10 animation/event functionality.

## Required coordinator validation

Before Wave 08 closes, tests must prove at least:

1. valid PNG/JPEG/BMP imports are signature-validated and hashed;
2. PNG bytes survive unchanged;
3. malformed/oversized/unsupported payloads fail closed;
4. asset metadata round-trips in Schema v13;
5. v12 remains compatible with an empty asset collection;
6. revision save/check-out preserves exact asset payload identity;
7. two revisions may reference different payload hashes for the same logical asset without mutating the older revision;
8. PostgreSQL blob/hash/revision links survive restart;
9. `.escadapkg` v1 remains readable;
10. `.escadapkg` v2 preserves assets and rejects tampered/missing/unexpected sidecars;
11. JSON import cannot create dangling new asset metadata;
12. `core.image.assetRef` resolves only stable project asset identity;
13. Working/Published/Active boundaries remain explicit;
14. existing Wave 06/07 security, Python and visual regressions remain green.
