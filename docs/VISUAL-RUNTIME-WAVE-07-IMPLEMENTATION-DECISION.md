# Wave 07 — Visual Runtime Object Model Implementation Decision

Status: **LOCKED WAVE 07 IMPLEMENTATION CONTRACT**  
Date: 2026-08-28  
Product base: `cc79713434c1d7b5988158b843b137eaf488d923`

This document turns the locked product architecture in `PROJECT GOAL.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md` and `docs/VISUAL-ASSETS-AND-IMAGES.md` into the executable Wave 07 boundary.

Wave 07 is architecture-first. It establishes the public client visual runtime model consumed later by the graphical editor, Screens, Popups, Dynamos and Python visual APIs. It does **not** implement the graphical editor itself.

## Wave objective

Deliver one deterministic visual runtime foundation with:

1. stable visual object definition identity;
2. a typed public Visual Property Registry;
3. per-client Runtime Visual Instance identity and lifecycle;
4. deterministic property-source resolution;
5. runtime override isolation from saved Engineering;
6. stable project Asset/Resource reference semantics;
7. a renderer-independent property API that Python can later consume without DOM authority.

Locked precedence:

`Animation > Script > Binding/Expression > Engineering Base`

## Non-goals

Wave 07 does not implement:

- graphical canvas/editor;
- Screen/Popup/Dynamo authoring UI;
- asset file importer or binary persistence;
- image renderer/object palette;
- production animation scheduler/tween engine;
- final Python visual API spelling;
- Server Python;
- new industrial protocols;
- direct DOM access from Python;
- arbitrary URL/filesystem assets.

These remain later waves.

## Visual property type system

The first registry must support a small explicit serializable type family sufficient for the common v0.1 visual contract:

- `number`;
- `boolean`;
- `string`;
- `color` using a stable serialized color value;
- `enum` with an explicit allowed-value set;
- `assetRef` using a stable project asset identity.

The implementation may use TypeScript discriminated unions or equivalent internal representation, but consumers must not infer types from default JavaScript values.

No `any`-shaped arbitrary renderer property bag becomes public authority.

## VisualPropertyDefinition

Every public property definition exposes at least:

- stable `key`;
- declared `type`;
- typed `defaultValue`;
- optional numeric min/max constraints;
- optional enum allowed values;
- `engineeringEditable`;
- `runtimeReadable`;
- `runtimeWritable`;
- `supportsBinding`;
- `animatable`;
- optional presentation/unit metadata that does not alter semantics.

Validation is centralized through the registry. The graphical Property Inspector, future Engineering projection and Python visual property API must consume the same definitions.

## Initial common property registry

Wave 07 must establish stable common keys for at least:

Geometry/layout:
- `x`
- `y`
- `width`
- `height`
- `rotation`
- `scaleX`
- `scaleY`
- `zIndex`

Visibility/appearance:
- `visible`
- `opacity`
- `fillColor`
- `strokeColor`
- `strokeWidth`
- `cornerRadius`

Text-capable foundation:
- `text`
- `textColor`
- `fontSize`

Image/resource foundation:
- `assetRef`
- `imageFit`

`imageFit` must be an explicit enum compatible with later image behavior, initially covering concepts equivalent to `contain`, `cover`, `fill` and `native`.

Defaults and constraints must be deterministic. At minimum, opacity is bounded and geometry must reject non-finite numeric values.

## Visual object definition identity

Wave 07 runtime contracts model an Engineering-owned visual definition without inventing a private renderer truth.

A definition projection requires at least:

- stable `objectId`;
- developer-visible stable `key` within its visual scope;
- stable `objectType` key;
- optional parent object identity;
- design/base property values keyed only by registered property keys;
- optional binding/expression descriptors or resolved binding inputs behind an explicit boundary;
- script/event references only through stable IDs/entry-point references when present;
- metadata treated as metadata, not runtime authority.

Because full Screen/Popup/Dynamo canonical persistence is implemented later, Wave 07 may use a frontend/public projection interface. It must remain intentionally mappable to the canonical Engineering model and may not become a second persisted project format.

## RuntimeVisualInstance

A runtime visual instance is client-local presentation state created from one visual definition.

Each instance has at least:

- unique `runtimeInstanceId`;
- source `objectId`;
- source object `key` and `objectType`;
- optional owning visual-context/parent instance identity;
- immutable Engineering base snapshot for the instance lifetime;
- independent binding layer;
- independent script-override layer;
- independent animation-override layer;
- disposed/not-disposed lifecycle state.

Two runtime clients or two instances of the same reusable definition may hold different runtime presentation values without changing Engineering.

