# Backup and recovery

## Backup and recovery: Authority, application and data

Separate required files, validate before applying and understand what each stage actually restores.

### Choose what must be recovered

Application means engineering: TAGs, screens, scripts and other portable .escadapkg objects. Security Authority holds users, credentials and access policy in a separate protected backup. Licensing is another artifact. Database, Historian and protected driver material follow their own procedures. An application file is not an installation image and contains no users/password hashes/login passwords. Having an .escadapkg alone does not recover access, history or driver keys.

### Prepare and check backups

Export the application through the supported flow and check project/revision and destination. Address unsaved edits before treating a copy as a recovery point. Switch this installation application offers Application backup and Authority backup before detach: export what is needed and return without executing the switch. For Authority, New backup password protects the exported file; it is not a login password. Store that password through a protected channel separate from the file and confirm download completion. Do not proceed with detach just to test a backup. Keep separate database/Historian and protected-key copies/recovery plans; this guide neither creates retention/archiving policy nor promises they are included in the package.

### Understand artifact fields

On an eligible neutral installation, Restore backup prepares Authority and application. If Authority already exists and only the application is missing, use Import application with the current Authority; do not force initial setup to bypass existing state.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Application package (.escadapkg) | Choose the exported application to recover. Check identity/compatibility during validation. | Exported application file | Selecting neither imports nor activates; filename/extension alone is not validity. |
| Authority backup (.json) | During recovery bootstrap select the protected Authority backup. | Separate Authority backup | Do not substitute edited JSON or an application package. |
| Authority backup password | Use the password chosen for that backup export. It opens the protected file, not a user session. | Separately stored password | 12 through 1024 characters. No sample password or file-edit recovery. Check origin/credential on rejection. |
| License (optional) | Select it if you also want to attempt installation after application recovery. | Compatible license file | Optional; license failure may leave the application recovered without the intended license. |
| Validate backups | Checks Authority backup and application before allowing Authority restoration. | Valid backups | Preview does not restore. Changed file/password requires validation again. |
| Restore Authority | Applies validated Authority when bootstrap is permitted. Then sign in with a restored Administrator. | Authority restored | Restored access is not an imported application; do not reuse old sessions. |
| Validate application | Checks the package against current state/Authority, including after Authority restoration. | Valid compatible package | Pre-restore validation does not replace the new validation. |
| Import application | Runs server-accepted recovery: replace Working, persist root revision, publish and activate through lifecycle. | Application imported, published and activated | Complete success requires every stage; an intermediate stage is not full recovery. |
| Continue recovery | Resumes after sign-in with files retained by the browser. | Already selected files | If the browser lost files, reselect and validate; shown names/sizes do not replace server checks. |
| Continue without license | Continues when the application recovered but optional license installation failed. | Separate license warning | Does not prove operating capacity/license; check Licensing and Runtime session afterward. |

### Recover Authority and application on a neutral installation

1. Confirm an eligible neutral installation and the correct destination server; do not detach merely to open this flow.
2. In initial setup choose Restore backup. Select application, Authority, backup password and optional license.
3. Choose Validate backups. Stop on rejection and correct file/compatibility/credential; extension acceptance is not validation.
4. With valid files, choose Restore Authority.
5. Sign in with a restored Administrator. The account must be enabled and hold effective engineering/administration permissions required for the package; the name developer alone is insufficient.
6. Continue recovery; the server validates the application again against restored Authority.
7. When validation permits, choose Import application and wait for complete results. Authority restore and application recovery are separate stages, not a promise of one transaction across every file.

### Recover only the application with existing Authority

If the installation has no persisted project but already has Authority, sign in with a compatible Administrator and choose Import application. Select the .escadapkg, Validate application, read the result and then import. The flow requires compatible package, appropriate destination project catalog/state and authorization; it is not generic overwrite of any running application. To modify an installed application use the corresponding engineering/portability flows or the specific switching procedure. Do not delete users/database to make the option appear.

### Interruption, partial failure and license

After Authority restoration the application may still be unrecovered. If browser memory retains files, continuation keeps the selection and prevents silent replacement. If the page lost files, reselect the requested artifacts; a new preview is mandatory before Apply. Retained selection is not a file backup. If the server returns an intermediate stage/error, record it and check persisted state before repeating: revision, publication and activation are distinct stages, and a completed stage must not be assumed rolled back. If the application recovered but optional license installation failed, read the warning and choose Continue without license where appropriate; address licensing separately rather than repeating the whole restore only for that warning.

### Confirm results and limits

Check authentication with expected Authority, active project/revision, screens, scripts and sources on the destination. In Runtime check session class, values, quality and timestamps. Database/Historian recovery remains separate through supported native procedures; current TAG values cannot recreate history. Protected driver material may require host preparation. Corrupt file, wrong password, disabled restored user, incompatible Authority reference or missing permissions must be resolved at the correct source. Never edit stores, tokens or signed files to force acceptance. Rehearse recovery in a controlled environment before relying on the copy for real operation.

[Open this guide in installed Help](/help?topic=recovery.backup-system-recovery)
