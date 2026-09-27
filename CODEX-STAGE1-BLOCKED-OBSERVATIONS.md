# W15 First-Project CODEX Stage 1 — Embargoed Record

**Gate:** `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`  
**Coarse disposition:** `BLOCKED_BY_ENVIRONMENT`  
**Embargo:** Active until Main declares the independent Human Preview complete or blocked.

## Provenance

- Product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`
- Harness branch: `preview/w15-first-project-env-harness`
- Harness SHA/tree: `bb451fa6e07982ac12384895f6097d5833761d16` / `650d30089596021cb1a564ee1d2f1abfc7d2b509`
- Audit session: `6f18039a-5e67-49bd-a566-2050cf775aee`
- Dependency preparation: `READY`, key `0d074f1bd0f1702a5313651791dd7ce6aa1996821f312ca45a7d6761f9b0794e`
- User journey used the product UI at `http://localhost:5173/`.

## Black-box observations

1. The clean session opened at the product's initial Administrator setup.
2. After the user completed that setup, the product displayed its no-project first-project form.
3. The form visibly offered a project key, project name, a backup-restore button, and a create/open-Engineering button. No project, imported package, Demo, or EEE state was introduced by the harness.
4. I entered a simple project key/name and activated the visible create/open-Engineering control through the browser UI. The page changed to the `Restaurar backup` surface. That surface asked for an `.escadapkg`, described validating the package with restored Authority before creating a project, and showed validation/restore actions disabled until a package was supplied.
5. I did not upload or restore any file. I did not create a project or reach Engineering/Runtime. I did not use hidden state, source, APIs, fixtures, or shell mutation to advance the journey.
6. The visible `Voltar` control did not respond to the initial accessibility/coordinate click attempts; activating it with Enter returned to the preserved first-project form. Re-activating the create/open-Engineering control returned to the same backup-restore surface.

## Post-block diagnostic correlation

After ending the exploratory journey, I inspected the accepted-base UI code and container logs as permitted for diagnostic correlation. The first-project form is wired to submit a project-creation request; the backup-restore control is a separate button. The logs show the initial Administrator bootstrap succeeded, but no first-project creation request was received. This leaves a material ambiguity: the observed transition to backup restore may reflect browser-control input targeting the wrong button rather than confirmed product behavior. I therefore do **not** classify this as a product defect.

No product code, tests, workflow, project, package, or configuration was changed. Stage 1 remains blocked at the first-project boundary; Stage 2 did not start. The audit session was not reset. Dedicated product containers remain running pending Main's direction.

## Evidence limits

- The browser accessibility tree and visible page were captured in the active UI session; no screenshot file was persisted to this branch.
- No stable product-level project-creation result was observed, so functional Runtime was not reached.
- Reclassify only after the create action can be reliably activated through ordinary user-visible browser interaction or Main directs a replacement audit session.
