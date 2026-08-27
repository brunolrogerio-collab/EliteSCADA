# EliteSCADA Visual Assets and Image Resources

Status: **LOCKED v0.1 PRODUCT REQUIREMENT**  
Approved by product owner: 2026-08-27.

This document defines the image/resource behavior required by the graphical Engineering and Runtime HMI portions of EliteSCADA v0.1.

It complements `PROJECT GOAL.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md` and `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

## Product requirement

The graphical Engineering environment must allow an engineer to import ordinary image files into the project and use them as visual resources in Screens, Popups and Dynamos.

Representative uses include:

- company/customer logos;
- photographs of a plant, machine, panel or installation;
- process illustrations;
- equipment images;
- backgrounds;
- symbols and icons;
- transparent overlays;
- decorative or explanatory images required by the HMI.

Image support is part of the v0.1 Full Product Validation Preview. The first owner-facing graphical editor is not complete if images can only be referenced through developer filesystem paths or manually edited JSON.

## Required import formats for v0.1

At minimum the Engineering asset importer must accept:

- JPEG / JPG (`.jpg`, `.jpeg`);
- PNG (`.png`), including PNG with alpha-channel transparency;
- BMP (`.bmp`).

The implementation should also accept other common browser/runtime-safe raster formats when practical, for example WebP, provided support is deterministic and does not weaken packaging, validation or portability.

Additional formats may be added later. SVG may be considered separately because active/vector content has different security and rendering implications; it is not required merely by this raster-image requirement.

## Transparency

Transparency support is mandatory where the source format provides it.

In particular:

- PNG alpha transparency must be preserved during import, project save/export/package, Engineering preview and Runtime rendering;
- transparent pixels must remain transparent rather than being flattened against an arbitrary background;
- object-level opacity may be applied in addition to the image's own alpha channel;
- Runtime rendering must remain visually consistent with Engineering preview.

## Canonical asset model

Imported images are Engineering project assets/resources, not loose filesystem dependencies.

Each imported asset must have stable project identity and metadata sufficient to support at least:

- stable asset ID;
- developer-visible name;
- original filename where useful for presentation/audit;
- media/MIME type;
- byte size;
- pixel width/height when available;
- content integrity/hash metadata where appropriate;
- optional description/metadata;
- payload/reference needed by the versioned project/package model.

Screens, Popups, Dynamos and image-capable visual objects reference the stable asset identity rather than an arbitrary absolute path such as `C:\...\logo.png`.

Renaming or moving the original source file on the Engineering workstation after successful import must not break an already imported project asset.

## Import workflow

A normal engineer must be able to:

1. open the project asset/resource interface or an image-object property;
2. choose/import an image file;
3. validate the file type and size;
4. see an Engineering preview/thumbnail;
5. give the resource a useful project name if desired;
6. place/use the resource on a Screen, Popup or Dynamo;
7. save/reopen the project without losing the resource;
8. publish/activate the project;
9. see the same resource in Runtime.

The UI should make imported assets reusable so the same logo/image does not need to be embedded repeatedly for each visual object.

## Image visual object behavior

The basic graphical editor must provide an image-capable object whose source is a project asset reference.

At minimum it must support, through the shared visual-property model where applicable:

- x/y position;
- width/height;
- visibility;
- opacity;
- z-order;
- rotation when supported by the common visual object model;
- fit/stretch behavior appropriate for an HMI image object;
- preserve-aspect-ratio behavior or an equivalent explicit fit mode.

Useful fit modes may include concepts equivalent to contain, cover, fill/stretch and original/native sizing. Exact public property keys are defined when the Visual Property Registry is stabilized.

The editor must not invent an image-only private geometry/property system that conflicts with the common visual property registry.

## Engineering authority and portability

Image/resource content participates in normal project portability.

Required behavior:

- `.escadapkg` backup/restore preserves imported assets;
- canonical project export/import preserves asset identity/references according to the package/export design;
- Screens/Popups/Dynamos referring to assets survive save/revision/publish/activate cycles;
- duplicate/reference handling is deterministic;
- missing/corrupt assets produce explicit validation diagnostics rather than silent blank objects;
- cross-project copy/import must eventually preserve or reconcile asset dependencies through the Engineering Fragment/dependency model.

The exact binary representation inside canonical JSON versus package sidecar/content-addressed storage is an implementation decision. Large binary resources should not force an inefficient design merely to say they are "inside JSON". The authoritative requirement is that assets belong to the versioned project/package model and remain portable, referentially stable and integrity-checked.

## Security and validation

Imported image files are untrusted input.

The implementation must:

- validate supported media types using file content/signature where practical, not filename extension alone;
- reject malformed or unsupported image data cleanly;
- apply reasonable configurable or product-defined size/dimension limits to prevent memory/resource abuse;
- never execute embedded code from an imported raster image;
- avoid exposing arbitrary local filesystem paths to Runtime clients;
- serve/project assets through the normal EliteSCADA application boundary;
- ensure authorization for Engineering asset modification follows normal Engineering permissions/Audit rules.

Metadata parsing must be bounded. The product should not trust arbitrary EXIF or embedded profile data as executable or authoritative application configuration.

## Runtime behavior

Runtime clients must render Active Revision image assets without depending on the Engineering workstation's original file paths.

Resource loading must support:

- deterministic asset lookup by stable reference;
- normal browser caching/versioning without stale assets leaking across Active Revision changes;
- graceful explicit diagnostics/fallback for missing/corrupt resources;
- disposal/release behavior appropriate for Screen/Popup/Dynamo lifecycle;
- multiple Runtime clients using the same project asset consistently.

Different Runtime clients may have different visual runtime overrides such as object visibility/opacity, but the underlying Active Revision asset remains project-authoritative.

## Python relationship

Client Visual Python may interact with image objects only through the normal public Visual Object API and declared visual properties.

Scripts may, where the stabilized property schema permits:

- show/hide an image object;
- change opacity, geometry or other runtime-writable visual properties;
- potentially select among already authorized/project-defined asset references if a future explicit property allows it.

Scripts do not receive arbitrary filesystem access or unrestricted URL/network loading as a side effect of image support.

## Required v0.1 validation scenarios

Before the v0.1 owner-facing package is accepted, automated and/or product acceptance evidence must include at least:

1. import a JPG logo/photo and render it in Engineering and Runtime;
2. import a BMP and render it correctly;
3. import a PNG with alpha transparency and prove the transparent background remains transparent over another HMI object/background;
4. resize and reposition an image object;
5. save/reopen and preserve the asset/reference;
6. publish/activate and render the same asset in Runtime;
7. `.escadapkg` backup/restore preserves the asset;
8. export/import or equivalent canonical portability path preserves the asset/reference;
9. missing/corrupt image resource produces explicit validation/runtime diagnostics;
10. reopening/restarting EliteSCADA does not require the original external image file to still exist;
11. multiple objects can reuse one imported project asset;
12. object opacity works without destroying source PNG alpha transparency.

## Wave placement

The contract is established now. Product implementation belongs primarily to the visual block:

- **Wave 07**: Visual Property Registry/runtime contract must include asset/image reference semantics where needed;
- **Wave 08**: graphical editor must include asset import/resource selection and the basic Image object;
- **Wave 09**: Screens/Popups/Dynamos must preserve/reuse asset dependencies;
- **Wave 11**: the complete Demo must exercise real imported visual assets such as a logo and/or plant/equipment imagery;
- **Wave 12**: hardening tests malformed, large, missing, transparent and portability/restart cases;
- **Wave 13**: Windows package must include Active/Demo project resources without external developer paths.

This feature is therefore not postponed to the advanced visual-library phase.
