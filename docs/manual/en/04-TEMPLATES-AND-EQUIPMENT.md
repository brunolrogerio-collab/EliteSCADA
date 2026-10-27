# Templates and Equipment

## Templates: create a reusable faceplate

Draw once and understand how changes reach linked equipment.

### Understand what you are creating

Use a Template Faceplate when several machines need the same drawing. For example, build a pump with name, state and reading and reuse it for Pump 01 and Pump 02. Template is the shared graphic definition; each Equipment identifies one actual pump.

You can register equipment without a Template and choose its drawing later. An Intelligence profile has a different purpose: it gathers measurements for analysis, not a replacement for the drawing.

### Procedure: create and reuse

1. In Engineering > Templates, use New Template and fill name and identifier.
2. Start with a few objects, such as title, state and one reading. Adjust their properties in the editor.
3. Configure required data bindings, visual changes and events. Equipment names do not automatically replace selected TAGs.
4. Run Preview, fix errors and apply to the project.
5. In Equipment, create or select a real instance and choose this optional Template. Save the assignment.
6. In the Screen editor, use the equipment/faceplate library to insert the instance when available.
7. Save, publish and activate. In Runtime, check two instances: the drawing may match, but each reading must belong to the correct equipment.

### Where to go and what to enter

Open Engineering > Templates. The editor uses the same objects, Properties, Dynamics and Events as Screens. Have editing permission and the editing lock when requested.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Template name | Readable library name. Describe its purpose rather than one physical instance. | Pump with state and pressure |  |
| Identifier | Stable reference used by links. Preserve it when updating the drawing; changing the display name does not require another equipment instance. | pump-faceplate |  |
| Objects and bindings | Drawing composition. A fixed TAG reference still points to that TAG when reused. Check each instance reference before representing different equipment. | Title, state and value display |  |

### Update without confusing definition and instance

A change to the shared definition can appear in every linked instance when its revision goes into use. Before applying, check which equipment uses that Template. For a different appearance in only some instances, consider another definition.

Changing the assigned Template changes the drawing while retaining equipment identity. A Dynamo is another reusable visual set. A Group only organizes objects; it does not have the same shared-definition link.

### If it is missing or does not update

Check the Template was applied, equipment points to the correct definition and the revision was activated. An independent object copy does not gain a link from sharing a name. Refresh and revalidate if the project changed. If another pump reading appears, inspect references rather than only the title.

[Open this guide in installed Help](/help?topic=templates.overview)

## Equipment: create, identify and link

Register the actual asset, then choose its drawing and measurements.

### Start with asset identity

Start by registering the machine that exists in the plant, such as Pump 01. In Engineering > Equipment, registration gives the actual instance an identity, separate from its connection, TAGs and screen drawing.

You can create equipment without a faceplate and assign a Template later. Intelligence uses this identity for profile assignments. Drawing a pump on a Screen does not automatically register equipment.

### Procedure: register and check

1. Enter a distinct name.
2. Check the generated path and choose a Template or No Template.
3. Select Create equipment; the product validates before applying to the project.
4. Select it in the list to edit name/link; choose Save changes.
5. Save/publish/activate when Runtime needs the equipment.
6. In Intelligence select this equipment identity and assign a measurement profile. Configure TAGs and capture before analysis.

### Instance fields

The form shows a name and optional Template. The product generates the path; you do not invent or type it to create an instance.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Equipment name | Label for finding the asset. Use names that distinguish instances. | Pump 01 |  |
| Generated path | Stable reference shown for review. Renaming an existing instance preserves its path. A name generating an existing path blocks a new instance. | Check the generated-path preview |  |
| Faceplate Template (optional) | Selects the shared drawing. No Template is valid; removing the link does not delete equipment. Changing it does not select TAGs or Intelligence profiles automatically. | Pump Template or No Template |  |

### Verification and common errors

The instance should appear with name, path and correct link. No Template does not mean invalid equipment. Template unavailable indicates an unresolved drawing link; select an existing Template or detach it. A duplicate path needs a distinct new-instance name. Refresh/review editing conflicts rather than creating repeated copies.

[Open this guide in installed Help](/help?topic=equipment.overview)
