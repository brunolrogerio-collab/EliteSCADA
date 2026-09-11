# Wave 14 C22 — Runtime Shell Viewport Fit

**Date:** 2026-09-05 BRT  
**State:** IMPLEMENTATION ACTIVE / C11 BLOCKER  
**Branch:** `wave14/c22-runtime-shell-viewport-fit`  
**Integration base:** `cac8b6d58a4969a4aa6369590f6bd32fc2fae3d2`  
**Integration target:** `wave14/corrections-integration` only

## 1. Trigger

Canonical C11 EEE browser validation reached the end of the functional HMI scenario after Trends, Alarm/Event navigation and return to the startup screen, then failed the final viewport guard with 46 px of vertical overflow.

The C11 package itself did not own that remaining overflow. Repository inspection showed that the generic Active HMI Runtime shell sized `.runtime-operator-application` as `calc(100dvh - 54px)`, accounting for the privileged App Bar but not for the additional `Runtime views` navigation rendered above the Runtime when historical access is available.

This is therefore a generic product defect discovered by C11, not permission for an EEE-specific fixture workaround.

## 2. Required correction

C22 must preserve the existing Runtime shell while ensuring that an Active Engineering HMI fits the browser viewport in all supported shell combinations:

- privileged App Bar without Runtime views;
- privileged App Bar with Runtime views;
- Runtime-only App Bar without Runtime views;
- Runtime-only App Bar with Runtime views;
- fullscreen Runtime remains `100vw x 100vh`.

The Runtime views navigation has a stable 46 px shell height. The Active Runtime application must subtract that shell height whenever the navigation is present rather than overflowing the document.

## 3. Implementation boundary

C22 may change only generic application-shell sizing and its regression coverage.

It must not:

- change authored Screen coordinates or logical HMI dimensions;
- hide document overflow as a substitute for correct sizing;
- modify C11 EEE package geometry to compensate for shell chrome;
- weaken the C11 viewport guard;
- change authorization, effective-capability, Active-revision, licensing or Runtime visual contracts;
- add EEE-specific Runtime behavior.

## 4. Regression proof

Wave11 gains a C22 project immediately after the canonical Active Runtime lifecycle. It consumes the real Active Engineering revision established by the lifecycle and verifies in Chromium that:

- `runtime-engineering-application` is mounted;
- `Runtime views` navigation is visible;
- the navigation resolves to 46 px;
- document and body scroll heights do not exceed the browser viewport beyond one-pixel rounding tolerance;
- the Runtime application's bottom edge remains inside the browser viewport.

Downstream Wave11 projects remain dependent on the C22 guard, so a future shell regression blocks the rest of the Active HMI chain rather than silently passing.

## 5. Acceptance gate

Before integration:

1. inspect exact diff against `cac8b6d58...`;
2. validate exact C22 head through universal EliteSCADA CI;
3. validate exact C22 head through Wave11 Active HMI Runtime;
4. diagnose any red before rerun;
5. require other automatically selected affected workflows to be green or explicitly diagnosed;
6. integrate only into `wave14/corrections-integration` after acceptance;
7. synchronize the accepted integration baseline back into C11 before resuming C11 validation.

PR #212 remains DRAFT and is not authorized for merge to `main`.
