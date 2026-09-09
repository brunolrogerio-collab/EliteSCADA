# EliteSCADA — Codespaces Preview reopen regression — 2026-09-09

## Purpose

This note supplements `docs/CODESPACES-PREVIEW-RUNBOOK.md` with one concrete historical failure mode discovered while reopening an existing Preview Codespace.

It is **not** an instruction to reuse that historical Codespace for the post-C26 Work audit. The historical environment was used only as evidence for a reopen/resume defect that the new Preview infrastructure must not reproduce.

## Historical observation

A previous Codespace started correctly on its first execution and loaded the EliteSCADA application. On a later reopen, the Preview was no longer usable without recovery.

The built-in Codespaces agent reported that recovery had required actions equivalent to:

- restart the API on internal port `5080`;
- restart the Web frontend on port `5173`;
- revalidate database/package state and the local `EliteSCADA` identity;
- revalidate/import/save/publish/activate the `eee-demo` project state as needed;
- reopen the forwarded Web port `5173` in the Codespaces Ports panel.

That sequence is accepted as historical diagnostic evidence only. It must **not** become the normal operator procedure for the new Preview.

The important product-infrastructure lesson is that a Codespace reopen can leave stale or incomplete transient process state even when the repository and persistent disposable database state are still usable.

## Required behavior for the post-C26 Preview

For a fresh post-C26 Codespace and every later attach/reopen of that same Codespace:

1. `postAttachCommand` must automatically restore a healthy Preview without mandatory manual terminal commands;
2. API remains fixed to `5080` and internal-only;
3. Web remains fixed to `5173` and is the only configured forwarded port;
4. stale Preview-owned API/Web processes must be removed before restart;
5. simultaneous/repeated attaches must not race for ports or ephemeral authentication state;
6. Vite must not silently escape to `5174` or another fallback port;
7. unexpected ports must not be auto-forwarded;
8. restart must preserve the accepted frozen package and Active revision rather than rebuilding a different application state;
9. health must be reproved after the restart;
10. final Work owner acceptance still requires a fresh Level-D Codespace under the runbook.

## Repository implementation

The reopen hardening was introduced on the post-C26 Preview branch by commit:

`4f11c0e2e030378cf8ad9a2828f5ce10466ce804`

`fix(preview): harden Codespaces reopen on fixed ports`

The implementation:

- changes `.devcontainer/devcontainer.json` so `postAttachCommand` invokes `scripts/preview/ensure-post-c26-preview.sh`;
- keeps only port `5173` in `forwardPorts`;
- keeps `5080` explicitly ignored for auto-forwarding;
- ignores unexpected auto-forwarded ports through `otherPortsAttributes`;
- serializes attach/recovery with a file lock;
- removes Preview-owned stale API/Web processes before launch;
- requires ports `5080` and `5173` to be free before starting;
- invokes the canonical `launch-post-c26-preview.sh` rather than bypassing lifecycle/auth/licensing;
- fails if the Web process attempts to migrate away from `5173`;
- verifies API and Web health after attach/recovery.

The `Post-C26 Canonical Preview` workflow was also extended so the same disposable state is launched a second time, simulating a Codespace reopen. The second launch must preserve the Active revision and frozen package marker and remain healthy on fixed ports `5080/5173`.

## Acceptance consequence

A new Codespace must not be declared ready merely because the first boot works. The reopen regression must also be green on the exact Preview head, and the real fresh Codespace must demonstrate that reopening it does not require the manual recovery sequence described above.

The historical Codespace name and its forwarded URL are intentionally not recorded as candidate acceptance data because that environment is not being reused for the post-C26 audit.
