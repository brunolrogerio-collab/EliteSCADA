# Next Coordinator Chat Handoff

Copy the text below into a new coordinator chat if rotation is required.

---

Assuma a coordenação da Wave 14 do EliteSCADA a partir deste ponto.

## REGRA FUNDAMENTAL

**GitHub é a memória oficial e a única autoridade sobre o estado do projeto.**

Revalide ao vivo antes de toda decisão, diagnóstico, alteração de código/documentação, ação em PR, rerun ou merge. Havendo divergência, GitHub live prevalece.

Repository: `brunolrogerio-collab/EliteSCADA`

- Integration: `wave14/corrections-integration`, PR #212 -> main — OPEN/DRAFT, **NÃO MERGEAR sem autorização posterior, específica e explícita do Product Owner**.
- Active C26 branch: `wave14/c26-po-homologation-corrections`.
- Coordinator issue: #286.
- Implementation PR #287: C26 -> C11, DRAFT.
- Validation PR #288: C26 -> main, **VALIDATION ONLY / MUST NEVER MERGE**.
- C11 validation PR #266: **MUST NEVER MERGE**.
- Pre-C26 Preview #285: preservar intocado como evidência histórica.
- Wave13 #205/#207: paused.

## Mandatory reading

Read live from C26 branch:

1. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
5. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
6. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
7. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

Then revalidate branch HEAD, issue #286, PRs #287/#288/#212 and workflows for the exact HEAD.

## Current validated C26 product/test boundary

Exact product/test SHA:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

Five normal gates at this exact SHA are SUCCESS:

- EliteSCADA CI #1444 / run `34237046911`
- Preview Licensing CI #392 / run `34237046935`
- Interop Lab Smoke #269 / run `34237046897`
- Wave 11 Active HMI Runtime #370 / run `34237046873`
- L3 Seven-Driver Lab #348 / run `34237046980`

C26 status:

- C26.1–C26.6 — **IMPLEMENTED / VALIDATED**
- C26.7 Engineering theme/contrast — **ACTIVE**
- C26.8–C26.10 — pending per live issue #286
- package/new Preview/new real PO homologation follows only after accepted product sequence

## C26.6 important implementation history

Commits:

- `4286f2409f30492387f764b192acede6d26bd3aa` — collapsible Engineering workspace + constrained viewport E2E;
- `4b0e46d96510c6a637e12dc83c6d56dd8b9cc592` — collapsed Properties mobile correction;
- `c1ae4bd311334835961bf4bbdcd6bd46b4acfa36` — outer Engineering grid placement fix;
- `7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11` — inner Visual Editor grid placement fix.

The Chromium regression found two real zero-width canvas bugs caused by CSS Grid auto-placement after hidden side panels. Do not weaken that regression. Final exact SHA is fully green.

## Immediate task: C26.7

Live issue #286 acceptance:

- readable explicit foreground/background tokens;
- editable, readonly, disabled and placeholder states visually distinct;
- regression coverage;
- affected Engineering surfaces include Screens and Script Engineering.

Keep this presentation-only. Do not change or weaken authentication, authorization, capability projection, Engineering Lock, lifecycle, package or backend authority.

Before writing, locate/fetch the exact live Screens and Script Engineering components/CSS and closest existing tests. Prefer a generic Engineering form-state treatment rather than one-off component hacks.

## Preserved Historian/Preview boundary

The backend HistoricalQuery route exists and is feature-gated. Preview #285 did not enable HistoricalQuery/cursor-key configuration, producing the PO-observed 404. Keep #285 unchanged. A new post-C26 Preview must enable safe Preview/dev HistoricalQuery configuration. Alarm / Operational Event / Audit remain separate.

## Permanent guardrails

- no direct main mutation;
- #212 not authorized to merge;
- #288 and #266 never merge;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose every CI red before rerun;
- never weaken tests, security, Identity, authorization, Engineering Lock, licensing, lifecycle, package, drivers or Runtime Active Revision authority;
- Runtime/Active cannot depend on `.escadalib`;
- preserve #285;
- Wave13 remains paused.

When Product Owner says `siga`, advance autonomously through safe subsequent work. `siga` does not authorize protected merges.

---
