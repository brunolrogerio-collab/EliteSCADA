# Wave 15 consolidation handoff

Updated 2026-10-05. The product is consolidated in `wave15/corrections-integration`, including #508–#512 after product consolidation #502. This replaces the conflicting development checkpoints in this file. It is not a claim that every requested feature or final integrated CI is complete.

The mobile presentation also intercepts privileged Engineering/administration bookmarks: authorized Runtime users see the same Runtime canvas, without mounting desktop authoring or the second global header. Five mounted browser contracts pass for desktop, fullscreen, portrait-held mobile and both privileged mobile bookmarks. The media route explicitly resolves optional persistence from services and reports offline/503 on hosts without persistence, rather than failing endpoint construction.

The owner authorized integration and prioritized development over repeated testing. Do not repeat the already accepted driver/L3 work without a new regression. Run the final integrated post-merge CI only after the remaining product scope is integrated.

## Integrated product

- #502 now includes #453/#454 HA/database core, #455/#456 HA/database UI, #439 Help, and the 26 replacement Dynamo catalog. Those PRs are merged.
- Database administration is an Engineering entry beside High Availability, not a large global header title. The obsolete Area Atual header element is absent.
- Reporting V3 (#498), editable SVG (#490), categorized SVG library foundation (#491), SVG Dynamo composition (#493), protected material authority (#499), and driver diagnostics (#504/#506/#507) remain in the common base.
- Historical Playback from #478/#480 reconstructs Screens/Popups/Dynamos from historian TAG samples with a time cursor and read-only guards. It is NOT video/media playback. The obsolete overlapping draft #452 is closed as superseded, not merged over the newer implementation.
- Previously unpublished media/mobile work was preserved as source commit `21860859` and integrated as `1c3ef121`. Unrelated driver/lab changes in the source worktree were deliberately left untouched.
- Header/mobile/network-media additions are in `671e3b5d`. Docker host credential persistence is in #508, commit `6229f4a4`.

## Dínamos and SVG assets

The replacement catalog contains 4 lamps, 4 buttons, 6 motors, 6 valves and 6 electrical contact/disconnector variants. Original first-party factory SVG geometry backs stable Visual Assets and canonical `core.svgSymbol` composition. The obsolete 72 built-ins are purged from the current source factory and their exclusive tests, not merely hidden in the UI. Migration retains a minimal marker test and removes platform-owned old entries without deleting project-authored definitions. The E3 conversion structure is retained. Thirty-seven replacement catalog/bootstrap tests pass.

Implemented public parameters include configurable stage colors/enables, fixed states, numeric/Boolean TAG and expression sources, inversion, typed Client Memory sources and dependencies, motor/valve discrete-state precedence, per-pole contact sources, state text and label placement, independent bezel/outline styling and depth effects. Buttons map to canonical command, analog write, Boolean set and toggle actions. Existing Client Visual scripts can execute through Events and publish state through Client Memory.

The parameter inspector now includes lazy-loaded Client Visual script/output selection and an inline wizard: generated handlers read the stable TAG's value only with Good quality and write an explicitly typed Client Memory output. Python syntax and canonical package Preview/CAS precede Apply. Existing scripts are not silently rewritten. Binding the output and saving/publishing the visual definition remain explicit operations.

Runtime Dynamo commands expose pending, accepted, failed and read-back-confirmed states. Duplicate in-flight actions are blocked. HTTP acceptance alone is never labelled as process confirmation; confirmation requires a fresh Good-quality matching TAG sample.

Mounted authoring review now uses definitions and artwork emitted from the actual C# catalog, not hand-maintained drawing fixtures. All 26 variants mount across five fixed states; lamp paint is explicitly asserted and screenshots captured. This found and fixed rectangular SVG backing plates, missing public-state projection in Design, and fixed-state paint replacing the independently configured outline. Design previews do not resolve executable command targets; Runtime retains its existing fail-closed command requirement. Fifteen focused projection contracts and three mounted authoring contracts pass. The latter also cover factory category/filter/copy through CAS and the script wizard's syntax/Preview/Apply/typed-output binding. Their HTTP boundaries are mocked, not claimed as a full persisted project or Python-engine execution test.

The repository factory now exposes 2,208 original SVG review candidates through Engineering > Visual assets, grouped by category/style, searchable and paginated. Each selected item is sanitized by the canonical SVG inspector and copied through the normal CAS asset-import path into the project; it then supports normal static insertion and semantic paint animation. Source batches remain `draft`: this does not falsify human approval or mass-publish rejected/unreviewed built-ins. All 2,208 previews pass canonical SVG sanitization locally.

External Opto 22/Wikimedia artwork is not bundled without verified redistribution rights. #484 remains the external expansion/curation task. The original factory collection is available now, rather than merely residing in repository files. User references:

- [Opto 22 SVG library and editors](https://www.opto22.com/support/resources-tools/image-library-svg-editors): candidate categorized static artwork, not animation/property authority.
- [Wikimedia P and ID symbols](https://commons.wikimedia.org/wiki/Category:P%26ID_symbols): process symbols with per-file licenses/attribution requirements.

Static SVGs can use semantic dynamic paint slots without becoming Dynamos. Generated Dynamos retain animation, command and parameter authority in EliteSCADA rather than in downloaded artwork.

## Media and documents

#495/#496 now have integrated source DTO/registry/CRUD/package support, separate protected Basic/Bearer credential provisioning, and bounded project PDF/MP4/WebM resource handling. Video and PDF objects use the shared canonical renderer and normal toolbar/property authoring. Video can select a configured Media Source by identity.

The Runtime consumer resolves only an enabled source from the persisted Active project. HTTP/MJPEG playback is server-mediated, does not accept browser-supplied URLs, does not follow upstream redirects, limits concurrent connections and request/stream lifetimes, and never exposes source credentials or URLs. Engineering does not continuously connect cameras.

The media relay now rewrites HLS playlists/keys/segments to authenticated same-origin routes with opaque AES-GCM tickets bound to Active project/revision/source. Requests reject cross-origin playlist resources, redirects, loopback/link-local/metadata destinations and DNS rebinding. Private industrial LAN destinations require explicit host policy (enabled in the isolated stable deployment). Playlists/segments are bounded to 1/32 MiB; stream/concurrency deadlines remain bounded.

RTSP uses an isolated, digest-pinned MediaMTX 1.21.1 sidecar, provisioned only from Active source identities. Camera credentials remain server-side and dynamic configuration is in memory, not the portable package or process arguments. HLS.js 1.7.3 is lazy-loaded for desktop browser playback; native HLS remains available. Reconnect uses bounded exponential backoff and stops on authentication/unsupported formats. RTSP is remuxed, not transcoded: the camera codec must be supported by the browser; RTSPS currently requires a certificate-valid literal address.

The synthetic laboratory passes real RTSP-to-HLS provisioning and playlist/initialization/segment reads through revision-bound tickets. Chromium decodes the stream, advances playback time and pauses; credential failure stops automatic retry. Its HTTP Active-source boundary is mocked separately, not misrepresented as full persisted-source acceptance. This found and fixed the gateway's initial cookie-check 302. Remaining media acceptance: persisted source authority and combined Video/PDF authoring. #495/#496 remain open for those checks and the final integrated gate.

## Runtime header and mobile

#503 now includes canonical header enable/height/background/title-position fields, individually visible tools, custom screen/image navigation buttons, fullscreen exit in the user menu only during actual fullscreen, and a hidden-header user-menu affordance accessible by hover, touch or keyboard.

Mobile Runtime uses coarse-pointer detection, simplified Runtime chrome, logical landscape rotation or configured portrait, proportional zoom, and per-screen mobile variants sharing the same Runtime/TAGs. Header and mobile configuration use Preview/Apply and require Save/Publish/Activate before the Active Runtime changes. Bézier point editing and thin-line selection from #505 remain integrated.

Five mounted desktop/mobile/fullscreen contracts pass, including privileged mobile visits to Engineering and database bookmarks rendering Runtime only. Hardware orientation locking cannot be promised in every browser; current implementation rotates the logical canvas. Native Android/iOS wrapper was not selected or implemented.

## Drivers and historical playback evidence

#504/#506/#507 are merged with their accepted exact-head T1 gates. The original Modbus 400 cause was fixed: persisted TAGs had been routed through the draft PointRead path. Active r13 Runtime and Test Read both returned 22/Good with the physical device on. Do not reopen that diagnosis as unknown.

The seven-protocol L3 lab accepted MQTT, IEC-104, CIP, OPC UA, DNP3, S7 and BACnet/IP activation/acquisition, supported writes, gateway and fault/recovery slices. The owner accepted controlled L3 peers because physical devices for every protocol are unavailable. The S7 writable-binding/no-write PointRead regression was fixed; S7/OPC UA read-only L3 probes passed. #500 remains administratively open pending the final integrated release gate, not more hardware.

Reporting and historian-based playback are integrated capabilities. Local/network video is a separate visual utility object and must not replace Historical Playback.

## Isolated Docker preview

The `elitescada-stable` stack was rebuilt from integrated `d4373ac3` (through #513) on 2026-10-05:

- Web: http://localhost:18080/
- API: http://localhost:15080/
- PostgreSQL: localhost:15432
- Database volume: `elitescada-stable-database`

API/web were recreated; the PostgreSQL container remained `27eadc918f54e4bbceea6083f8ed9f93df1232ba97eb7724e5229dff065b62f4`. API health and web health return 200. No database/project purge occurred in this consolidation.

#508 adds distinct durable host-key and ciphertext volumes. The API provisions a private 0600 key once and reuses it on restart; plaintext credentials are not baked into Git/images. Keep both volumes together for credential continuity, separate from the database.

Restart policies and normal startup initialize the services automatically. Start/stop the whole Compose group in Docker; starting only web cannot start a stopped API/database by itself.

## Current verification and remaining sequence

This consolidation passed API build, production Web build, and 23 focused Core tests covering media/branding/header round-trip and invalid-reference rejection. The existing large-bundle warning is not a build failure. These results are not a final broad CI or full mounted product acceptance.

1. Complete #501's persisted package/Runtime command and script acceptance; the 26-variant authoring gallery and wizard checks above are already accepted and should not be repeated without a regression.
2. Complete #495/#496 mounted synthetic-stream and document/player acceptance.
3. Factory-library copy is verified at its mounted HTTP boundary; external #484 curation retains per-source redistribution evidence and is not replaced by undocumented third-party bundling.
4. Check the integrated editor/Runtime flows for regressions rather than retesting unchanged driver foundations.
5. Integrate the remaining product changes, then run one final post-merge CI and close acceptance issues based on its result.
6. Only then hand development to the residential-driver coordinator.

At this checkpoint the only open PR found by GitHub live was #362, the independent preview-workbench lane targeting `preview/w15-first-project-env-harness`. It was deliberately not merged into the product. Revalidate GitHub before the next integration; do not treat stale screenshots or earlier checkpoint hashes as current authority.
