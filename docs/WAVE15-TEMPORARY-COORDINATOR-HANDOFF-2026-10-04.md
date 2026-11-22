# Wave 15 consolidation handoff

## Product Owner acceptance / final integration delta — 2026-10-06

This delta supersedes earlier remaining-sequence wording below. PR #529 merged at `25df20bf562acf6c3d78eb2e953f7010729b7c49`; its T1 passed, and exact-SHA post-merge CI #37494810971 completed successfully (Web, Backend build/test/runtime smoke and Chromium integration smoke). Full-browser E2E was skipped by workflow conditions. Product Owner explicitly accepted and closed #484, #501 and #308: the first-party SVG factory/catalog is sufficient without third-party artwork curation, the current Dynamo set is usable, and additional visual-layer polish is future work outside the Wave 15 gate. #482/#496/#500/#503/#445 are also closed. Remaining productization is tracked by #306/#300/#379/#424/#425. The sole open PR #362 is a separate Preview-workbench lane.

Exact current mutable state is in root `LAST CHANGE.md`; GitHub remains authoritative.

The next coordinator's owner-directed assignment is to initiate Home/Building automation drivers using #472/#475. #472's old `WAIT_CURRENT_LANES / NO_IMPLEMENTATION_BRANCH` state is superseded. Revalidate/reuse #469 and close/revise the still-open #483 canonical contract/catalog prerequisites on the current base before protocol-specific code; review #475 GO/WAIT/REJECT and choose a bounded first driver slice. Keep #306/#300/#379/#424/#425 visible as separate Wave 15 closeout work.

Updated 2026-10-05. The product is consolidated in `wave15/corrections-integration`, including #508–#517 after product consolidation #502. This replaces conflicting earlier development checkpoints. The final HA/database/security and CI-repair package follows #517; its final merge/gate evidence is appended below.

**Owner's current scope:** finish and integrate the non-Dynamo product. Dynamo visual quality was rejected and its redesign is deferred. The categorized/linked static SVG library was NOT deferred: the owner specifically reported missing library-category and editor insertion access. This is corrected by the follow-up below, without claiming artwork approval or redesigning the Dynamos. Existing 26 definitions and the conversion/composition infrastructure are preserved; the obsolete 72 remain purged.

The mobile presentation also intercepts privileged Engineering/administration bookmarks: authorized Runtime users see the same Runtime canvas, without mounting desktop authoring or the second global header. Five mounted browser contracts pass for desktop, fullscreen, portrait-held mobile and both privileged mobile bookmarks. The media route explicitly resolves optional persistence from services and reports offline/503 on hosts without persistence, rather than failing endpoint construction.

The owner authorized integration and prioritized development over repeated testing. Do not repeat the already accepted driver/L3 work without a new regression. Run the final integrated post-merge CI only after the remaining product scope is integrated.

## Integrated product

