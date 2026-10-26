# First project: from sign-in to Runtime

## First project: from sign-in to the first reading

Follow a small engineering exercise through a known Runtime sample without connecting equipment.

### Prepare the exercise environment

Use a dedicated test installation and an authorized account. Check address/project before editing; do not learn revision activation on an operating plant. This exercise reads internal memory, not a real sensor. If an application exists, do not detach just to force initial setup: use the authorized test project or ask its owner to prepare the environment.

### Sign in and create the project

1. Open the correct address. On an eligible new installation complete initial Administrator setup; to recover existing data use Restore backup. Never assume default credentials.
2. Sign in with the authorized account.
3. If Create New Project is offered, enter Project key and Project name, then Create project and open Engineering.
4. Confirm the opened project. If absent, check whether a project already exists and use its corresponding flow. Creation is not a new Active application. If another user created a project while this form was open, reload/check state instead of repeating blindly.

### Fields for this exercise

Names below describe a test project. Use readable references; product workflows select/generate internal IDs.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Project key | Stable project identity, distinct from presentation title. | pilot-plant | 1–200 characters after trimming; not a license key or an ID to guess. |
| Project name | Readable name shown in Engineering. | Pilot plant | 1–300 characters after trimming; confirm destination. |
| TAG source | For this exercise select/create Server Memory from the catalog, without host/port. | Server Memory | Internal provider, not an equipment driver or browser memory. |
| TAG path | Engineering reference used by Script and screen. | Demo.Level | Associate with prepared memory source; a path alone creates no sample. |
| Type | Choose a numeric type compatible with this sample. | Double | Do not use String to conceal a value incompatibility. |
| Script scope | Choose Server to publish the server-owned sample. | Server | Client Visual runs in the client and does not replace this publisher. |
| Trigger and handler | Add Initialize with the exact function name below. | Initialize / initialize_demo | Function name alone does not configure a trigger; enable Script/entry as required by the editor. |
| Declared Script reference | Declare Demo.Level as ServerMemoryTag through Script references. | Demo.Level | API requires declared Server Memory; code text does not replace valid configuration. |
| Value Display binding | Select Demo.Level in the object data picker. | Demo.Level value | Do not guess a GUID or leave fixed 42 pretending to be a sample. |
| Screen | Create a simple title/Value Display screen and configure supported opening/navigation. | Exercise screen | Creating a screen does not guarantee automatic Runtime opening. |

### Produce a known sample

1. Prepare Server Memory and Double TAG Demo.Level in Data Sources/TAGs, applying valid edits to Working.
2. Create an enabled Python Server Script and declare Demo.Level as ServerMemoryTag.
3. Add Initialize with handler initialize_demo.
4. Use this code and validate. It publishes 42.0/Good because this is a known exercise value; no equipment is measured. It is not continuous: Initialize publishes when that entry executes as the revision starts. For periodic updates study Timer/cadence in Script Help rather than add an infinite loop.

```python
def initialize_demo():
    publish_server_memory_sample("Demo.Level", 42.0, "Good")
```

### Build the first screen

1. Open Screens and create one for the intended device.
2. Add Text with Exercise level title.
3. Add Value Display and adjust position, size and decimals in Inspector.
4. Select Demo.Level in its data binding. Add a unit only if it matches the value meaning; it can remain blank here.
5. Preview, read diagnostics and apply the edit.
6. Configure supported presentation/navigation to open it. Do not add a write command to test a reading.

### Save, publish, activate and check

Open project lifecycle/revisions and check edits/validation. Save creates a persisted revision; select the correct one and Publish. Publication prepares it without automatically making it Active. Activate the published revision when this candidate should run in the test environment. Check result/Active revision. In TAG Monitor select Demo.Level and wait for 42/Good, checking timestamps and internal-memory source. Open the screen through Runtime and compare. Fixed screen text 42 with no TAG sample is not a passing exercise. Applying an edit to Working alone does not prove execution.

### If the result is missing

Check project/Active revision before changing the drawing. No sample: check source/TAG, declared reference, enabled Script, Initialize/handler and Script diagnostics. Valid TAG but blank screen: check opened screen, binding, object and formatting. View-only session is enough to observe; interactive actions require their own class/permission. If Save/Publish/Activate fails, read the message and current state before repeating; one completed stage does not imply the next. For physical equipment replace the exercise with its driver guide and verify value/quality independently. This example is not a field test.

[Open this guide in installed Help](/help?topic=getting-started.first-project)
