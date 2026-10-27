# Screens, Popups and objects

A screen brings together the process drawing, readings and operator actions. A Popup is a supporting window for equipment details without replacing the main screen. This chapter builds a small example before introducing layout and reuse.


This chapter starts with a small screen: a title and a reading. Follow sections 1 to 3 and check the result before adding interaction or reuse. The following sections provide detail when needed.

A **TAG** is the variable providing the reading. **Structure** is the list of objects in the drawing. **Inspector** is the panel for adjusting the selected object. A **property** is one such setting, for example width or color. Controls have specific names, but the process stays the same: select, adjust and check the effect.

## 1. Prepare a verifiable example

Use a test application with a numeric pressure TAG whose unit and current value you know. The examples call it `Plant.Pump.Pressure`, in bar. This is illustrative: select the actual TAG from your application instead of binding to a nonexistent reference. To test writes, use a writable test variable; do not learn the editor by sending real process commands.

Open Engineering in the correct project. Confirm your account can edit and that you hold the editing lock when required. Visual editing has two stages: validate/apply the drawing to the project, then save/publish/activate a revision. Seeing a change in the editor does not mean Runtime is already executing it.

## 2. Find the editor controls

| Region | Purpose | First action |
| --- | --- | --- |
| Screen or Popup list | Select an existing definition or create another | Check name and project before editing. |
| Top toolbar | Insert shapes, text, values, controls and browsers; undo/redo | Start with text and a value display. |
| Drawing area | Position, select and resize objects | Find an object in Structure if you cannot select it on the drawing. |
| Toolbar beside the drawing | Align, distribute, size, group and lock; adjust zoom | Select the objects that should participate. |
| Structure / Library / Assets | Find objects, insert reusable items and import visual assets | Structure helps select overlapping items. |
| Properties / Dynamics / Events | Configure the selection | With no selected object, Properties shows the surface and Events may show screen events. |
| Validation and application | Check the candidate and apply it to the project | Read errors before applying. |

The panel changes with the selection. If a property disappears, check whether you selected one object, several objects or the background. Some fields apply only to a particular object type.

## 3. Build the first screen

1. Open **Screens** and create a definition. Give it a display name such as `Pumping`. The identifier is the stable screen reference, not its visual title. Preserve it once other items refer to the screen.
2. With no objects selected, check surface dimensions, orientation and background. Dimensions define the logical drawing area; zoom only changes the editor view. Changing orientation does not automatically design a separate mobile layout.
3. Insert text. Set its content to `Discharge pressure`, choose a readable color and place it near the reading. Fixed text needs no TAG.
4. Insert a **Value display**. Select it and locate **TAG / variable value** in **Properties**. Select the pressure TAG and apply the binding. This object's binding destination is the displayed text.
5. Enable fixed decimal places and select `1` to show tenths. This changes presentation, not TAG precision. Add a `bar` unit label; distinguish the number from its unit.
6. Position and size the objects. Align the title and reading using the arrangement controls. Run **Preview change**, inspect errors or the valid candidate, then choose **Apply to Workspace**. Labels follow the selected interface language.
7. Follow save, publish and activate in [Engineering and data](03-ENGINEERING-DATA-SOURCES-TAGS.md). Configure navigation if needed; creating a screen does not automatically make it the initial screen.
8. Open it in Runtime and compare the reading with the TAG. Change the test value and check updates, decimals and unit. When data is missing, inspect quality and availability; zero does not prove communication works.

**Expected result:** a fixed title and variable reading appear together; the reading follows the TAG in the active revision. The title remains unchanged as the TAG changes.

## 4. Understand properties before filling them in

| Field or group | What it defines | Example and effect |
| --- | --- | --- |
| X/Y position and width/height | Drawing position and size | More width accommodates a long value; it does not change the process scale. |
| Rotation | Object orientation | Rotate a symbol to match the installation; check text and occupied space too. |
| Visible | Whether the object is presented | Useful with a dynamic condition; hiding an object does not stop equipment. |
| Enabled | Whether a control accepts interaction | Disabling a button neither grants nor revokes user permissions. |
| Text, font, size and alignment | Content and readability | A section title and an operational value may need different sizes. |
| Fill, outline and thickness | Shape appearance | Use contrast and labels, not color alone for important states. |
| Minimum, maximum, value and unit | Indicator/control range and presentation | A 0–10 bar scale needs pressure in the same unit. The visual range does not automatically convert the TAG. |
| Decimal places | Reading format | One place shows tenths; it does not improve instrument accuracy. |

A **fixed value**, a **binding** and a **dynamic** serve different purposes. The fixed value is the starting configuration. A binding supplies a source value, such as a TAG. A dynamic calculates or selects a presentation from data. If a property changes on its own, inspect its dynamics and binding before repeatedly editing the fixed value.

Installed Help provides an individual reference for each object type, including supported fields, defaults, units and bounds. Open **Help → Screens and objects** and choose the type; search also finds property names. Technical identifiers remain available for reference without requiring you to memorize them.

## 5. Choose an object by purpose

