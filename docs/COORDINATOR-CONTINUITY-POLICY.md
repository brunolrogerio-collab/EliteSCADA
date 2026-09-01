# EliteSCADA Coordinator Continuity Policy

Date: 2026-09-01 (BRT)

## Permanent coordination rule

The Development Lead's normal preference is to keep the active EliteSCADA coordinator conversation running continuously. A coordinator must **not** suggest or plan a chat/coordinator replacement as part of the normal project workflow.

A new chat/coordinator is a contingency only, for example when the current conversation becomes unavailable or an external platform limitation forces replacement. It is not a project milestone, release step, optimization or routine handoff.

## Repository is authoritative persistent memory

Regardless of whether the same chat continues or a replacement is ever required, the repository must continuously contain enough context to resume coordination safely without depending on chat history.

Therefore:

1. `PROJECT GOAL.md` is the authority for stable product goals, locked architecture and permanent coordination rules.
2. `LAST CHANGE.md` is the authority for mutable operational state: active branch, exact SHA, Actions run/job evidence, issue/PR state, blocker, accepted/rejected evidence and exact next action.
3. Material decisions, diagnoses, fixes, validation outcomes, sequencing constraints and next actions must be persisted in the repository during the same coordination cycle in which they become relevant.
4. Issue/PR comments and dedicated evidence/status documents should carry detailed acceptance evidence when useful, but must not become the sole location of a critical project fact.
5. Chat history is convenient working context only. It is never the sole source of project truth.
6. If chat memory conflicts with live repository, branch, issue or exact-SHA CI evidence, inspect live GitHub and follow the repository/CI authority rules defined in `PROJECT GOAL.md`.
7. Do not ask the Development Lead to change chats merely because the conversation is long. Continue in the current coordinator conversation unless a real external limitation prevents it.
8. If a replacement conversation is unavoidable, it must begin by reading `PROJECT GOAL.md`, `LAST CHANGE.md` and the currently referenced gate/handoff evidence before taking any project action.

## Current release sequencing

This continuity policy does not alter the current release gate. Wave 11 remains prohibited until the complete L3 gate is accepted on the required exact SHA and issue #180 is closed.

This document makes explicit the continuity preference already embodied by `PROJECT GOAL.md` and `LAST CHANGE.md`; it does not supersede either authority file.