Closing/disposing an instance clears subscriptions/runtime overrides and prevents further writes through that disposed instance.

## Property resolution

For each registered property, effective value resolution is deterministic:

1. use Animation override when present;
2. otherwise use Script override when present;
3. otherwise use Binding/Expression value when present and valid;
4. otherwise use Engineering Base value;
5. otherwise use the property registry default.

The runtime must be able to report the active source of the effective value as one of:

- `animation`;
- `script`;
- `binding`;
- `engineering`;
- `default`.

Invalid layer values fail closed for that layer and must not corrupt the lower authoritative value.

Timing accidents must not determine writer precedence.

## Runtime property API

The runtime instance boundary must support explicit operations equivalent to:

- read effective property value;
- read effective value plus active source;
- apply/clear a binding value;
- set/clear a Script override only when the property is `runtimeWritable`;
- set/clear an Animation override only when the property is `animatable`;
- clear runtime layers on disposal;
- query disposed state.

The implementation names may differ, but semantics are fixed.

Script writes do not modify the Engineering base snapshot. There is no implicit "save this runtime value" behavior.

## AssetReference

Wave 07 defines reference semantics, not the binary importer.

A visual asset reference is a stable project-owned identifier, never a filesystem path or arbitrary URL.

The canonical Wave 07 public reference shape is deliberately narrow:

`assetRef = null | { assetId }`

Only the stable project asset ID travels in a visual property reference. Developer-facing name, original filename, media/MIME type, dimensions, hash and other descriptive metadata belong to the future first-class project asset entity and must not be duplicated into `assetRef` as competing authority.

Wave 07 property validation must therefore accept/reject asset references structurally without loading files and reject additional path/URL/metadata fields on the reference object.

Required v0.1 raster families remain JPG/JPEG, PNG including alpha, and BMP. Their actual import, storage, preview and Runtime rendering are later-wave responsibilities.

## Python boundary preparation

Wave 07 does not expose direct renderer or DOM handles.

The future Client Visual Python bridge must be able to map explicit capabilities onto the runtime instance API:

- locate permitted object instance by stable ID/key within current visual context;
- read a registered runtime-readable property;
- set/clear a registered runtime-writable Script override;
- request animation only through an explicit animation capability later.

No JavaScript object, React component, DOM node, CSS selector, storage API or renderer-private object shape becomes part of the public Python surface.

## Diagnostics

Property/runtime diagnostics must be structurally available for later UI exposure:

- property key;
- effective value;
- active source;
- validation failure reason where applicable;
- runtime instance identity;
- disposed state.

Do not require translated human text inside the core model. Stable diagnostic codes/details may be localized by UI later.

## Worker ownership

### DEV 1 — Visual Property Registry / Engineering projection

Owns registry/type/validation/projection logic and focused tests within its allowed files. Does not implement runtime instance layering or central shell composition.

### DEV 2 — Runtime Visual Instance

Owns per-instance state/lifecycle/property-layer resolution using the locked registry contract. Does not modify the registry's public semantics or Engineering central composition.

### DEV 3 — Python ↔ Visual acceptance contract

Owns contract/adversarial acceptance proving only registered readable/writable capabilities are exposed and runtime overrides remain client-instance-local. Under temporary CI deferral, tests are written but not run through GitHub Actions until owner reset.

### Coordinator

Owns cross-slice contract reconciliation, shared exports/composition, canonical Engineering decisions, integration branch and final validation/merge.

## Temporary CI rule

Wave 07 is currently **DEVELOPMENT ACTIVE / CI_DEFERRED** because the owner reported approximately 19 included GitHub Actions minutes remaining.

Until the owner explicitly reports reset:

- do not open Wave 07 PRs because `pull_request` triggers the full CI workflow;
- do not manually dispatch Wave 07 Actions;
- branches may receive implementation commits;
- tests must still be written;
- delivery is `IMPLEMENTED / CI_DEFERRED`, never fully validated or merge-ready.

After reset, normal worker/integration validation resumes and the Wave Definition of Done remains unchanged.

## Wave 07 final gate after CI reset

The final integrated product must prove:

`definition -> registered base properties -> runtime instance -> binding layer -> script override -> animation override -> deterministic effective source -> disposal -> recreated deterministic instance`

and must prove that:

- runtime writes never mutate Engineering;
- invalid/unregistered/non-writable properties fail closed;
- asset references cannot become arbitrary paths/URLs;
- Python-facing capability cannot recover DOM/renderer authority;
- multiple instances remain isolated;
- final Web + backend/full tests + Runtime smoke + Chromium + Wave-specific acceptance are green before merge.
