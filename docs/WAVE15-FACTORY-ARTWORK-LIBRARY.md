# Original factory SVG review library

Engineering > Visual assets now includes a lazily loaded catalog of the 2,208 original
static SVG candidates already authored in `assets/sources/`. It supports category/style
filters, text search, 60-item pages and explicit Copy to project. Import uses the existing
canonical asset upload/CAS path; there is no parallel blob store or automatic Dynamo creation.
Imported assets can then be selected in Screen/Popup/Template SVG objects and animated with
normal semantic paint properties.

The API embeds source batches/artwork as immutable resources, maps only manifest identities,
never accepts filesystem paths, serves sanitized inert SVGs, and requires Workspace
Engineering read authority. Copy requires existing Engineering Modify authority and current
Workspace revision, as with any normal upload. Source/project/package geometry remains vector.

All batches remain draft review candidates. This feature does not assert that a human approved
every image, does not change the factory's approved-publication gate, and does not mark
third-party content as licensed. It exposes only original/generated EliteSCADA artwork with
explicit commercial redistribution permission from the repository manifests. Rejected,
deprecated and reference-only candidates are excluded. Original notice:
`assets/licenses/ELITESCADA-ORIGINAL-ASSET-NOTICE.md`.

Two focused tests prove manifest/resource completeness, categorization, truthful draft status,
unknown-ID rejection and canonical sanitization of every available preview. The web production
build passes. Mounted copy/insertion acceptance is still recorded separately from these checks.

External references remain Opto 22's SVG library and Wikimedia P&ID categories. Their graphics
are not relabelled as original assets or redistributed before source-specific license review.
