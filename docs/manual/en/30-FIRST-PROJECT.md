# First project: from sign-in to Runtime

## First project: from sign-in to the first reading

Create a test variable, display 42 on a screen and check that your application is working.

### What you will build

A small function puts 42 into a variable named Demo.Level. A screen displays it. This teaches the full path without connecting equipment. Use a test installation and an authorized account. If an application is already operating, confirm the environment with its owner before editing.

### Sign in and open the test project

1. Open the address supplied for the test installation.
2. On first access, follow initial Administrator setup. To recover an existing application, use Restore backup. Do not assume a default password.
3. Sign in. If Create New Project appears, enter key pilot-plant and name Pilot plant. The key identifies the project; the name is its displayed title.
4. Choose Create project and open Engineering. Check the opened name. If a project already exists or another user just created one, reload/check state rather than delete data to expose the option.

### Put a value in the variable

1. In Data Sources, prepare Server Memory. It stores the value on the server without an equipment address.
2. In TAGs, create Demo.Level of type Double and associate the prepared source. Double supports decimal numbers. Apply changes through the editor controls.
3. In Scripts, create an enabled Python Script running on the server: select Server. Declare Demo.Level as ServerMemoryTag, meaning a server-memory TAG.
4. In Script entries, choose Initialize and enter initialize_demo as function name. Initialize runs when the revision starts.
5. Paste the example and validate. It publishes 42.0 with Good quality, a valid known sample for this exercise. It publishes once at startup, not continuous updates or a sensor reading.

```python
def initialize_demo():
    publish_server_memory_sample("Demo.Level", 42.0, "Good")
```

### Show the value on screen

1. Create a Screen for the intended test device.
2. Add Text titled Exercise level.
3. Add Value Display, select it and set size, position and decimals in Inspector.
4. Select Demo.Level in its data binding. The binding makes it follow the variable; fixed text 42 does not test a reading.
5. Preview, check messages and apply the edit. Prepare navigation/opening for the selected presentation mode. No command is needed for this exercise.

### Run the prepared version

While editing, your work is a draft called Working. Save creates an identified copy, a revision. Publish prepares a revision for use. Activate runs the published revision; it becomes Active. In project lifecycle/revisions, Save, select the correct revision, Publish and Activate in the test environment. Wait for confirmation. In TAG Monitor check Active revision, Demo.Level and sample 42/Good. Then open the Runtime screen and compare. Fixed screen text 42 with no TAG sample is not a passing exercise.

### Look up fields when needed

Follow the exercise and return to this table when a field is unclear. Technical names identify fields; internal IDs are generated or chosen through the product’s selectors.

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

### If something is missing

Start in TAG Monitor. No Demo.Level sample: check source, enabled Script, declared reference and Initialize entry/function initialize_demo; read Script diagnostics. Correct TAG but incorrect screen: check screen and object binding. If Save/Publish/Activate was rejected, read the message and current state before repeating. View only is sufficient to observe. Later, use the matching driver guide and independent checks to connect real equipment.

[Open this guide in installed Help](/help?topic=getting-started.first-project)
