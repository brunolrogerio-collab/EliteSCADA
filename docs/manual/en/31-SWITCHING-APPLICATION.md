# Switching the installation application

## Switch application: check, preserve and detach

Prepare copies and understand the effects before removing the current application from this installation.

### What this operation does

Use Engineering, Installation when preparing this server for another application. It removes the current application binding, invalidates old access and returns to initial setup. That application stops operating on this server. The product stays installed and this flow does not erase Historian. To edit a screen or run another revision of the same project, use project lifecycle instead.

### Prepare copies before deciding

1. Check Current Application, Working and Active to confirm the correct installation.
2. Check unsaved changes and save a revision if needed to retain them; saving does not start running those edits.
3. Export Application and confirm the .escadapkg download; it contains application engineering.
4. To retain users/permissions too, Export Authority. Authority stores that access; its backup is separate and uses New backup password. Keep password separately.
5. Confirm database/history/driver-key copies with the owner; this screen does not include all of them in the application package.

### Confirm and wait

1. With copies checked, open Detach this installation application.
2. Read confirmations about application, users/sessions, history and unsaved work. Confirm only what you understand and chose.
3. Choose machine license handling: Keep retains it, Remove removes it, Replace requires a valid new code. This choice is separate from the application.
4. Resolve any blockers and refresh state.
5. Choose Detach and return to neutral once and wait. The server checks state again before applying. Success returns to initial setup. You must still create, import or recover the next application.

### Look up summary and copy fields

Use this table when a preparation field is unclear. Exporting a copy does not require executing the switch.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Current Application | Project to be unbound from this installation. | Current project | Not selection of a remote server or new project. |
| Working | Revision and unsaved edits. | Unsaved changes | Save does not publish/activate; export the needed copy. |
| Published and Active | Compare prepared/running revisions to understand preservation/shutdown. | Shown revisions | Do not assume Working exactly matches Active. |
| Security Authority | Identity/policy binding to detach; old sessions become invalid. | Present Authority | Separate backup; .escadapkg does not recover login passwords. |
| Historian | History/database is preserved by this flow. | Preserved | Not data export or guaranteed automatic compatibility/attachment of the next application. |
| Engineering protection | Current protection and conditions requiring authorized resolution. | Protection state | Do not remove protection/credentials to bypass a blocker. |
| Export Application | Creates a portable engineering copy through the supported flow. | .escadapkg download | Check file/revision/destination; cancelled download is not a completed copy. |
| New backup password / Export Authority | Exports protected Authority with its own password. | Completed separate backup | 12–1024 password characters; never publish file/password or confuse with login. |

### Look up confirmations and licensing

Confirmations ensure you understand what is affected. Checking a box creates no copy. If a message is unclear, return to preparation and check before continuing.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Backup decision | Confirms export for this revision or an explicit choice to continue without a copy. | Backups exported | Skipping export accepts the missing copy; not automatic backup. |
| Loss of unsaved Working | Required when unsaved edits exist and you proceed. | Acknowledged consequence | Does not save edits or guarantee recovery. |
| Detach Application | Acknowledges the current application stops operating bound to this installation. | Reviewed consequence | Not simply closing the UI. |
| Detach Authority and sessions | Acknowledges unbinding and invalidating prior session authorization. | Reviewed consequence | New/restored identities/policy follow bootstrap/recovery flows. |
| Preserve Historian/database | Acknowledges separate historical data and absence of silent deletion. | Data preserved | Not migration, disposal or historical restore. |
| Machine license | Keep retains current license; Remove deliberately removes it; Replace requires a valid candidate. | Keep license | Installation-owned, not embedded in application. Check post-action result. |
| Replacement license code | Fill only for Replace; candidate is validated before detach. | Code from authorized flow | Not a cryptographic key; correct an invalid candidate before proceeding. |
| Blockers and diagnostics | Read rejection reasons/stage and revalidate after addressing cause. | Displayed blocker | Do not bypass disabled buttons through store edits or repeated requests. |
| Detach and return to neutral | Submits the decision; server previews/revalidates before applying. | Neutral installation | State changes between stages can cause rejection. |
| Cancel / Back | Returns to preparation before confirming execution. | Review copies | Does not promise undo of an already received/completed server operation. |

### Connection loss or error

Do not immediately repeat. Reopen the correct address and check whether the old application remains or initial setup is shown. Connection loss alone does not tell you whether the switch finished. Record message/stage/time and ask for state confirmation if uncertain. Cancel/Back before confirmation does not undo an already completed server operation. Correct an invalid replacement license rather than remove the valid one experimentally. Use Backup and recovery to restore copies.

[Open this guide in installed Help](/help?topic=installation.switch-workflow)
