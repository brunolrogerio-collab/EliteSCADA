# Wave 15 consolidation handoff

Updated 2026-10-05. The product is consolidated in `wave15/corrections-integration` at `272f1742` (merged #508), following the product consolidation #502 at `9cae3a82`. This replaces the conflicting development checkpoints in this file. It is not a claim that every requested feature or final integrated CI is complete.

The owner authorized integration and prioritized development over repeated testing. Do not repeat the already accepted driver/L3 work without a new regression. Run the final integrated post-merge CI only after the remaining product scope is integrated.

## Integrated product

- #502 now includes #453/#454 HA/database core, #455/#456 HA/database UI, #439 Help, and the 26 replacement Dynamo catalog. Those PRs are merged.
- Database administration is an Engineering entry beside High Availability, not a large global header title. The obsolete Area Atual header element is absent.
- Reporting V3 (#498), editable SVG (#490), categorized SVG library foundation (#491), SVG Dynamo composition (#493), protected material authority (#499), and driver diagnostics (#504/#506/#507) remain in the common base.
- Historical Playback from #478/#480 reconstructs Screens/Popups/Dynamos from historian TAG samples with a time cursor and read-only guards. It is NOT video/media playback. The obsolete overlapping draft #452 is closed as superseded, not merged over the newer implementation.
- Previously unpublished media/mobile work was preserved as source commit `21860859` and integrated as `1c3ef121`. Unrelated driver/lab changes in the source worktree were deliberately left untouched.
- Header/mobile/network-media additions are in `671e3b5d`. Docker host credential persistence is in #508, commit `6229f4a4`.

## Dínamos and SVG assets

The replacement catalog contains 4 lamps, 4 buttons, 6 motors, 6 valves and 6 electrical contact/disconnector variants. Original first-party factory SVG geometry backs stable Visual Assets and canonical `core.svgSymbol` composition. The obsolete 72 built-ins are retired from the current library/bootstrap; the E3 conversion structure is retained.

Implemented public parameters include configurable stage colors/enables, fixed states, numeric/Boolean TAG and expression sources, inversion, typed Client Memory sources and dependencies, motor/valve discrete-state precedence, per-pole contact sources, state text and label placement, independent bezel/outline styling and depth effects. Buttons map to canonical command, analog write, Boolean set and toggle actions. Existing Client Visual scripts can execute through Events and publish state through Client Memory.

Remaining in #501: direct script/output selection and inline authoring wizard, richer command pending/failure/confirmed-feedback treatment, and mounted visual acceptance of all 26 variants across authoring/package/Runtime. Do not describe these missing pieces as complete.

The categorized static-library foundation is integrated, but the requested collection of more than 1200 external static assets is NOT imported. #484 remains the expansion/curation task. Verify licenses per asset before redistribution. User references:

- [Opto 22 SVG library and editors](https://www.opto22.com/support/resources-tools/image-library-svg-editors): candidate categorized static artwork, not animation/property authority.
- [Wikimedia P and ID symbols](https://commons.wikimedia.org/wiki/Category:P%26ID_symbols): process symbols with per-file licenses/attribution requirements.

Static SVGs can use semantic dynamic paint slots without becoming Dynamos. Generated Dynamos retain animation, command and parameter authority in EliteSCADA rather than in downloaded artwork.

## Media and documents

#495/#496 now have integrated source DTO/registry/CRUD/package support, separate protected Basic/Bearer credential provisioning, and bounded project PDF/MP4/WebM resource handling. Video and PDF objects use the shared canonical renderer and normal toolbar/property authoring. Video can select a configured Media Source by identity.

The Runtime consumer resolves only an enabled source from the persisted Active project. HTTP/MJPEG playback is server-mediated, does not accept browser-supplied URLs, does not follow upstream redirects, limits concurrent connections and request/stream lifetimes, and never exposes source credentials or URLs. Engineering does not continuously connect cameras.

Remaining: RTSP/HLS conversion gateway, richer reconnect/playback/PDF controls and mounted acceptance. RTSP/HLS currently return explicit unsupported diagnostics, not fake success. #495/#496 remain OPEN.

## Runtime header and mobile

#503 now includes canonical header enable/height/background/title-position fields, individually visible tools, custom screen/image navigation buttons, fullscreen exit in the user menu only during actual fullscreen, and a hidden-header user-menu affordance accessible by hover, touch or keyboard.

Mobile Runtime uses coarse-pointer detection, simplified Runtime chrome, logical landscape rotation or configured portrait, proportional zoom, and per-screen mobile variants sharing the same Runtime/TAGs. Header and mobile configuration use Preview/Apply and require Save/Publish/Activate before the Active Runtime changes. Bézier point editing and thin-line selection from #505 remain integrated.

Remaining acceptance: mounted desktop/mobile/fullscreen/role flows and visual review. Hardware orientation locking cannot be promised in every browser; current implementation rotates the logical canvas. Native Android/iOS wrapper was not selected or implemented.

## Drivers and historical playback evidence

#504/#506/#507 are merged with their accepted exact-head T1 gates. The original Modbus 400 cause was fixed: persisted TAGs had been routed through the draft PointRead path. Active r13 Runtime and Test Read both returned 22/Good with the physical device on. Do not reopen that diagnosis as unknown.

The seven-protocol L3 lab accepted MQTT, IEC-104, CIP, OPC UA, DNP3, S7 and BACnet/IP activation/acquisition, supported writes, gateway and fault/recovery slices. The owner accepted controlled L3 peers because physical devices for every protocol are unavailable. The S7 writable-binding/no-write PointRead regression was fixed; S7/OPC UA read-only L3 probes passed. #500 remains administratively open pending the final integrated release gate, not more hardware.

Reporting and historian-based playback are integrated capabilities. Local/network video is a separate visual utility object and must not replace Historical Playback.

## Isolated Docker preview

The `elitescada-stable` stack was rebuilt from the integrated `272f1742` tree on 2026-10-05:

- Web: http://localhost:18080/
- API: http://localhost:15080/
- PostgreSQL: localhost:15432
- Database volume: `elitescada-stable-database`

API/web were recreated; the PostgreSQL container remained `27eadc918f54e4bbceea6083f8ed9f93df1232ba97eb7724e5229dff065b62f4`. API health and web health return 200. No database/project purge occurred in this consolidation.

#508 adds distinct durable host-key and ciphertext volumes. The API provisions a private 0600 key once and reuses it on restart; plaintext credentials are not baked into Git/images. Keep both volumes together for credential continuity, separate from the database.

Restart policies and normal startup initialize the services automatically. Start/stop the whole Compose group in Docker; starting only web cannot start a stopped API/database by itself.

## Current verification and remaining sequence

This consolidation passed API build, production Web build, and 23 focused Core tests covering media/branding/header round-trip and invalid-reference rejection. The existing large-bundle warning is not a build failure. These results are not a final broad CI or full mounted product acceptance.

1. Finish #501's script wizard/command feedback and mounted visual review.
2. Finish #495/#496 gateway/player/document scope.
3. Curate the requested static SVG collection under #484 and retain redistribution evidence.
4. Check the integrated editor/Runtime flows for regressions rather than retesting unchanged driver foundations.
5. Integrate the remaining product changes, then run one final post-merge CI and close acceptance issues based on its result.
6. Only then hand development to the residential-driver coordinator.

At this checkpoint the only open PR found by GitHub live was #362, the independent preview-workbench lane targeting `preview/w15-first-project-env-harness`. It was deliberately not merged into the product. Revalidate GitHub before the next integration; do not treat stale screenshots or earlier checkpoint hashes as current authority.