- #502 now includes #453/#454 HA/database core, #455/#456 HA/database UI, #439 Help, and the 26 replacement Dynamo catalog. Those PRs are merged.
- Database administration is an Engineering entry beside High Availability, not a large global header title. The obsolete Area Atual header element is absent.
- Database administration now also retains Engineering navigation when opened directly and uses the common light/dark theme tokens. It is not an isolated administration page without project context.
- Developer receives HA Observe and Transfer capabilities by product default (fresh Authority/bootstrap/first project), not merely a Docker role edit. HA Admin is separate. An unlicensed user can inspect the full topology/configuration; save, apply and operations require a valid HA entitlement on both UI and API.
- Authority grant changes refresh Runtime authorization immediately. Saved packages bind to stable role/scope identities rather than freezing mutable grant versions; changed identities still fail closed. The role editor sends canonical string capability IDs rather than numeric enum values.
- Reporting V3 (#498), editable SVG (#490), categorized SVG library foundation (#491), SVG Dynamo composition (#493), protected material authority (#499), and driver diagnostics (#504/#506/#507) remain in the common base.
- Historical Playback from #478/#480 reconstructs Screens/Popups/Dynamos from historian TAG samples with a time cursor and read-only guards. It is NOT video/media playback. The obsolete overlapping draft #452 is closed as superseded, not merged over the newer implementation.
- Previously unpublished media/mobile work was preserved as source commit `21860859` and integrated as `1c3ef121`. Unrelated driver/lab changes in the source worktree were deliberately left untouched.
- Header/mobile/network-media additions are in `671e3b5d`. Docker host credential persistence is in #508, commit `6229f4a4`.

## Dínamos and SVG assets

**Dynamo artwork not accepted; Dynamo redesign deferred by owner.** Technical tests are not visual approval. #501 remains open. The static-library request is separate: its missing unified-library and editor insertion access is corrected in the follow-up below. External-source curation and artwork acceptance remain open in #484/#482. The next Dynamo plan needs well-drawn SVG bases, layered detail and independently bound animation/property slots, not recolored monochrome substitutes.

The replacement catalog contains 4 lamps, 4 buttons, 6 motors, 6 valves and 6 electrical contact/disconnector variants. Original first-party factory SVG geometry backs stable Visual Assets and canonical `core.svgSymbol` composition. The obsolete 72 built-ins are purged from the current source factory and their exclusive tests, not merely hidden in the UI. Migration retains a minimal marker test and removes platform-owned old entries without deleting project-authored definitions. The E3 conversion structure is retained. Thirty-seven replacement catalog/bootstrap tests pass.

Implemented public parameters include configurable stage colors/enables, fixed states, numeric/Boolean TAG and expression sources, inversion, typed Client Memory sources and dependencies, motor/valve discrete-state precedence, per-pole contact sources, state text and label placement, independent bezel/outline styling and depth effects. Buttons map to canonical command, analog write, Boolean set and toggle actions. Existing Client Visual scripts can execute through Events and publish state through Client Memory.

The parameter inspector now includes lazy-loaded Client Visual script/output selection and an inline wizard: generated handlers read the stable TAG's value only with Good quality and write an explicitly typed Client Memory output. Python syntax and canonical package Preview/CAS precede Apply. Existing scripts are not silently rewritten. Binding the output and saving/publishing the visual definition remain explicit operations.

Runtime Dynamo commands expose pending, accepted, failed and read-back-confirmed states. Duplicate in-flight actions are blocked. HTTP acceptance alone is never labelled as process confirmation; confirmation requires a fresh Good-quality matching TAG sample.

Mounted authoring review now uses definitions and artwork emitted from the actual C# catalog, not hand-maintained drawing fixtures. All 26 variants mount across five fixed states; lamp paint is explicitly asserted and screenshots captured. This found and fixed rectangular SVG backing plates, missing public-state projection in Design, and fixed-state paint replacing the independently configured outline. Design previews do not resolve executable command targets. Catalog 1.0.1 explicitly marks its command targets optional: absent targets omit the action rather than the whole drawing; configured invalid targets and mandatory commands still fail closed. All 26 definitions compose without commands in C# and expand successfully through the real frontend Runtime projection. Twenty-four catalog tests, fifteen projection contracts and four mounted authoring contracts pass. The latter also cover factory category/filter/copy through CAS, the script wizard's syntax/Preview/Apply/typed-output binding and combined Video/PDF composition. Their HTTP boundaries are mocked, not claimed as a full persisted project or Python-engine execution test.

The repository factory now exposes 2,208 original SVG review candidates through Engineering > Visual assets, grouped by category/style, searchable and paginated. Each selected item is sanitized by the canonical SVG inspector and copied through the normal CAS asset-import path into the project; it then supports normal static insertion and semantic paint animation. Source batches remain `draft`: this does not falsify human approval or mass-publish rejected/unreviewed built-ins. All 2,208 previews pass canonical SVG sanitization locally.

### Static-library access correction — owner report 2026-10-05

The gallery alone did not fulfill library integration. All 2,208 factory SVGs now enter the unified Library Catalog as categorized static visual assets, with real thumbnails, localized taxonomy, search and explicit copy to project. The shared visual-editor Library tab provides direct SVG insertion for Screens, Popups, Templates and Dynamo composition. Insertion imports only the selected SVG and reuses its stable project identity; it does not copy thousands of assets into every project. The server owns the sanitized payload and category/style/provenance metadata, enforces EngineeringModify and Workspace CAS, and upgrades metadata omitted by old gallery copies. Existing screen drafts survive asset refresh. Source batches still carry their actual review status; no artwork is falsely marked approved and external Opto/Wikimedia files are not implied to be bundled.

Local acceptance: production Web build, three factory backend tests, six catalog model tests and three other mounted authoring checks passed. A real API/PostgreSQL browser test (no mocked routes) browses the actual 2,208-entry library, copies by canonical identity, inserts the same asset in Screen/Popup/Template, preserves a previously added rectangle and draft name, verifies Preview/Apply and resulting exported references, reuses the same project asset, rejects stale Workspace CAS and unknown artwork, then restores its fixture. This test is part of the final post-merge integration smoke. It proves accessibility/insertion, not professional visual approval.

External Opto 22/Wikimedia artwork is not bundled without verified redistribution rights. #484 remains the external expansion/curation task. The original factory collection is available now, rather than merely residing in repository files. User references:

- [Opto 22 SVG library and editors](https://www.opto22.com/support/resources-tools/image-library-svg-editors): candidate categorized static artwork, not animation/property authority.
- [Wikimedia P and ID symbols](https://commons.wikimedia.org/wiki/Category:P%26ID_symbols): process symbols with per-file licenses/attribution requirements.

Static SVGs can use semantic dynamic paint slots without becoming Dynamos. Generated Dynamos retain animation, command and parameter authority in EliteSCADA rather than in downloaded artwork.

## Media and documents

#495/#496 now have integrated source DTO/registry/CRUD/package support, separate protected Basic/Bearer credential provisioning, and bounded project PDF/MP4/WebM resource handling. Video and PDF objects use the shared canonical renderer and normal toolbar/property authoring. Video can select a configured Media Source by identity.

The persisted acceptance found a real missing link: only the frontend registered `core.videoPlayer`/`core.pdfViewer`, so canonical API Preview rejected a screen authored with them. This consolidation registers both types and all eight media properties in the backend, validates asset MIME/identity and prospective Media Source GUIDs, and adds the same inert PDF response headers to Runtime as Engineering. Three real Active/registry parity browser checks and 482 Core tests pass after the fix.

The Runtime consumer resolves only an enabled source from the persisted Active project. HTTP/MJPEG playback is server-mediated, does not accept browser-supplied URLs, does not follow upstream redirects, limits concurrent connections and request/stream lifetimes, and never exposes source credentials or URLs. Engineering does not continuously connect cameras.

The media relay now rewrites HLS playlists/keys/segments to authenticated same-origin routes with opaque AES-GCM tickets bound to Active project/revision/source. Requests reject cross-origin playlist resources, redirects, loopback/link-local/metadata destinations and DNS rebinding. Private industrial LAN destinations require explicit host policy (enabled in the isolated stable deployment). Playlists/segments are bounded to 1/32 MiB; stream/concurrency deadlines remain bounded.

RTSP uses an isolated, digest-pinned MediaMTX 1.21.1 sidecar, provisioned only from Active source identities. Camera credentials remain server-side and dynamic configuration is in memory, not the portable package or process arguments. HLS.js 1.7.3 is lazy-loaded for desktop browser playback; native HLS remains available. Reconnect uses bounded exponential backoff and stops on authentication/unsupported formats. RTSP is remuxed, not transcoded: the camera codec must be supported by the browser; RTSPS currently requires a certificate-valid literal address.

The synthetic laboratory passes real RTSP-to-HLS provisioning and playlist/initialization/segment reads through revision-bound tickets. Chromium decodes the stream, advances playback time and pauses; credential failure stops automatic retry. That streaming fixture's HTTP Active-source boundary is mocked separately. The new `media-active-revision.spec.ts` exercises real PostgreSQL, public Preview/Apply/Save/Publish/Activate, persisted camera identity and exact PDF payload/ETag, draft-vs-Active isolation and canonical Runtime Video/PDF mounting without mocked HTTP. Each activation requires a new revision-bound Runtime lease. This closes a different boundary from the already accepted stream decoder test; it is not a repeat of the synthetic driver/media laboratory. Native PDF pixel decoding is not asserted by the endpoint/iframe contract.

## Runtime header and mobile

#503 now includes canonical header enable/height/background/title-position fields, individually visible tools, custom screen/image navigation buttons, fullscreen exit in the user menu only during actual fullscreen, and a hidden-header user-menu affordance accessible by hover, touch or keyboard.

Mobile Runtime uses coarse-pointer detection, simplified Runtime chrome, logical landscape rotation or configured portrait, proportional zoom, and per-screen mobile variants sharing the same Runtime/TAGs. Header and mobile configuration use Preview/Apply and require Save/Publish/Activate before the Active Runtime changes. Bézier point editing and thin-line selection from #505 remain integrated.

Five mounted desktop/mobile/fullscreen contracts pass, including privileged mobile visits to Engineering and database bookmarks rendering Runtime only. Hardware orientation locking cannot be promised in every browser; current implementation rotates the logical canvas. Native Android/iOS wrapper was not selected or implemented.

## Drivers and historical playback evidence

#504/#506/#507 are merged with their accepted exact-head T1 gates. The original Modbus 400 cause was fixed: persisted TAGs had been routed through the draft PointRead path. Active r13 Runtime and Test Read both returned 22/Good with the physical device on. Do not reopen that diagnosis as unknown.

The seven-protocol L3 lab accepted MQTT, IEC-104, CIP, OPC UA, DNP3, S7 and BACnet/IP activation/acquisition, supported writes, gateway and fault/recovery slices. The owner accepted controlled L3 peers because physical devices for every protocol are unavailable. The S7 writable-binding/no-write PointRead regression was fixed; S7/OPC UA read-only L3 probes passed. #500 remains administratively open pending the final integrated release gate, not more hardware.

Reporting and historian-based playback are integrated capabilities. Local/network video is a separate visual utility object and must not replace Historical Playback.

## Isolated Docker preview

The `elitescada-stable` API/web are rebuilt from the final merged source for this consolidation, not a selected subset of branches. Rebuild completion and exact source are recorded below:

- Web: http://localhost:18080/
- API: http://localhost:15080/
- PostgreSQL: localhost:15432
- Database volume: `elitescada-stable-database`

API/web were recreated; the PostgreSQL container remained `27eadc918f54e4bbceea6083f8ed9f93df1232ba97eb7724e5229dff065b62f4`. API health and web health return 200. No database/project purge occurred in this consolidation.

#508 adds distinct durable host-key and ciphertext volumes. The API provisions a private 0600 key once and reuses it on restart; plaintext credentials are not baked into Git/images. Keep both volumes together for credential continuity, separate from the database.

Restart policies and normal startup initialize the services automatically. Start/stop the whole Compose group in Docker; starting only web cannot start a stopped API/database by itself.

## Current verification and remaining sequence

The consolidation package passed production Web build, 482 Core tests, 969 Drivers tests (one opt-in already accepted media-lab test skipped), three visual-schema contracts, 19 mounted HA/database checks and three Active-media/property-parity browser checks. Test counts are scoped results, not invented full-suite claims. Latest CI #517 failures were a null screen in malformed Preview and a stale schema list missing `core.svgSymbol`; both are corrected. The fresh Runtime smoke now expects Developer HA Observe/Transfer but not HA Admin.

1. Merge the final non-Dynamo product package and obtain its exact post-merge CI; preserve any failure as a concrete blocker until fixed.
2. Rebuild the isolated API/web from that merged source, preserving PostgreSQL and protected-material/key volumes.
3. Close implemented media/driver/header/mobile/foundation tracking based on the final gate and preserved acceptance. Native Android/iOS packaging remains an undecided future option, not a browser Runtime defect.
4. Leave #501 artwork acceptance open and do not redesign Dynamos. Keep external SVG curation/visual approval separate from the corrected library access; the owner did not defer static-library access.
5. Hand the single integrated checkpoint to the residential-driver coordinator; do not send them to reconstruct historical branches or repeat accepted L3 tests.

At this checkpoint the only open PR found by GitHub live was #362, the independent preview-workbench lane targeting `preview/w15-first-project-env-harness`. It was deliberately not merged into the product. Revalidate GitHub before the next integration; do not treat stale screenshots or earlier checkpoint hashes as current authority.

## Final convergence follow-up — 2026-10-05

- PR #519 merged the categorized SVG library insertion bridge at `0455d0c22a9a84752d87f59855f33bc7a2439ffb`. Its post-merge run #37374617366 passed Web and Chromium; Backend failed only because the fresh-project smoke still expected the retired 72-item Dynamo count. The actual first-project seed is the current 26-item replacement catalog, confirmed by `EngineeringFirstProjectBootstrapTests`.
- PR #520 corrects that smoke contract to 26 and adds the missing `core.reportLauncher` object to the shared visual schema, icon-first palette/toolbar, property inspector and canonical Runtime. Stable report references are validated against the prospective package. Runtime History/Reports links now remain usable on mobile; Engineering/admin paths still resolve to the Runtime-only mobile surface.
- PR #520 T1 initially failed during classification because its description omitted the required `VALIDATION_PROFILE` declaration; no evidence job ran. After declaring `APP_SHELL,UI_EDITOR,RUNTIME_RENDERER,FOUNDATION_LIFECYCLE,SCRIPT_ENGINEERING,EEE_PACKAGE,CI_INFRA,DOCS_I18N_HELP`, run #37377526579 passed classification, common sanity, focused .NET and Web build. Chromium ran 63 tests: 58 passed, with two stale label assertions failing (the shipped History title and asset-import button labels had changed); the assertions are corrected in the next PR commit and a fresh synchronize T1 is pending. This second failure is test expectation drift, not a confirmed product defect.
- The refreshed T1 #37378504571 then passed classification, common sanity, focused .NET and Web, and Chromium passed 63/64 tests. The sole remaining failure was another stale expectation in the same locale contract: the history dataset intentionally uses `Eventos` / `Events` / `Eventos`, not the separate visual Events Browser's longer title. The test now follows the canonical history translations; this is assertion-only and awaits one fresh PR T1.
- Local checks on #520: production Web build, report-reference Preview test, built-in visual schema tests, 26-item first-project bootstrap test, and Playwright test discovery passed. Mounted Chromium did not run locally because this checkout has no configured PostgreSQL topology; CI owns that boundary.
- The isolated Docker preview still reflects #519 at this point. After #520 merges and the final post-merge CI is green, rebuild API/Web from the exact integrated head, preserving PostgreSQL plus protected-material/key volumes. Do not purge project data.
- Then update this record with the exact final SHA/run, close only issues whose acceptance is now demonstrated (#496 media/report utilities, #500 diagnostics, #482 library access, #503 mobile/runtime and #445 historical playback), and leave #484 source-license curation and #501 Dynamo artwork acceptance open for explicit content review. Do not resume the rejected 72-dynamo visual redesign.
