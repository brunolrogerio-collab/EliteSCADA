# Wave 14 C15 — HMI Trend Multipen

## Scope

C15 introduces `core.trend` as a first-class canonical HMI visual object. The same object definition is authored in Engineering and rendered by the shared canonical renderer used by Screens, Popups and Runtime.

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

The object also declares normal `x`, `y`, `width`, `height`, `zIndex`, transform, opacity, tooltip, enabled, background, stroke, corner radius and shadow properties through the existing registry.

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

The public visual mutation seam intercepts `property.set` for the reserved structural key `pens` before the scalar property reducer. This follows the same architectural rule already used by other object-owned structural payloads: scalar registry remains scalar, object-specific geometry/data remains under the owning object contract.

## Engineering authoring

The object palette exposes Trend as registered content. Selecting a single Trend in either Screen or Popup Engineering shows a dedicated Pen editor inside the shared Property Inspector.

The Pen editor consumes the existing canonical Engineering TAG catalog. TAG identity is persisted from `TagValueReferenceEngineering.tagId`/`TagEngineering.id`, not inferred from a name or path. The path and unit are retained only as useful authoring/presentation information.

Editor copy for the C15 Pen controls is available in `pt-BR`, `en` and `es` through the locale already selected by the visual editor provider.

## Runtime historical contract

C15 uses only the protected Historical Query API:

`POST /api/historical/query`

A visible multipen Trend produces one `historian.samples` request with:

- relative time range anchored at `now`;
- `tag.id in (...)` over all visible canonical TAG ids;
- ascending `timestamp` ordering;
- bounded page size.

It does **not** use the legacy `/api/history/{tagId}` single-TAG viewer route.

Historian rows consume the public fields `tag.id`, `tag.path`, `timestamp`, `value` and `quality`. Samples are grouped by canonical TAG id.

### Modes

- `history`: one query for the configured relative window when the object/configuration changes;
- `live`: the same protected relative-window query is refreshed at `trendRefreshSeconds`;
- `combined`: intentionally not exposed by C15 because the current protected public contracts do not define a distinct historical/live splice contract. Adding a cosmetic combined mode before that contract exists would create a private runtime truth source.

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
- legend with latest value/unit;
- quality display and line gaps for bad-quality samples;
- loading, no-data and historian-unavailable states in `pt-BR`, `en` and `es`.

The object remains subject to normal canonical visibility, enabled state, geometry, stacking, transform, opacity, tooltip, background and border behavior.

## Lifecycle and persistence

No C15-only persistence path is introduced. Trend objects and their native `pens` array remain part of the ordinary Engineering package and therefore follow the existing lifecycle:

`Working -> Revision -> Published -> Active`

Runtime continues to consume the active public visual definition. Save/Publish/Activate authority is unchanged.

## Tests

C15 adds/updates browser-contract coverage for:

- registered built-in schema and palette presence;
- separation of scalar schema from native `pens` payload;
- Pen JSON round-trip and validation;
- canonical Pen mutation;
- one protected multipen Historical Query request;
- grouping historian rows by canonical TAG id and retaining quality.

Validation must follow `docs/CI-VALIDATION-POLICY.md`, including the Wave 11 Active HMI Runtime workflow required by the Wave 14 correction package.