| Need | Object and configuration to inspect |
| --- | --- |
| Draw equipment and piping | Rectangle, ellipse, line or polygon: position, size, outline/fill; polygons also have points. Shapes alone do not control the process. |
| Identify or display a reading | Text or Value display: text/font/alignment; readings also need binding and formatting. |
| Show a quantity within a range | Dial, ring, graduated bar, thermometer, bands or actual versus target: value, minimum/maximum, unit, limits/target and the presentation options applicable to that type. Choose an operationally meaningful range. |
| Change a variable | Numeric input or slider: value binding, range, step and allow-write option. TAG write direction, account authorization and Runtime session are still required. |
| Perform an action | Button: caption, enabled state and event/action. A caption such as “Start” does not configure a command. |
| Observe change over time | Trend: add series and configure TAG, legend, axis and scale. The window belongs to the chart; each series has its own settings. |
| Query occurrences | Alarm or event browser: source/mode, period, filters, columns and ordering. Acknowledging an alarm differs from recording an event. |
| Use media or documents | Image, SVG symbol, 3D model, video or PDF: select a supported resource and set fit/presentation. Do not place credentials in URLs. |
| Open a report | Report launcher: select an existing report and available parameters. A visual object does not create the report definition. |
| Display Intelligence | Use guided generation to associate objects with existing profiles/equipment and results; see [Asset and Process Intelligence](12-ASSET-PROCESS-INTELLIGENCE.md). |

### Trend: an example with two quantities

Add one pressure series and one current series. Give them clear legends such as `Discharge pressure` and `Motor current`. Use one axis for bar and another for A: mixing incompatible units on one scale makes interpretation difficult. Differentiate color and style. A fixed 0–10 bar scale helps compare amplitudes; values outside it may be outside the visible area.

Check live/history mode, window, refresh and Historian capture. Selecting a period does not create samples that were never stored. Start browsers without restrictive filters, then add one filter at a time; an empty list may result from configuration.

## 6. Add dynamics and interaction with clear intent

In **Dynamics**, select a single object. Choose the property to vary and its source. A Boolean condition can change visibility; a map can relate states to presentations; analog fill can represent level. Use only options offered for the type, and inspect available quality/missing-value conditions. Test true, false and missing-data states; do not present communication loss as stopped equipment.

In **Events**, distinguish ready-made actions from Script handlers. For a simple action, select a button, choose the click action and destination. The ready-made action wizard uses **Click** and keeps one action per trigger; adding another for that trigger replaces the previous one. For more logic, configure a Client handler and dependencies as described in [Scripting](06-SCRIPTING-ALARMS-HISTORY.md). A ready-made action and a Script are different configurations.

To write a test variable, select the write action and actual reference, respecting type and range. To execute a command, select the actual command. `CommandExecute` and `ProcessValueWrite` are distinct authorizations; visual properties grant neither. After requesting a command, use the result and actual state to indicate success; changing a button's color does not confirm the equipment started.

## 7. Create and open a Popup

1. Open **Popups**, create one and name it `Pump details`. Choose dimensions suitable for the device. Select an existing template only if the application needs one; a Popup can also be standalone.
2. Insert identification and readings using the same object types as a screen. Bind the correct equipment TAGs. Validate and apply the Popup to the project.
3. Return to the source screen and select a button. In **Events**, choose **Open popup**, select the destination and add the action. Current controls may show that English label even in another locale.
4. Validate and apply the screen; save, publish and activate the revision containing both resources.
5. In Runtime, click the button, check readings and close the Popup. The main screen should remain the operating context.

If another pump's data appears, correct the instance bindings or parameters. Renaming the Popup alone does not change data sources. Opening it does not grant permission to perform its actions.

## 8. Arrange and reuse without losing references

Use Structure to select objects behind others. Visual order determines what appears in front; alignment positions edges/centers; distribution arranges spacing; common size makes dimensions consistent. Grouping helps work on a set but does not create equipment or combine permissions. Locking helps avoid accidental moves; it is not a security lock.

Visual assets are imported files, such as images. Dynamos are reusable drawings with objects and parameters; an instance may expose its own parameters. After inserting a dynamo, check instance parameters, dimensions, orientation and references. A pump's appearance does not automatically select its TAGs. A copied drawing also needs its bindings reviewed before representing different equipment.

## 9. Diagnose by symptom

| Symptom | Check first |
| --- | --- |
| Property is missing | Selection type/count, panel filter and whether the field is editable. |
| Reading stays fixed | Applied binding, actual TAG, availability/quality and active revision. |
| Object changes despite fixed value | Dynamics, bindings and Script affecting the same property. |
| Button does nothing | Configured action/event, existing target, enabled state, revision and authorization. |
| Write is rejected | Value type/range, TAG write direction, permission and Runtime session class. |
| Object is absent | Visibility, position/size, overlap order, visual asset and range/scale. |
| Apply is rejected after validation | The project may have changed; refresh, review changes and validate again. |
| Runtime shows the old drawing | Save, publish and activate are separate from applying the drawing. Check the active revision. |

## 10. Continue with Help

Installed topics: `visual.first-screen`, `visual.object-selection`, `visual.properties`, `visual.dynamics`, `visual.events`, `visual.popup-workflow`, `visual.trend-series`, `visual.browser-filters` and `libraries.reusable-resources`. Open one through `/help?topic=<identifier>` in the product. Final chapter captures will come from the actual UI after the visual audit; an illustrated image cannot substitute for the accepted interface.
