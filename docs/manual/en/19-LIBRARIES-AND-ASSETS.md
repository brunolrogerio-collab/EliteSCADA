# Libraries and assets: reuse and incorporation

## Libraries: associate, inspect and incorporate resources

Reuse resources without confusing the catalog with actual project content.

### Choose the reuse mechanism

Open Engineering > Libraries. An .escadalib file groups reusable resources such as Templates, Dynamos, Screens, Popups, Scripts and visual assets with dependencies. It is not a complete .escadapkg project, database backup or Authority. The catalog can show factory, project and associated-library origins. Association makes a library available for inspection; Use/incorporate copies the selected resource and required dependencies into Working. Only then is it project content. Visual Preview lets you inspect appearance without incorporation.

### Fields, filters and information

Name, type, category, origin and keywords help locate the right resource. No results may come from combined filters; clear them before importing another copy. Version/hash and dependency data are inspection information, not fields for arbitrary references.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| File to associate | Select the received .escadalib. The server validates structure/content; renaming another file to this extension does not convert it. | bombas-1.0.escadalib |  |
| Origin / catalog filters | Distinguish factory, project and associated library before use. Project resources already belong to Working; associated resources may still need incorporation. | Associated → Pump Template |  |
| Dependencies / inspection | Inspect resources accompanying the selection. A Screen may depend on Dynamos and images; incorporation can include more than one resource. Internal IDs/paths aid inspection, not manual recreation. | Screen + Dynamo + image |  |
| New library name | When exporting a selection, name the collection clearly. Not a Runtime application name. | Station pumps |  |
| New library version | Enter a version to identify the delivery. It describes the exported library; it neither changes project revision nor automatically updates incorporated copies. | 1.0 |  |
| Resources to export | Select existing project resources; choose the main item and inspect included dependencies. Export creates a file, not a published revision. | Pump Template and Popup |  |

### Procedure: use a received resource

1. Open Libraries and associate the file. Check name, version and origin.
2. Choose the library and filter the resource type. Inspect dependencies; use visual Preview when available. Association and Preview do not change Working.
3. Use/incorporate the desired resource; wait for confirmation and project refresh. This changes Working and may reuse equivalent content already present.
4. Open the resource in its appropriate area. Check TAGs, commands, parameters and references: a pump picture cannot infer your pump TAG.
5. Insert/configure objects in Screens or Popups and test. Save, publish and activate before checking Runtime.
6. To export your collection, enter name/version, select resources and download .escadalib. Reopen it in a test project to verify dependencies were included.

### Updates, disassociation and failures

Disassociate removes the library from the associated catalog; it does not delete incorporated project resources. Recorded origin is informational, not automatic synchronization with future versions. The same library ID with different content can conflict; check the required version before disassociating and associating another file. Unavailable Preview does not prove the whole package invalid: some kinds have no visual preview. If Workspace changed during the operation, reload before retrying. Association supplies no Driver credentials, users or license. The current associated catalog is process-local and may disappear after service restart; retain the file to associate it again. This differs from resources already incorporated and saved in the project.

[Open this guide in installed Help](/help?topic=libraries.associate-incorporate)

## Visual assets: import, name and use images

Organize graphics files and preserve references when renaming.

### Graphics file and visual object

Open Engineering > Visual assets. An asset is the image/model file; a Screen object displays it. Importing an image creates no TAG, command, parameterized Dynamo or Screen instance. Factory catalog and project assets are shown separately. Dynamo artwork or factory content may not appear in the user-image list; check its origin before importing duplicates.

### Fields and actions

Use Preview to check content, then a readable name to find it in the editor. Types offered by the picker are still service-validated; changing extensions is insufficient.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Import file | Select PNG, JPG, BMP or SVG for images; the picker also accepts GLB/GLTF for supported models. Check that the target object supports the type; not every control renders 3D. | bomba-frente.svg |  |
| Display name | Initially taken from the filename without extension. Rename changes the label and preserves stable identity used by references. | Pump — front view |  |
| Delete / confirm | Requires confirmation and may be rejected when the asset is in use. Remove or replace project references before trying again. | Delete an unused image |  |

### Procedure and verification

1. Import a file and wait for service confirmation.
2. Check Preview and name; adjust the label if needed.
3. Open the Screen/Popup and insert a compatible object, choosing the asset through its library/picker. For background or branding, use the relevant configuration field.
4. Adjust size/aspect ratio on the Screen; this does not alter original image pixels.
5. Save, publish and activate before checking Runtime. If missing, check reference, supported type, revision and content availability; do not replace an asset merely because another has the same-looking name.

[Open this guide in installed Help](/help?topic=assets.management)
