# EliteSCADA — Work UI Audit Handoff

Product: EliteSCADA

Technically validated candidate SHA: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`

Corrected canonical C11: `19d5257d970f53ae798c5fa53946fce07c586452`

Package: `preview/fixtures/EliteSCADA-EEE-Demo.escadapkg`

Package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`

Preview / Runtime: `https://psychic-space-zebra-5vx477r5vq6gc4xpr-5173.app.github.dev/`

Engineering: `https://psychic-space-zebra-5vx477r5vq6gc4xpr-5173.app.github.dev/engineering`

Application: `eee-demo` / **EliteSCADA — EEE Demo** / Active revision 2.

Initial state expected: EEE loaded, simulation running with changing TAG values, Historian + HistoricalQuery ready, useful Alarm/Operational Event state prepared, Licensing Demo, API 5080 internal and Web 5173 forwarded.

Authentication: use username `EliteSCADA`; the Product Owner supplies the existing Preview password directly to Work through the secure conversation. Never write the password in GitHub, logs, screenshots, findings or repository files.

First pass boundary: **audit only**. Use the real browser product; do not modify code, create patches/commits, merge PRs or spend the Work window on repository/environment maintenance. Record findings under #289 using stable IDs, severity P0–P3, reproduction, expected/observed, reproducibility, classification (`GENERIC PRODUCT`, `EEE-SPECIFIC`, `UNCERTAIN`) and evidence.

Start with free exploration. Then deliberately verify Runtime, Screens, Popups, Dynamos, TAGs, Alarm/Event/Historian/Trends, Engineering, Screen Editor, Popup Editor, Properties, theme/contrast, localization, error UX and representative viewport/fullscreen behavior.

## Product Owner prior observations to investigate, not assume

Confirm or refute independently; do not restrict the audit to these items:

- `PO-PRE-01` Engineering navigation should behave as an independently scrollable/collapsible panel across development areas, avoiding whole-page scrolling just to switch modules.
- `PO-PRE-02` Templates / Equipments / Dynamos lists appear to lack a useful visual preview of the selected object.
- `PO-PRE-03` Tags / Sources navigation should have independently scrollable lists and avoid requiring the same TAG to be selected twice; one selection should drive parameters and summary.
- `PO-PRE-04` Screen / Popup authoring feels too detached from visual output; evaluate direct visual/WYSIWYG-style editing, better space use and a resizable/optimized Properties panel.
- `PO-PRE-05` Security currently appears limited to `developer` and `operator`; evaluate whether configurable roles/permissions can be created and associated with users without weakening authority.
- `PO-PRE-06` Library creation/browsing appears to lack useful visual preview of objects.
- `PO-PRE-07` Script authoring lacks intuitive discovery/autocomplete for commands, objects, TAGs, properties/paths plus syntax validation/debugging. Practical test: compare two TAGs and, when true, change an object's color/state without having to guess internal APIs.
- `PO-PRE-08` Application Protection in the persistent header may be intrusive; evaluate whether a footer/status-area placement is more appropriate.
- `PO-PRE-09` Engineering Cycle / project-management surfaces sometimes appear and sometimes return 404; determine product vs route/harness/environment cause.
- `PO-PRE-10` Runtime ↔ Engineering transitions have been observed taking roughly 30 seconds; measure and classify separately because Codespaces capacity may be a factor.

Known setup blocker: **NONE** for this candidate based on exact-head CI plus real Codespace startup evidence.

Audit result destination: issue `#289`. Preserve original evidence; coordinator will triage/reproduce before any correction.