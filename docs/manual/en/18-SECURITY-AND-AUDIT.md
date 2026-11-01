# Security and Audit: accounts, permissions and protection

## Local users: create, assign roles and maintain access

Register who signs in and distinguish account, role, session and license.

### Before administering accounts

Open Engineering > Security to register who signs into this installation. Local accounts, passwords and permissions belong to Security Authority, the installation’s security store. They are not project objects or part of the engineering package. A local account is also different from a commercial portal account.

Managing users requires UserRoleAdmin or SystemAdmin. Editing the project does not grant that access. Before changing roles or passwords, check your current account and preserve at least one enabled local administrator.

### Local session duration

In Security, set when the product should ask the user to sign in again. The timer starts at login and sets the maximum lifetime of a local login; it is not an inactivity timer. Closing the browser does not end the session. The new duration applies to future logins, while open sessions keep the expiry they already received. Choose 5 to 1,440 minutes (5 minutes to 24 hours); the product's initial value is 480 minutes, or 8 hours, unless the installation defines a different initial value in server configuration.

If the duration expires while the application is open, it does not automatically show the sign-in screen. The server stops accepting protected actions and ends live updates. The screen may continue to show the last values it received. Refresh the page or reopen the application to return to sign-in. When handing over a station, choose Sign out first and have the next operator use their own account.

### Procedure: create and test an operator

1. Check available roles, which actions each allows and for which resources. If a suitable role is missing, prepare and apply policy first.
2. Use New user and fill login, name, private password and enabled state.
3. Select only needed roles and create the account.
4. For later edits, review the summary and confirm saving. Changing a field does not save by itself.
5. In another session, sign in with the new account and test one allowed reading and one action that must be denied. License and View Only/Interactive class still limit actions.
6. Return to the administrator account to correct grants if needed. One session does not prove access for two different users.

### Account fields

Use New user to create an identity. For an existing account, select it in the list; its login name is shown for identification and is not renamed by this form. Password is entered during creation or in the separate reset form, without exposing the current password.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Username / login | Name used to sign in. Do not confuse it with the readable display name. | operacao.turno1 | 3–200 characters; unique username. |
| Display name | Identifies the person readably. Changing it changes neither login nor roles. | Operations — Shift 1 | 1–300 characters. |
| Password / New password | Create an individual password and follow the policy accepted by the server. Reset replaces the password rather than recovering the previous one. Never copy passwords into messages, scripts or packages. | Enter privately | The form requires at least 8 characters; the server validates the policy. |
| Enabled | Allows account use. Disabling deletes neither identity nor action history. Changes can invalidate the session; keep another administrative account available. | Checked |  |
| Roles | Select existing roles whose permissions match the work. Grants from assigned roles combine; adding a role removes no permission from another. Naming a role developer or administrator creates no privilege by itself. | Station 01 operator | Check the grants, not just the name. |

### Reset, search and common problems

Search filters name/login/role, and status separates enabled and disabled accounts; hiding a row does not disable an account. To reset a password, select the account, enter New password, review the impact and confirm. Account/role or password changes can end sessions; sign in again when requested. The system rejects removing the last enabled local administrative access and assigning nonexistent roles. A 401 calls for signing in again; 403 indicates insufficient permission. A project revision does not carry users, and importing .escadapkg does not restore Authority.

[Open this guide in installed Help](/help?topic=security.local-accounts)

## Roles and scopes: grant the action on the right resource

Build explicit grants, check their reach and apply the validated Authority policy.

### Account, role, capability and scope

Start with who can do what and where. The account identifies the person. A role gathers permissions assigned to an account. Each grant connects an allowed action to all resources or a specific scope. The interface calls the action a capability and its reach a scope.

Open Engineering > Security and the roles/capabilities/scopes panel. Policy belongs to the installation’s Authority, not the drawing or project package. A role name alone grants no access.

Viewing requires UserRoleAdmin. Validating/applying requires EngineeringModify together with UserRoleAdmin or SystemAdmin. Check these permissions before editing policy.

### Procedure: prepare and apply a policy

1. Load current policy and check who will keep administering the installation.
2. Create a role or select one. Before changing it, check which users already receive it.
3. Add a grant for each needed action. Choose global or the correct hierarchy node. If including descendants, check which resources become covered.
4. For Intelligence, explicitly add AssetProcessIntelligenceManage: global or Equipment/Location with a valid resource link. SystemAdmin alone does not replace that grant when configuring tools.
5. Run policy Preview, correct errors and apply only the validated candidate. A new edit invalidates the previous preview.
6. Assign the role and test allowed/denied access with that account.
7. If another administrator changed policy, reload and redo against the current version. Do not force an old version or delete users to bypass conflict.

### Role and grant fields

