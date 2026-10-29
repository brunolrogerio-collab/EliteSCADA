# Backup and recovery

## Backup and recovery: Authority, application and data

Separate required files, validate before applying and understand what each stage actually restores.

### Choose what must be recovered

Before restoring, identify what was lost and which copies you have. The .escadapkg application carries engineering: TAGs, screens, Scripts and other portable objects. Security Authority keeps users, passwords and access policy in a separate protected backup. License is another file.

Database, Historian history and driver keys/protected material have their own procedures. The application package is not a full installation copy and contains no users, password hashes or login credentials. A .escadapkg alone does not guarantee recovery of access, history or driver keys.

### Prepare and check backups

Export the application through the supported flow and check project, revision and file destination. Resolve unsaved changes before treating the copy as recoverable. Wait for download completion.

Switch this installation’s application offers Application and Authority backups before detach, the unlinking step. You can export and return without switching. Do not detach just to test a backup.

New backup password protects the exported Authority file; it is not the login password. Store it through a protected channel separate from the file. Also prepare database/Historian and driver-key copies/recovery. This flow does not set retention or include Historian archive files. To archive and restore raw samples, use Archive and restore samples as described in [Historian capture and query](07-HISTORIAN-CAPTURE-QUERY.md); those files are managed separately by the API.

### Recover Authority and application on a neutral installation

1. Confirm an eligible neutral installation and correct destination server. Do not detach merely to open this flow.
2. In initial setup choose Restore backup and select application, Authority, backup password and optional license.
3. Use Validate backups. If refused, correct file, compatibility or credential. A file extension does not replace validation.
4. With valid files, use Restore Authority.
5. Sign in with an enabled restored Administrator having the effective permissions required for the package. The developer name alone is insufficient.
6. Continue recovery. The server validates the application again using restored Authority.
7. When allowed, use Import application and wait for the full result. Authority restore and application recovery are distinct stages, not a promise of one transaction across all files.

### Recover only the application with existing Authority

If the installation has no persisted project but already has Authority, sign in with a compatible Administrator and choose Import application. Select the .escadapkg, Validate application, read the result and then import. The flow requires compatible package, appropriate destination project catalog/state and authorization; it is not generic overwrite of any running application. To modify an installed application use the corresponding engineering/portability flows or the specific switching procedure. Do not delete users/database to make the option appear.

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

### Interruption, partial failure and license

Restoring Authority does not mean the application is already recovered. If the browser retains selected files, continuation preserves that selection and prevents silent replacement. If files were lost, select requested files again and make a new preview before Apply. Remembering a selection is not a file backup.

For an intermediate stage/error, record the result and check stored state before repeating. The flow passes through revision, publication and activation; do not assume a completed stage was undone.

If application recovery succeeded but optional licensing failed, read the warning. Use Continue without license when appropriate and handle licensing afterwards rather than repeating the entire restore for that warning.

### Confirm results and limits

Check authentication with expected Authority, active project/revision, screens, scripts and sources on the destination. In Runtime check session class, values, quality and timestamps. Database/Historian recovery remains separate through supported native procedures; current TAG values cannot recreate history. Protected driver material may require host preparation. Corrupt file, wrong password, disabled restored user, incompatible Authority reference or missing permissions must be resolved at the correct source. Never edit stores, tokens or signed files to force acceptance. Rehearse recovery in a controlled environment before relying on the copy for real operation.

[Open this guide in installed Help](/help?topic=recovery.backup-system-recovery)
