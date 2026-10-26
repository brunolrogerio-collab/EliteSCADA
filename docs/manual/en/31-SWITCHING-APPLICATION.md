# Switching the installation application

## Switch application: check, preserve and detach

Understand detach and each confirmation before returning this installation to neutral.

### When switching is appropriate

Open Engineering → Installation to prepare this installation for another application. It is not ordinary screen editing or activating the next revision of the same project. Detach unbinds application/Authority, invalidates old sessions and stops their Runtime effects. It neither uninstalls the product nor silently erases Historian. Licensing is an explicit choice. Operate only on the authorized destination and outage; reading this guide does not execute the switch.

### Check state and prepare copies

1. Review current Application, Working, Published, Active, Authority, license, Historian and engineering protection; refresh if state changed.
2. Save unsaved Working edits when needed before considering a copy recoverable. Saved-but-unpublished is different; compare revision numbers.
3. Export Application as .escadapkg and confirm download.
4. To retain users/credentials/policy, enter New backup password and Export Authority. Store password separately; the application package carries no such access.
5. Handle database/Historian and protected host-material backups separately; detach preservation is not a recovery copy.

### Summary and preservation

These values identify the current target; read them before confirmation.

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

### Read confirmations and licensing

Open Detach this installation application. Review each item deliberately; checking a box creates neither a backup nor a validated new environment.

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

### Execute and verify

After checking copies, consequences and license choice, choose Detach and return to neutral once and wait. The UI validates then requests detach; the server revalidates current state. Success returns to neutral entry. Confirm that state before creation/import/recovery. The next application/Authority require normal preparation; old sessions do not authorize the new binding. This guide adds no lock, transport or detach contract.

### Failure, interruption or uncertain state

Read blockers and refresh after resolution. Do not work around a rejected replacement license by removing a valid one as an experiment. If connection/page is lost during execution, assume neither success, cancellation nor rollback: reopen the correct address and check actual state. Record stage, message and time. Do not request another switch until reconciled. Recover application/Authority from copies using recovery Help; database/Historian use separate native procedures. Never replace these with manual database edits.

[Open this guide in installed Help](/help?topic=installation.switch-workflow)