New role creates a draft. Duplicate copies settings into another role, not users. In the displayed hierarchy, IDs identify existing nodes/resources; this panel lets you select nodes but provides no form for creating or rearranging the hierarchy. Do not invent a UUID to replace a missing node.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Stable identifier | Key used in user assignments. Edit before first Apply. It is protected afterward; changing Display name does not change the key. | operador-estacao01 |  |
| Display name | Readable label for selecting and recognizing the role. Not its permission list. | Station 01 operator |  |
| Description | Explain purpose and intended reach to prevent later mistakes. | Read the station and acknowledge its alarms |  |
| Capability | Select the specific action. Use the reference below; similar names do not make capabilities interchangeable. | AlarmAcknowledge |  |
| Scope | Unscoped grants this capability globally. Selecting a node restricts that grant to the corresponding resource. Global does not mean all capabilities. | Station 01 node |  |
| Include descendants | With a selected node, includes its descendants in the authorization hierarchy. Unchecked covers only that node. It is not a name/path prefix search. | Station and descendant equipment | Disabled without a node. |

### Available action reference

These are the options shown in the current version. Permission creates no resource, changes no license and bypasses no active revision, readiness, context or operation limit.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| View | View Runtime; grants no commands. | View |  |
| TagRead | Read TAGs for the authorized resource/scope. | TagRead |  |
| CommandExecute | Execute registered commands. Distinct from writing a process value. | CommandExecute |  |
| ProcessValueWrite | Request a process-value write, such as a setpoint. Does not replace command permission. | ProcessValueWrite |  |
| AlarmAcknowledge | Acknowledge alarms; does not fix the physical cause. | AlarmAcknowledge |  |
| AlarmShelve | Shelve/unshelve alarms through an available operation, subject to the definition. | AlarmShelve |  |
| TrendUse | Use authorized queries/trends. | TrendUse |  |
| TrendSave | Save trend settings where available; grants no general project editing. | TrendSave |  |
| EngineeringView | Read authorized Engineering. | EngineeringView |  |
| EngineeringModify | Modify Engineering subject to Engineering Lock and operation-specific requirements. | EngineeringModify |  |
| UserRoleAdmin | Administer users/roles through authorized flows. Policy mutation also requires Engineering editing. | UserRoleAdmin |  |
| SystemAdmin | Administer system operations requiring this capability, such as querying Audit. Do not assume it replaces every specific capability. | SystemAdmin |  |
| HighAvailabilityObserve | Observe HA status. | HighAvailabilityObserve |  |
| HighAvailabilityTransfer | Request HA authority transfer subject to readiness and permissions. | HighAvailabilityTransfer |  |
| HighAvailabilityAdmin | Administer HA settings. An operation may require other Engineering permissions. | HighAvailabilityAdmin |  |
| AssetProcessIntelligenceManage | Manage Intelligence with an explicit global grant or a valid Equipment/Location restriction. License and activation remain necessary. | AssetProcessIntelligenceManage |  |

### Preview, session and rejection

The selected account’s preview shows what roles in the draft appear to grant. It helps review the edit but does not decide access for a concrete resource. Displayed effective capabilities belong to your current session, not the selected person.

The server makes the final decision using person, resource and scope. Runtime also considers session class and license. Policy has its own Authority version and Preview/Apply; exporting or activating a project does not replace it.

The product refuses deletion of an assigned role or loss of the last administrator. If a node is missing or incompatible, choose a valid link. Do not broaden to global merely to remove an error.

[Open this guide in installed Help](/help?topic=security.policy-scopes)

## Audit: find actions and check recording health

Use appropriate filters, read outcomes and distinguish an empty list from persistence failure.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-AUDIT: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=audit.query-diagnostics; fields=audit.retention; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### What Audit answers

Open Audit in the system. Access requires SystemAdmin in the authorized Runtime. Audit records protected actions and outcomes: who attempted which operation on which resource and when. Administrative success does not prove a pump physically started; inspect command feedback and TAGs. Audit does not replace alarm.events, operational.events or Historian readings. This screen queries/diagnoses and does not edit occurrences.

### Query fields

Leave optional filters empty to broaden the query. Filled filters combine: a record must satisfy all of them. Values are exact, not free-text fragments. Copy values from a known event instead of guessing IDs. Editing filters does not query until Query is selected.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| From | Start in browser local time, converted to UTC for querying. Empty means no lower time bound. | Incident day, 08:00 | Must not be later than To. |
| To | End of period; the entered instant is included. If an event is missing, check timezone and period before assuming loss. | Same day, 09:00 |  |
| User / subject | Stable actor identity recorded in the event, not necessarily display name or login. Copy it from event details. | Observed identity ID |  |
| Action | Exact recorded operation name. Start with period/outcome if you do not know it. | command.execute |  |
| Outcome | All leaves outcome unrestricted. Succeeded means recorded as successful; Denied is an authorization rejection; Failed is an unsuccessful operation. | Denied |  |
| Resource kind (advanced) | Target category, not the action name or TAG type. | command |  |
| Resource / key (advanced) | Exact target identifier used by the event. May be a key/path or ID depending on the operation; it is not always a UUID. | pump.start |  |
| Area (advanced) | Area recorded in action context. A filter does not add an area to an event lacking one. | Estacao01 |  |
| Correlation ID (advanced) | Links records from the same request/flow when correlation is provided. Not the individual event ID. | Copy from a known event |  |
| Events per page | Limits records shown on each page. Changes neither retention nor total stored records. | 100 | Integer from 1 to 1000. |

