# Libraries and assets: reuse and incorporation

## Libraries: associate, inspect and incorporate resources

Reuse resources without confusing the catalog with actual project content.

### Choose the reuse mechanism

A .escadalib library is a reusable resource set, such as Template, Dynamo, Screen, Popup, Script or graphic file. It includes dependencies, the other resources needed by the chosen item. It is not a complete .escadapkg project, database backup or Authority.

Associating only makes the library available in the catalog. Visual Preview shows appearance without copying into the project. Using/incorporating copies the resource and required dependencies into Working, the project draft. Only then are they part of the project. Check whether the catalog shows factory, project or associated-library content.

### Procedure: use a received resource

1. Open Libraries and associate the received file. Check name, version and origin.
2. Choose the library and filter resource type. Review dependencies and visual Preview when available. Association and Preview do not change Working.
3. Use/incorporate the resource and wait for confirmation. This changes the draft and may reuse equivalent content already present.
4. Open the item in its editor. Check TAGs, commands, parameters and references: a pump drawing does not choose your pump’s readings.
5. Insert/configure objects in Screens or Popups and test. Save, publish and activate before checking Runtime.
6. To export your set, enter name/version, select resources and download .escadalib. Reopen in a test project to check included dependencies.

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

### Updates, disassociation and failures

Disassociate removes the library from the associated catalog; it does not delete incorporated project resources. Recorded origin is informational, not automatic synchronization with future versions. The same library ID with different content can conflict; check the required version before disassociating and associating another file. Unavailable Preview does not prove the whole package invalid: some kinds have no visual preview. If Workspace changed during the operation, reload before retrying. Association supplies no Driver credentials, users or license. The current associated catalog is process-local and may disappear after service restart; retain the file to associate it again. This differs from resources already incorporated and saved in the project.

[Open this guide in installed Help](/help?topic=libraries.associate-incorporate)

## Visual assets: import, name and use images

Organize graphics files and preserve references when renaming.

### Graphics file and visual object

To use an image or model on a Screen, first import its file in Engineering > Visual Assets. Asset means that graphic resource; a visual object displays it on the Screen. Importing creates no TAG, command, parameterized Dynamo or drawing instance.

Check origin before importing another copy. Factory content and project files are separate. Artwork included in Dynamos or factory content may not appear in the user-image list; look under its corresponding origin.

### Procedure and verification

1. Import a file and wait for service confirmation.
2. Check Preview and name; adjust the label if needed.
3. Open the Screen/Popup and insert a compatible object, choosing the asset through its library/picker. For background or branding, use the relevant configuration field.
4. Adjust size/aspect ratio on the Screen; this does not alter original image pixels.
5. Save, publish and activate before checking Runtime. If missing, check reference, supported type, revision and content availability; do not replace an asset merely because another has the same-looking name.

### Fields and actions

Use Preview to check content, then a readable name to find it in the editor. Types offered by the picker are still service-validated; changing extensions is insufficient.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Import file | Select PNG, JPG, BMP or SVG for images; the picker also accepts GLB/GLTF for supported models. Check that the target object supports the type; not every control renders 3D. | bomba-frente.svg |  |
| Display name | Initially taken from the filename without extension. Rename changes the label and preserves stable identity used by references. | Pump — front view |  |
| Delete / confirm | Requires confirmation and may be rejected when the asset is in use. Remove or replace project references before trying again. | Delete an unused image |  |

[Open this guide in installed Help](/help?topic=assets.management)
