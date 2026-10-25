# Templates and Equipment

## Templates: create a reusable faceplate

Draw once and understand how changes reach linked equipment.

### Understand what you are creating

A Faceplate Template is a shared graphical definition, such as a pump drawing with name, state and reading. Equipment identifies a particular pump. Pump 01 and Pump 02 can use the same Template, and equipment can exist without a Template. An Intelligence profile organizes measurements and does not replace the drawing.

### Where to go and what to enter

Open Engineering > Templates. The editor uses the same objects, Properties, Dynamics and Events as Screens. Have editing permission and the editing lock when requested.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Template name | Readable library name. Describe its purpose rather than one physical instance. | Pump with state and pressure |  |
| Identifier | Stable reference used by links. Preserve it when updating the drawing; changing the display name does not require another equipment instance. | pump-faceplate |  |
| Objects and bindings | Drawing composition. A fixed TAG reference still points to that TAG when reused. Check each instance reference before representing different equipment. | Title, state and value display |  |

### Procedure: create and reuse

1. Select New Template and enter name and identifier.
2. Insert a small assembly and configure properties.
3. Add bindings/dynamics/events; do not assume TAGs are replaced automatically from equipment names.
4. Preview, resolve errors and apply to the project.
5. Open Equipment, create or select an instance and choose this optional Template. Save the link.
6. In the Screen editor, insert the instance from the equipment/faceplate library where available.
7. Save, publish and activate. Check two instances and their readings in Runtime.

### Update without confusing definition and instance

Editing the shared Template updates the drawing followed by all linked instances in that revision. Review the impact before applying; use another Template for a separate appearance. Changing the equipment link changes its associated drawing while preserving equipment identity. A Dynamo is another reusable visual assembly; a Group only arranges objects.

### If it is missing or does not update

Check the Template was applied, equipment points to the correct definition and the revision was activated. An independent object copy does not gain a link from sharing a name. Refresh and revalidate if the project changed. If another pump reading appears, inspect references rather than only the title.

[Open this guide in installed Help](/help?topic=templates.overview)

## Equipment: create, identify and link

Register the actual asset, then choose its drawing and measurements.

### Start with asset identity

Open Engineering > Equipment. Equipment represents a real instance such as Pump 01. It is neither a Driver, TAG nor drawing. Create it without a faceplate and attach a Template later if needed. Intelligence uses this identity in profile assignments; drawing a pump does not register equipment.

### Instance fields

The form shows a name and optional Template. The product generates the path; you do not invent or type it to create an instance.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Equipment name | Label for finding the asset. Use names that distinguish instances. | Pump 01 |  |
| Generated path | Stable reference shown for review. Renaming an existing instance preserves its path. A name generating an existing path blocks a new instance. | Check the generated-path preview |  |
| Faceplate Template (optional) | Selects the shared drawing. No Template is valid; removing the link does not delete equipment. Changing it does not select TAGs or Intelligence profiles automatically. | Pump Template or No Template |  |

### Procedure: register and check

1. Enter a distinct name.
2. Check the generated path and choose a Template or No Template.
3. Select Create equipment; the product validates before applying to the project.
4. Select it in the list to edit name/link; choose Save changes.
5. Save/publish/activate when Runtime needs the equipment.
6. In Intelligence select this equipment identity and assign a measurement profile. Configure TAGs and capture before analysis.

### Verification and common errors

The instance should appear with name, path and correct link. No Template does not mean invalid equipment. Template unavailable indicates an unresolved drawing link; select an existing Template or detach it. A duplicate path needs a distinct new-instance name. Refresh/review editing conflicts rather than creating repeated copies.

[Open this guide in installed Help](/help?topic=equipment.overview)
