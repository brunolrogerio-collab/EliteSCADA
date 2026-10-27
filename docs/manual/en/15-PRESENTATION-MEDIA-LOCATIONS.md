# Presentation, media and locations

## Header and Mobile: presentation and navigation

Configure visual identity and choose the right screen for a device.

### The header is not the process screen

Open Engineering > Header. Branding, titles, shortcuts and clock sit above the process screen. Changing them does not alter TAGs or draw process objects. Validate preview and Apply to Working, then save, publish and activate.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Brand presentation | Default uses SCADIVO; Text uses entered text; Image uses a project asset; Hidden removes branding. | Text: Pumping station |  |
| Text and subtitle | Title identifies the application; subtitle adds context. With an image the text also provides accessible identification. | Intake / Unit 1 | Text up to 128; subtitle up to 256 characters |
| Project image | Select/import a supported file. This is not a camera URL; use Media Sources for that. | Application logo |  |
| Height and colors | Height occupies usable space. Follow theme retains theme colors; custom colors override that choice. | Check readability on desktop and mobile | Height 56–168 px |
| Title, screen name and typography | Select alignment and font/size/weight/color. Showing screen name follows navigation without renaming the screen. | Title left; screen name below |  |
| Control position and order | Moves navigation as a group to the chosen side; order arranges relative position. User menu remains rightmost. | Controls right |  |
| Date and time | Enable if useful; select mode, format, position and order. This clock is not TAG acquisition time. | Date and time in header |  |
| Screen shortcuts | For each button choose label, existing screen and optional image. The screen must exist in the active revision. | Pumps → Pumping screen | Up to 16 editor shortcuts |

### Mobile: orientation and variant

Open Engineering > Mobile. Select Landscape or Portrait for touch devices; physical orientation lock depends on the browser. For each desktop screen choose Same desktop screen or an existing alternate. You must design that alternate; Portrait does not rearrange all objects automatically. Variants use the same TAGs/Runtime, not a separate data copy. Header settings remain under Header.

### Procedure and verification

1. Configure branding and necessary shortcuts.
2. Validate/apply.
3. Design a mobile variant in Screens and select it in Mobile if needed.
4. Validate/apply Mobile.
5. Save/publish/activate.
6. Check Runtime on desktop/mobile for buttons, navigation, readability and orientation. For an old layout inspect active revision; for a missing image inspect the selected asset.

[Open this guide in installed Help](/help?topic=application.presentation)

## Media Sources: camera, address and credential

Register the source, then choose its visual presentation.

### Source and object are separate steps

Open Engineering > Media Sources. The source describes where content comes from; a screen video/image object defines presentation. Registering RTSP does not guarantee native browser playback: it depends on the server relay. Check playback infrastructure before repeatedly changing settings.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Display name | Identifies the camera/source in the list. | Entrance camera |  |
| Stable key | Unique reference; use letters, numbers, dot, hyphen or underscore. | entrance-camera |  |
| Protocol | Choose the source format: HTTP/HTTPS, HLS, MJPEG or RTSP/RTSPS. Changing this option does not convert content. | HLS when the source provides HLS |  |
| Address | Absolute URL without username, password, query parameters or fragment. Credentials belong in the protected form. | https://camera.example.invalid/stream |  |
| Enabled | Controls source enablement in configuration; does not prove reachability or playback. | Enable the source in use |  |
| Protected credentials | After creating the source select Basic user/password or Bearer token. Save separately. They are write-only and excluded from packages; configure again on the target host. | Use the protected panel, not a password URL |  |

### Procedure and common problems

1. Create the source with name, key, protocol, address and enablement.
2. Save and configure credentials in the created source panel if needed.
3. Configure the corresponding visual object with its supported source.
4. Save/publish/activate and check Runtime playback. On failure inspect endpoint/protocol, service access, host credential, required relay and active revision. Removing credentials may block authenticated playback. Project export does not transfer camera passwords.

[Open this guide in installed Help](/help?topic=sources.media)

## Home/Building: locations and devices

Organize device locations and inspect their actual capabilities.

### Separate location, device and connection

In Engineering > Home/Building, first organize where a device is. Location may be a room or area. Equipment is the device instance, such as a light. Data Source stores the connection used to communicate.

These are different facts: moving a light to another room does not change its protocol. Start with devices and TAGs the connection actually offers.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Location name | Readable name within the hierarchy. | Pump room |  |
| Location kind | Choose Site, Building, Floor, Area/Room or Zone. The kind grants no permissions and creates no automation. | Area/Room |  |
| Parent location | Where it belongs. Choose an existing parent and avoid circular relationships; no parent represents a root where allowed. | Building 1 → Floor 1 → Pump room |  |
| Device location | Assigns an instance to an existing Location; organizes the project rather than testing communication. | Pump 01 in Pump room |  |
| Device name and path | Name is a label; path is a stable reference. Review the candidate before applying and preserve identity when reorganizing. | Check instance and reference in preview |  |

### Procedure and verification

1. Configure connectivity and inspect supplied TAGs/capabilities.
2. Create location hierarchy with name, kind and parent.
3. Assign devices to locations and review identity.
4. Preview and apply only the validated candidate.
5. Save/publish/activate.
6. Test readings/actions in a test installation. Displaying equipment does not create missing capabilities/commands; inspect provider, mapping and permissions. For graphical faceplates see Equipment and Templates.

[Open this guide in installed Help](/help?topic=home-building.organization)
