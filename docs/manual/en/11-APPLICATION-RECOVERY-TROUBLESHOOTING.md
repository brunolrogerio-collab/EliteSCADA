# Application Management, Recovery, and Troubleshooting

## Export .escadapkg

Export an `.escadapkg` to keep or move application configuration: TAGs, screens, Scripts and other portable objects. Wait for the download to finish and identify its project/revision. To edit many rows of one kind, use the CSV/Excel chapter; to share only reusable visual resources or Scripts, use Libraries.

The package does not contain the Authority user/permission backup, license or database/history. Prepare those copies separately when recovering a complete installation.

At the destination, **Inspect** examines content and information without applying changes. **Preview** checks proposed content and shows diagnostics. **Apply** incorporates the validated candidate into the project draft, called Working. Then save, publish and activate separately; seeing imported content in the editor does not mean that revision is operating.

**Related Help:** `/help?topic=packages.escadapkg`.

## Import

### Procedure: safely import an application

**Goal:** check a received package and incorporate it into the project before putting it into operation.

**Prerequisites:** a compatible package and an authorized session.

**Steps:**
1. Select the `.escadapkg`.
2. Run Inspect when available.
3. Run Preview.
4. Correct reported issues.
5. Run Apply only for the validated candidate.
6. Save the resulting application.
7. Publish and activate separately when appropriate.

**Expected result:** Working receives the imported content; Published/Active do not change implicitly.

**Troubleshooting:** if Working changes after Preview, run validation again before Apply.

## Reusable libraries

`.escadalib` differs from `.escadapkg`.

Associating a library makes compatible resources available in the catalog. Using/incorporating copies the selected item and its validated dependencies, the other resources needed for it to work, into the project draft called Working.

Unassociating removes availability from the catalog and does not delete content already incorporated. Runtime and Active do not depend on an external `.escadalib` file.

**Related Help:** `/help?topic=libraries.reusable-resources`.

## Authority backup

An Authority backup preserves supported local identities and assignments in a protected envelope. It does not include plaintext passwords, tokens, cookies, sessions, a license, the application, or Historian data.

The Authority backup password is exclusive to that artifact and is not a login or Engineering Lock password.

## Restore-first

A truly empty installation offers Restore backup before requiring creation of a disposable user/project.

Recovery preserves separate authorities:

1. application (`.escadapkg`);
2. Security Authority;
3. database/Historian;
4. Licensing as an optional, separate concern.

### Procedure: full recovery on an empty installation

**Goal:** restore Authority and the application without creating a temporary identity/project.

**Prerequisites:** installation confirmed to be empty; Authority backup and password; `.escadapkg`; optional license.

**High-level steps established by the contract:**
1. select the artifacts;
2. run Inspect/Preview on all inputs before mutation;
3. revalidate that the installation is empty;
4. restore Authority atomically;
5. apply the validated package to Working;
6. save the root revision;
7. publish;
8. confirm that the Runtime-configured project key matches the restored application;
9. activate through the normal operation;
10. install the optional license through the Licensing service;
11. validate the final invariants.

**Expected result:** usable Authority exists, the application is saved/published/Active, and Runtime is consistent with the Active revision.

**Important:** database/Historian recovery is a separate authority and its state must be explicitly known.

## Application detach / neutral installation / A -> B -> A switching

Engineering > Installation detaches the current Application and Authority and returns the installation to a neutral state for a new bootstrap. It is not project deletion, product uninstall, or automatic license removal.

### Procedure: safely switch the Application

**Prerequisites:** authorized access to installation administration; a decision about what to preserve; Application and Authority backups when they are needed for rollback.

**Steps:**
1. Review the installation summary, including any unpublished Working changes.
2. Export the Application as `.escadapkg`. If local users/identities must be preserved, export Authority separately using the password requested by the page. `.escadapkg` does not contain credentials.
3. Open **Detach application from this installation** and read the consequences. Confirm the acknowledgements shown for Application, Authority, old sessions, Historian, and Working changes.
4. Decide whether to keep, remove, or replace the machine license. A candidate replacement is validated before detach.
5. Read and resolve blockers. If you choose to continue without a recommended backup, acknowledge that explicitly in the page.
6. Select **Detach and return to neutral** and wait for the server transaction. After a failure, refresh and inspect the actual state before retrying.
7. In neutral bootstrap, bind the next Application and configure its Authority through the normal workflow.

**Expected result:** the old Application and Authority are no longer bound, and old tokens/sessions no longer authorize actions; the installation returns to neutral bootstrap. Historian is preserved; the database and its backups remain a separate concern. The license follows the confirmed choice.

**Related Help:** `/help?topic=installation.switching`, `/help?topic=application.export-import`, `/help?topic=recovery.backup-system-recovery`, `/help?topic=getting-started.neutral-bootstrap`.

## Troubleshooting workflow

Use this same order before creating a workaround:

1. reproduce the problem;
2. identify the authority/layer that should provide the state;
3. compare backend and UI;
4. validate identity/capabilities;
5. validate lifecycle/Active;
6. for data, validate Data Source -> Driver -> binding -> quality/timestamp;
7. for history, validate ingestion before presentation;
8. for Scripts, validate reference, sandbox, timeout, and fault isolation;
9. for Licensing, distinguish Demo, Valid, and Invalid;
10. for recovery, identify the failed stage and do not declare partial success to be complete success.

### Startup

Confirm backend health and UI availability. On an empty installation, distinguish creating a new installation from Restore-first.

### Login

Login failure should be handled in Authority. Engineering Lock and the license are not login credentials.

### Driver communication

Use only capabilities declared by the Driver and check configuration, endpoint, connection, binding, and diagnostics.

### TAG quality

Do not force Good. Correct the source of the quality state.

### Runtime

Confirm the Active revision, entitlement, and session authority.

### Scripts

Check declared dependencies, public API, sandbox, and instance diagnostics.

### Licensing

- no license: Demo;
- valid license: tier entitlement;
- license present but invalid: Run blocked.

### Authority

Do not restore tokens/sessions. After restore, restored identities authenticate through the normal mechanism.

### Import/restore

Preview is required before Apply where the workflow offers it. Apply does not replace Save/Publish/Activate.

**Related Help:** `/help?topic=recovery.backup-system-recovery`, `/help?topic=troubleshooting.overview`, `/help?topic=diagnostics.overview`.
