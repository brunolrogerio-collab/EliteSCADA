# Coordinator recovery bootstrap

This is a state-independent recovery procedure. It carries no current product SHA, PR assignment, or CI result. Continue the existing coordinator chat whenever available; replacement is a contingency under [the continuity policy](COORDINATOR-CONTINUITY-POLICY.md).

## Establish the current baseline once

1. Read root `PROJECT GOAL.md`, the [global collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md), and the latest section of root `LAST CHANGE.md`.
2. Read the complete short [current pointer](CURRENT-COORDINATOR-HANDOFF.md) and [Main order](WAVE15-MAIN-COORDINATOR-HANDOFF.md).
3. Read #297 first, #302 next, then #305 and the active mission issue/PR's latest binding comments. Inspect live integration ref, candidate head, and the required Actions results.
4. Resolve any conflict using [the authority map](README.md). Read only the historical evidence and frozen contracts relevant to the active mission.
5. Establish the active executor, its assignment, last delivery, current next owner/action, and any unresolved review request. Publish a corrected current order if necessary before dispatching work.

## Continue from that baseline

Use incremental checks of changed documents, current refs, the owning issue/PR, and the existing run IDs. Do not reread historical handoffs on every `siga`, reopen a resolved blocker, or ask the Product Owner to repeat applicable authorization.

Main directly dispatches to an authorized existing executor chat when the tool is available and observes it through the chat wait mechanism. If dispatch is unavailable, report the precise missing action; a GitHub comment by itself does not wake a chat.

All material decisions and results belong in the repository/owning ledger. Finish user-visible responses with the current local time specified by the global protocol.

[Historical transfer snapshots](NEXT-COORDINATOR-CHAT-HANDOFF-ARCHIVE-2026-10-15.md) remain evidence only.
