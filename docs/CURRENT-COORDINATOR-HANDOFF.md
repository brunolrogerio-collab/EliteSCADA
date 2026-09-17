# Current Coordinator Handoff — Wave 15

> **PONTE CURTA da coordenação corrente.**
>
> O handoff operacional vivo e canônico é:
>
> `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
>
> Este arquivo não substitui o handoff canônico. Em qualquer divergência, reconstruir GitHub live e corrigir o handoff canônico.

## Estado rápido

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 — VERIFIED/FROZEN.
- FND-08 — VERIFIED/FROZEN.
- FND-03 — ACTIVE / NOT FROZEN.
- Runtime Session Lease v1 — VERIFIED/FROZEN.
- machine-license v2 + hardening — VERIFIED/FROZEN.
- Runtime Admission — VERIFIED/FROZEN.
- Shared Runtime Seat Accounting — PR_READY / aguardando decisão do Main após handoff final.
- FND-04 — QUEUED / CONTRACT DEFINED / NOT ACTIVE.
- FC0-A — BLOCKED.

## Product checkpoint

Último product checkpoint integrado/validado:

`6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`

tree:

`53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`

Commits posteriores de coordenação/documentação podem avançar o HEAD da integração sem mudar automaticamente esse product checkpoint.

## Candidate atual

PR #331 — `FND-03: enforce shared runtime seat accounting`

- base de produto autorizada: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- exact candidate head: `09f81e97369089def481ceb25629779a5aba8aff`
- target: `wave15/corrections-integration`
- PR permanece OPEN / not merged.

EliteSCADA CI #1544 / run `35255337014`, latest attempt no mesmo exact candidate:

- Chromium `105348050154` — SUCCESS
- Web `105348051287` — SUCCESS
- Backend/test/smoke `105348092685` — SUCCESS

## Ordem corrente

CODEX deve reler `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` e cumprir somente a `CURRENT ORDER` live.

No estado desta ponte, a ordem é publicar o handoff final do Shared Runtime Seat Accounting e então STOP para decisão do Main. Nenhum novo rerun, merge, FND-04 ou novo slice FND-03 é autorizado por esta ponte.

## Autoridade permanente do Main para CI

O Product Owner autorizou permanentemente o Main Coordinator a operar CI/GitHub Actions de validação pré-merge e pós-merge, inclusive rerun tecnicamente justificado, sem pedir nova autorização a cada execução.

Essa autoridade exige:

- exact SHA/ref;
- diagnóstico antes de rerun;
- preservação do candidate/merge SHA;
- menor rerun suficiente;
- registro de run/attempt/job;
- nenhum loop cego de rerun.

Ela **não** autoriza merge, alteração de workflow/teste para obter verde, commit artificial, force push ou escrita em `main`.

Autoridade de merge permanece separada; CI verde nunca equivale a autorização de merge.

## Retomada obrigatória de qualquer coordenador

1. Ler integralmente `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
2. Ler `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md` para o protocolo genérico de sucessão.
3. Revalidar integration HEAD/tree, PRs, issues e Actions live.
4. Distinguir PRODUCT CHECKPOINT de coordination/documentation HEAD.
5. Corrigir qualquer estado stale antes da próxima ordem.
6. Usar o handoff canônico como canal primário de ordens CODEX/DEV/AUD.

## Fontes de coordenação

- canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
- prompt genérico/sucessão: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
- FND-03/licensing: #301
- Foundation/dependencies: #305
- Wave 15 global: #297

Ao Product Owner: `Hora: HH:MM` em `America/Sao_Paulo`.
