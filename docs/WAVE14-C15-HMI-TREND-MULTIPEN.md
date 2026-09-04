# Wave 14 C15 — HMI Trend Multipen

## Scope

C15 introduces `core.trend` as a first-class canonical HMI visual object. The same object definition is authored in Engineering and rendered by the shared canonical renderer used by Screens, Popups and Active Runtime.

Required base: `2607e03d5445eefe1f434495d0ee81136c6cd220`.

This package is intentionally isolated from C14 and does not depend on first-class operational events.

## Canonical object contract

`core.trend` reuses the shared C05 scalar Visual Property Registry for normal geometry, transform, visibility and presentation. Trend-specific scalar properties are:

- `trendMode`: `history | live`;
- `trendWindowSeconds`: 60..604800 seconds;
- `trendRefreshSeconds`: 1..3600 seconds;
- `trendLegendVisible`;
- `trendGridVisible`;
- `trendAxesVisible`;
- `trendQualityVisible`.

The object also declares normal `x`, `y`, `width`, `height`, `zIndex`, transform, opacity, tooltip, enabled, background, stroke, corner radius and shadow properties through the existing registry. Move, resize, selection, stacking, clipboard and composition therefore remain ordinary canonical visual operations rather than Trend-only behavior.

### Structured `pens` payload

`pens` is deliberately **not** registered as a scalar property and is never serialized into a JSON string. It remains native Engineering JSON owned by the Trend object contract.

Each Pen persists:

- stable Pen id;
- canonical `tagId`;
- human-readable `tagPath`;
- label;
- visible flag;
- engineering unit;
- color;
- line width;
- line style (`solid | dashed | dotted`);
- axis (`left | right`);
- scale (`auto` or fixed minimum/maximum).

The implementation validates stable identities, unique Pen ids, line width, color syntax and fixed-scale ranges. A Trend is bounded to 16 Pens.

The public visual mutation seam intercepts `property.set` for the reserved structural key `pens` before the scalar property reducer. This follows the same architectural rule already used by other object-owned structural payloads: scalar registry remains scalar, object-specific structured data remains under the owning object contract.

## Engineering authoring

The Object Palette exposes Trend as registered content. Selecting a single Trend in either Screen or Popup Engineering shows a dedicated Pen editor inside the shared Property Inspector.

The Pen editor consumes the existing canonical Engineering TAG catalog. TAG identity is persisted from `TagValueReferenceEngineering.tagId` / `TagEngineering.id`, not inferred from a display name or path. Path and unit remain authoring/presentation metadata.

Editor copy for the C15 Pen controls is available in `pt-BR`, `en` and `es` through the locale already selected by the visual editor provider.

Popup Engineering uses the existing `Popup -> visual Screen session -> Popup` adapter. C15 does not introduce a Popup-specific Trend schema or renderer.

## Runtime historical contract

History uses only the protected Historical Query API:

`POST /api/historical/query`

A visible multipen Trend produces one `historian.samples` request per Trend instance with:

- relative time range anchored at `now`;
- `tag.id in (...)` over all visible canonical TAG ids;
- ascending `timestamp` ordering;
- bounded page size.

It does **not** use the legacy `/api/history/{tagId}` single-TAG viewer route.

Historian rows consume the public fields `tag.id`, `tag.path`, `timestamp`, `value` and `quality`. Samples are grouped by canonical TAG id. `trendRefreshSeconds` reissues the protected relative-window query so a historical Trend can follow a moving `now` window without inventing a second history API.

## Runtime live contract

`live` mode consumes the existing canonical Runtime TAG transport already used by active visual bindings:

- protected readable snapshot: `GET /api/tags`;
- realtime transport: `/ws/tags` and `tagValueChanged` messages;
- `trendRefreshSeconds` controls snapshot fallback/reconciliation;
- live samples are retained only inside the configured `trendWindowSeconds` and bounded per Pen.

Live mode does **not** poll the Historian. A mounted Trend exposes `data-trend-source="runtime-tags"`; history exposes `data-trend-source="historian"` for deterministic browser acceptance coverage.

Quality follows the fail-safe Runtime convention: only `Good` / quality `0` samples are drawable. `Bad`, `Uncertain` or other non-good quality creates a trace gap regardless of whether quality text is visible. `trendQualityVisible` controls chrome only; hiding the label cannot silently turn bad data into valid process data.

### Combined mode

`combined` remains intentionally unexposed. C15 now has legitimate historical and realtime sources, but there is still no canonical public splice policy defining overlap precedence, timestamp reconciliation, duplicate suppression across Historian/Runtime boundaries and quality precedence. C15 does not create a private React-only interpretation of those rules merely to add an enum value. A future combined mode must first define that public contract.

## Rendering

The shared `CanonicalVisualRenderer` handles `core.trend`, therefore the same rendering path applies to:

- Engineering Screen preview;
- Engineering Popup preview;
- active Runtime Screens;
- active Runtime Popups.

The renderer provides:

- SVG multipen traces;
- individual Pen color, width and line style;
- automatic or fixed scale per Pen;
- left/right axis assignment metadata;
- grid and axes toggles;
- legend with latest usable value/unit;
- optional quality display with fail-safe gaps for non-good samples;
- loading, no-data and unavailable states in `pt-BR`, `en` and `es`.

The object remains subject to normal canonical visibility, enabled state, geometry, stacking, transform, opacity, tooltip, background and border behavior.

## Lifecycle and persistence

No C15-only persistence path is introduced. Trend objects and their native `pens` array remain part of the ordinary Engineering package and follow the existing lifecycle:

`Working -> Revision -> Published -> Active`

Runtime consumes the Active public visual definition. Save/Publish/Activate authority is unchanged.

Wave11 acceptance coverage adds a Trend to both a Screen and Popup, applies the Engineering package, saves a revision, publishes it, activates it, verifies the native Pen array in `/api/runtime/application`, and verifies the Screen Trend mounts through `CanonicalVisualRenderer` from the Active revision.

## Tests

C15 adds/updates browser-contract coverage for:

- registered built-in schema and Object Palette presence;
- separation of scalar schema from native `pens` payload;
- Pen JSON round-trip and validation;
- canonical Pen mutation;
- ordinary add/move/resize behavior and two independent Trend instances;
- Popup adapter round-trip using the same canonical Trend object;
- one protected multipen Historical Query request per Trend instance;
- grouping historian rows by canonical TAG id and retaining quality;
- mounted historian multipen rendering;
- mounted no-data localization;
- fail-safe quality gaps even when quality chrome is hidden;
- two mounted independent Trend instances;
- mounted live snapshot plus WebSocket update with no Historian polling;
- Save -> Publish -> Activate -> Active Runtime persistence for Screen and Popup.

## Validation policy / handoff

Validation follows `docs/CI-VALIDATION-POLICY.md`.

The repository workflows `EliteSCADA CI` and `Wave11 Active HMI Runtime` are configured for `workflow_dispatch` and/or `main`-scoped push/PR triggers. The isolated `wave14/c15-hmi-trend-multipen` branch therefore does not automatically create workflow runs. C15 must not claim those workflows passed unless a run exists for the exact delivery HEAD.

The Wave11 Playwright configuration contains an explicit `chromium-wave11-c15-trend` project between the existing lifecycle and owner-package projects so C15 lifecycle acceptance runs in deterministic order when the workflow is dispatched on the integrated candidate.

C18 may reuse the protected Historical Query API and its public dataset conventions. It should not depend on Trend-specific Pen payloads or private renderer state.
