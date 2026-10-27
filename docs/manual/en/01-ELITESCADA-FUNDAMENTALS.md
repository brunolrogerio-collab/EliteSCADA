# SCADIVO — Fundamentals for getting started

## 1. Follow an application's path

SCADIVO lets you build screens to monitor and operate residential, building or industrial applications. Think of a pump: you want to see pressure, consult history and offer an action to an authorized operator. Each part has a purpose, and you can build them gradually.

In **Engineering**, you prepare connections, TAGs, screens and rules. In **Runtime**, a person uses the version placed into operation. Editing a drawing in Engineering does not immediately change the screen already operating.

You do not need to learn every term before starting. Build a small example first and consult the glossary in section 5 when a new word appears.

## 2. Make a first reading appear

1. Follow [First project](30-FIRST-PROJECT.md) to prepare a test application and known reading in Server Memory, the server's memory, without physical equipment.
2. A **TAG** identifies that reading in the project. Check its value, quality and time before drawing; [Engineering and data](03-ENGINEERING-DATA-SOURCES-TAGS.md) explains origin and address.
3. In [Screens, Popups and objects](05-SCREENS-AND-OBJECTS.md), insert a value display and select its TAG through a data binding. A Popup is a details window opened over a screen.
4. Check Preview and apply the edit to the project where that workflow is offered. Resolve diagnostics first.
5. Save work, publish and activate the revision as separate steps, explained below.
6. In Runtime, check the active revision and expected reading. Add alarms, history or Scripts after understanding this first path.

## 3. Know what each part stores

| Part | Purpose | Example check |
| --- | --- | --- |
| Application | Project engineering: TAGs, screens, Scripts and other portable objects. | Which project and revision are being edited or used? |
| Security Authority | Installation accounts, passwords, roles and access decisions. | Can this person only view or also perform the action? |
| License | Available commercial features and capacity limits. | Does the installation have a license compatible with the features used? |
| Historian | Stores and queries samples when capture is configured. | Does the requested period contain recorded data with suitable quality? |

A license grants no security permission. The server decides whether an action is authorized even when a browser button is visible. Session class and licensing may restrict features; neither grants a permission missing from Authority.

An `.escadapkg` moves the application; it is not an account/Authority backup, license or database/history backup. Those copies have separate procedures in [Backup and recovery](28-BACKUP-AND-RECOVERY.md). In current file-based workflows, licensing is bound to the destination machine; check its status in Licensing.

Screens, Scripts and reports use values supplied by Runtime. Do not create a private Driver connection inside them to bypass the source, authorization or project lifecycle. Scaling and access control do not gain a second implementation in the browser.

## 4. Edit now, operate when ready

Before editing, check the project, your Engineering permission and any requested editing control. The editable draft is **Working**. An identified saved copy is a **Revision**. **Published** means a published revision; **Active** is the revision Runtime executes.

1. Open Engineering in the correct project.
2. Edit the resource in Working.
3. Validate and run Preview where available. Preview checks the proposal without applying it.
4. Use Apply where offered to incorporate the validated edit into the project. Then save work with Save.
5. Create or select the corresponding revision and review its changes.
6. Publish the ready revision with Publish.
7. Use Activate when the change may enter operation. Check the server-reported active revision and the result in Runtime.

**Expected result:** the previous active revision stays in use until explicit activation. Saving does not publish; publishing does not activate. Apply in an editor or import is not activation either.

If a change seems to appear before activation, check which revision the server reports as Active and which area you are viewing. The editor's local appearance does not prove an operational change.

## 5. Look up terms when needed

| Product term | Meaning in your task |
| --- | --- |
| Engineering | Area for building and editing the application. |
| Runtime | Area presenting and executing the operating revision. |
| Working | Current project draft; it may contain unsaved changes. |
| Revision | Identified copy of saved work. |
| Published / Active | Published is prepared for activation; Active is the revision used by Runtime. |
| Data Source | Configuration of a value's origin, such as equipment connection or internal memory. |
| Driver | Communicates through an equipment protocol. Internal memory is a different source category. |
| Server Memory | Server memory retaining values; a screen can read them through TAGs. |
| Client Memory | Local state for this Runtime client; it may be lost when the context ends and is not automatically shared with other clients. |
| TAG | Identified project variable with type and origin. Its current value is not its entire history. |
| Binding | Connection between a property and a resource, such as a displayed value and its TAG. |
| Quality | Condition of a reading. Missing data or bad quality does not mean zero. |
| Timestamp | Time associated with a reading, helping identify whether it is fresh. |
| Script | Written rule performing a task on the server or client according to its chosen type. |
| Handler | Declared Script function receiving an event; its name must match the function you prepared. |
| Template Faceplate | Shared drawing associated with equipment. Changing its definition may affect linked instances. |
| Dynamo | Reusable visual composition with a shared definition and instance-specific values when parameterized. |
| Popup | Details window opened over a screen, with its own objects and bindings. |
| Equipment | Asset identity, such as Pump 01, separate from TAGs and the drawing representing it. |
| Preview / Apply | Preview checks a proposal; Apply incorporates validated content into the project. Neither is Activate. |
| Authority | Accounts and rules used by the server to decide who may do what. |
| Role | Permissions assigned to people. Check the resources and actions actually permitted. |
| Capability / permission | An allowed action; writing a value and executing a command are distinct actions. |
| Scope | Resource or application part to which a permission applies. |
| Engineering Lock / application secret | Optional Engineering content protection. Its secret differs from login and Authority backup passwords. |
| Editing reservation | Control of who may edit at a given time, where required. It does not replace account permissions. |
| Historian | Sample recording and querying; requires capture and available data. |
| License | Commercial use/capacity conditions, separate from an account and its roles. |

## 6. Continue with your task

Use the [English index](README.md) to find chapters by task. In installed Help, search for a function, field or command name. Detailed references explain choices after the initial procedure.

If a value is missing, check source, TAG, binding and active revision. If an action is rejected, read the result and check session/permission; do not repeat an uncertain command without checking its state. For missing history, check capture and period, not just the current value.

**Related Help:** `/help?topic=getting-started.first-project`, `/help?topic=engineering.lifecycle`, `/help?topic=bindings-commands.overview`, `/help?topic=recovery.backup-system-recovery`.
