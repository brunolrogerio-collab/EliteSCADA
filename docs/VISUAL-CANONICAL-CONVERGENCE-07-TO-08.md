# EliteSCADA — Visual Canonical Convergence 07 -> 08

Status: **COORDINATOR-ONLY READINESS / CI_DEFERRED**  
Date: 2026-08-28

This document defines the safe work allowed while Wave 07 waits for final GitHub Actions validation and before Wave 08 graphical-editor implementation is authorized.

## Decision

Do **not** absorb the full Wave 08 graphical editor into Wave 07.

Use the no-Actions interval for a coordinator-only **canonical visual convergence and Wave 08 readiness pass**. DEV 1, DEV 2 and DEV 3 remain stopped.

This phase may correct indispensable architecture/integration gaps required for Wave 07 and Wave 08 to share one canonical visual model. It does not authorize canvas/editor/product UI work.

## Why this phase exists

Repository review found that the frontend Wave 07 runtime foundation and the existing canonical/backend visual contracts are not yet one model:

1. canonical `VisualElementEngineeringDto.Properties` is currently `Dictionary<string,string>`, while the Wave 07 registry requires typed number/boolean/string/color/enum/assetRef values;
2. canonical visual elements have `Key`/`Type` but no stable `VisualObjectId`, while Script visual-event references already require a stable `VisualObjectId`;
3. visual bindings do not yet expose an unambiguous canonical target visual property compatible with the Wave 07 `propertyKey` model;
4. existing C# `VisualPropertyFoundation` predates the Wave 07 contract and diverges in names/semantics (`imageResourceId`, `resource:none`, `imageFit=none`, default materialization and a separate value-kind model);
5. frontend Engineering view types still model Screen/Popup `elements` as `unknown[]`;
6. the project has a locked visual-asset requirement but the canonical Engineering package does not yet contain a first-class visual asset/resource collection suitable for stable `assetRef` resolution.

Starting Canvas/Property Inspector before resolving these gaps would force editor-private authority, which is forbidden by `PROJECT GOAL.md`.

## Safe work now

The coordinator may:

- review the complete Wave 05/06/07 integration path and current `main` for duplicate/conflicting visual contracts;
- define the canonical typed visual property representation and migration strategy;
- define stable visual definition/object identity semantics shared by Screens, Popups, Dynamos, Scripts and Runtime;
- define canonical visual-property binding target semantics;
- define first-class visual asset identity/metadata semantics required by `assetRef`;
- align or deprecate the older C# VisualScripting property/runtime foundation where necessary;
- add adapters/contracts/tests required to prove frontend/backend parity;
- update documentation and handoff state;
- implement small, clearly indispensable convergence corrections on `integration/visual-runtime-wave-07` when they can be reasoned about safely without Actions.

Any functional code written during this interval remains `CI_DEFERRED` and cannot be described as validated or merge-ready.

## Not authorized now

Do not implement:

- graphical Canvas;
- zoom/pan/grid/snap UI;
- selection/multiselect handles;
- drag/resize/rotation UI;
- Property Inspector UI;
- Object Palette UI;
- Screen editor route/workspace;
- image file picker/import UI;
- binary asset persistence/serving;
- production visual renderer;
- production animation/tween scheduler;
- Wave 09 Screen/Popup/Dynamo navigation/authoring behavior;
- Wave 10 event editor/animation/preview.

## Canonical convergence target

Before Wave 08 workers are activated, the coordinator should have one deliberate answer for the following.

### Typed Engineering visual properties

Canonical Engineering must preserve JSON-native typed values rather than stringifying visual state. The preferred direction is a property bag whose values preserve JSON type, validated against the public Visual Property Registry.

The representation must support at least:

- finite number;
- boolean;
- string;
- color string;
- enum string;
- null or stable project `assetRef` object.

The registry default remains distinct from an explicitly engineered base value.

### Stable visual identity

Every canonical visual object must have stable identity independent from its developer-facing key. Script references, Runtime instances, editor selection and import/export must resolve through stable identity.

Screen/Popup/Dynamo definitions retain their own stable definition IDs. Nested visual objects require their own stable object IDs.

### Binding target

A visual binding must identify the **visual property being driven** separately from the source TAG/property/expression reference. No editor may infer this from array position or a renderer-private convention.

### AssetReference

`assetRef` is either null or a stable project asset reference. Arbitrary filesystem paths and URLs remain invalid.

The canonical project must eventually contain first-class visual asset identity/metadata and package payload semantics. During this readiness phase metadata/identity contracts may be established, but binary import/storage/rendering remains Wave 08 implementation work.

### Frontend/backend parity

C# and TypeScript must not maintain conflicting public property names/defaults/constraints. A parity mechanism is required before graphical editor work. This may be a shared contract descriptor/fixture or another deliberate source-of-truth mechanism, but not informal duplication.

## Wave 07 final validation boundary

Wave 07 still requires its deferred exact-head CI before merge. This readiness phase does not waive or replace:

- Web build;
- backend Release/full PostgreSQL tests;
- Runtime smoke;
- Chromium;
- Wave-specific visual/Python acceptance.

If convergence code materially expands the Wave 07 product head, the final CI must validate that exact final head.

## Wave 08 Definition of Ready

Wave 08 may be activated only when:

1. Wave 07 final integration is green and merged;
2. canonical typed visual property persistence is settled;
3. stable visual definition/object identity is settled;
4. visual-property binding target semantics are settled;
5. frontend/backend property registry parity is settled;
6. asset reference identity contract is settled sufficiently for the Image object/import slice;
7. the Engineering editor can consume canonical visual definitions without inventing a private persisted representation;
8. worker scopes and reserved central files are frozen.

## Stop condition during no-Actions interval

Continue only while review exposes a concrete integration defect, ambiguous contract or clearly indispensable dependency. Once remaining work would require significant UI/runtime implementation that cannot be usefully validated without CI, stop and wait for Actions reset rather than creating a large unvalidated branch.