### Procedure: investigate a denial

1. Choose a short period around the known attempt and Outcome = Denied.
2. Select Query. Select a row for details; keyboard and Enter also select.
3. Read identity, action, target and reason metadata; do not assume the license caused a denial.
4. Copy action/identity/target from details to refine filters if needed.
5. Check roles, grants and scope with Authority administration. Do not alter history to try granting access.
6. Use Next/Previous for pages with the same filter. Changing filters and selecting Query returns to page one.
7. Clear removes filters and queries again; it deletes no events. Refresh reloads the current page and diagnostics.

### Diagnostics: read-only values

Expand Diagnostics. A temporary queue does not prove loss, but failures/rejections/drops require investigation. Query health and recording health are different. These indicators are read-only: this screen has no retention policy editing fields.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Persisted / last persisted | Store count and last write time reported by the service; not just records on the current page. | Check count and time together |  |
| Append failures | Indicates storage-write failures; inspect service diagnostics and database availability. A historical count does not prove a current failure. | Compare count/last failure |  |
| Pending queue | Records awaiting buffer forwarding. Check whether the queue decreases after service recovery. | 0 when idle |  |
| Forwarded / forward failures | Track buffer output and forwarding failures; distinct from query counts. | Check last forwarded time |  |
| Rejected by limit / dropped on shutdown | Report records rejected by capacity or dropped during shutdown. Do not treat a queryable list as proof of completeness when these occur. | Investigate nonzero counts |  |
| Retention / last retention | Shows enablement, finite/indefinite condition and execution of cleanup configured in the service. Indefinite means no active finite age, not unlimited disk. Backup and archiving are separate procedures. | Inspect diagnostics; do not enter a value |  |

### No results or no access

An empty list may mean no filter match, wrong period/timezone or absent recordings. Clear filters, check time and inspect diagnostics before concluding loss. A full page with no Next available does not prove no events exist outside the filter. A 401 calls for sign-in; 403 requires permission; 400 means an invalid filter. Pagination uses internal references supplied by the server: there is no cursor to enter in the form. This screen does not configure global database size or archiving; do not confuse retention diagnostics with that administration.

[Open this guide in installed Help](/help?topic=audit.query-diagnostics)

## Engineering Lock: protect and reopen the application

Use the application secret without confusing it with a user password, backup or permission.

### When to use it and what it protects

Use Engineering Lock to protect the application’s engineering content. Its secret unlocks that protection but grants no missing permissions. It differs from the editing reservation used when someone works on the project.

Store each secret for its purpose: account password identifies the person; Lock secret protects the application; Authority-backup password protects that file. Put none in screens, Scripts or Help. Administration, import/export, restore and unlocking still require normal permissions even while locked.

### States and actions

In unlocked Engineering, open the padlock control in the top bar. When locked, the restricted surface presents the unlock form and available administration. Protected editors are not left open behind a simple message.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Secret configured | Indicates a secret exists. Configured does not necessarily mean currently locked. | Configured and unlocked |  |
| New secret / Replace secret | Enter privately to configure or replace. The current secret is not displayed. Configure keeps Engineering open; Configure and lock closes protected Engineering. | Use an application-specific secret |  |
| Lock now | Uses the configured secret and switches to the restricted surface. Sends no stop command to Runtime. | After configuring the secret |  |
| Engineering Lock secret / Unlock | Enter the application secret to reopen content. Knowing a user password does not replace it; unlocking grants no missing permission. | Enter without sharing in messages |  |
| Remove secret | Removes configured protection through the permitted flow. Neither resets a user password nor deletes the project. | Only when protection removal is intended |  |

### Procedure and persistence after restart

1. With authorized access, open the padlock control.
2. Set the private secret. Use Configure to keep working or Configure and lock to restrict immediately.
3. Check the response and state returned by the server.
4. Complete Save → Publish → Activate. Configuration changes the Working draft; this cycle puts protection in the active revision that must remain after restart. Configure alone does not complete it.
5. To edit later, use Unlock with the correct secret and appropriate permissions.
6. Check available content and protection in the revision you will publish. If refused, read the reason. Do not clear users, database or Authority to try unlocking.

[Open this guide in installed Help](/help?topic=security.lock-workflow)
