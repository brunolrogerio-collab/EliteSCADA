# Coordinator Duty — One-Click GitHub Codespaces Creation

## Purpose

This document makes Codespaces creation automation a permanent coordinator responsibility for EliteSCADA Preview/homologation flows.

GitHub live remains the official memory and sole authority. Before producing or changing any Codespaces creation handoff, the coordinator must revalidate the live repository, Preview PR, branch HEAD, devcontainer configuration and required technical gates.

## Binding coordinator responsibility

When a Preview requires a new GitHub Codespace, the coordinator must prepare the creation flow so the Product Owner/user only has to review GitHub's creation screen and confirm **Create codespace**.

The coordinator must **not** delegate routine repository/branch/devcontainer selection to the Product Owner when those values are already known from GitHub live.

The coordinator owns all of the following before asking the user to create the Codespace:

1. identify and revalidate the exact active Preview branch;
2. identify the repository-controlled `devcontainer.json` that must be used;
3. confirm the technically validated Preview head and required workflows;
4. confirm required Codespaces secret names without exposing secret values;
5. publish or refresh a one-click `codespaces.new` creation link in the active Preview PR/handoff;
6. avoid `quickstart=1` whenever the gate requires a **fresh** Codespace;
7. ensure repository automation (`postCreateCommand`, `postAttachCommand`, launcher, ports and dependencies) removes avoidable manual setup after creation;
8. after creation, request only the minimum evidence needed to verify the real environment, such as the forwarded Web URL and exact Git SHA;
9. convert any manual workaround required for successful startup into repository-controlled automation before final acceptance.

If GitHub presents an avoidable configuration ambiguity, for example multiple devcontainer configurations, the coordinator must resolve that ambiguity in the repository/deep-link preparation rather than asking the Product Owner to guess which option is correct.

The Product Owner may still have to perform actions that GitHub deliberately reserves for the authenticated user, such as confirming creation, accepting requested permissions, selecting from account-specific machine types when GitHub cannot preselect one safely, or supplying/authorizing repository Codespaces secrets. Those are confirmation/authorization boundaries, not setup work to be delegated by the coordinator.

## Current Wave 14 one-click creation

Repository:

`brunolrogerio-collab/EliteSCADA`

Preview branch:

`preview/wave14-post-c26-work-audit`

Repository-controlled configuration:

`.devcontainer/devcontainer.json` — **EliteSCADA Post-C26 Work Audit Preview**

Fresh creation link:

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/brunolrogerio-collab/EliteSCADA/tree/preview/wave14-post-c26-work-audit)

Direct URL:

`https://codespaces.new/brunolrogerio-collab/EliteSCADA/tree/preview/wave14-post-c26-work-audit`

Do **not** append `quickstart=1` for the final post-C26 audit candidate because that mode may offer to resume an older matching Codespace instead of creating the required clean environment.

## Current technical contract

At the time this duty was recorded, the last technically validated Preview head was:

`fbe3f26fb5aa575e1de716b51417efd95ac37d77`

That technical head passed the natural post-C26 Preview and Audit State Readiness workflows. A later documentation-only commit must not be substituted as product/runtime validation evidence merely because it is the branch tip.

The current devcontainer contract includes:

- Node 24;
- full Python 3.12 devcontainer feature;
- isolated Server Script Python preflight under `-I -S` for `ast`, `json` and `sys`;
- Web port 5173 forwarded;
- API 5080 internal;
- TimescaleDB private/internal;
- automatic `postAttachCommand` startup through `scripts/preview/ensure-post-c26-preview.sh`;
- no manual port reassignment as part of the accepted startup path.

Always revalidate these values live before using them. This section records the current Wave 14 state, not permission to ignore future repository changes.

## Relationship to the operational runbook

Use this duty together with:

- `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
- `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`;
- the active Preview PR;
- the coordinator issue.

`docs/CODESPACES-PREVIEW-RUNBOOK.md` remains the detailed operational/recovery procedure. This file defines **who owns the automation burden**: the coordinator does.

## Guardrails

- never expose GitHub credentials, tokens or secret values in the one-click handoff;
- never make a Codespace creation link a substitute for exact-head validation;
- never use a stale Preview branch merely because its one-click link still opens;
- never silently resume an old Codespace when a fresh environment is required;
- never weaken product security, authentication, authorization, Identity, Licensing, lifecycle or Runtime authority to simplify Codespaces startup;
- never ask the Product Owner to perform manual restart/port/bootstrap rituals that can and should be repository-controlled.
