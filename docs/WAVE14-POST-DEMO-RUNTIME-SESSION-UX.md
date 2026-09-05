# Wave 14 — Post-DEMO Runtime Session / Switch User UX

**Recorded:** 2026-09-05 BRT  
**Authority:** Product Owner decision during Wave 14 canonical EEE DEMO closure  
**State:** DESIGN LOCK / NOT IMPLEMENTED  
**Scope:** generic EliteSCADA Runtime shell and authentication UX; never EEE-specific

## 1. Product Owner intent

An operator using Runtime must have a discreet, always-reachable way to understand the current authenticated identity and to end or change the user session without exposing Engineering/development surfaces.

The intended operator experience is a small system-owned session/user icon in the Runtime shell. **The current authenticated user's display name or username must remain visibly presented beside this icon while a user is authenticated.** Activating the control opens a **system popup/overlay** with session actions.

This popup is EliteSCADA system UI. It is not an authored HMI Popup, is not stored in `.escadapkg`, and cannot be hidden or replaced by project content.

## 2. Current product audit

Audit performed against C11 candidate:

`3486a488181201062ba2f6790cd6deb7f5bccb8a`

Current product already provides:

- `UserSessionMenu` in the application shell;
- current-user identity presentation;
- role presentation;
- a working **Logout / Sair** action;
- `/api/auth/logout` session termination through `AuthGate`;
- capability-derived shell links, so a Runtime-only identity does not receive Engineering, Audit or Licensing navigation merely because those product areas exist.

Therefore **logout and presentation of the current identity are not missing backend capabilities**.

The missing/refinement requirement is the dedicated operator-oriented session UX, especially an explicit **Trocar usuário / Switch user** flow suitable for Runtime stations, while preserving the current-user name as a persistent shell cue.

## 3. Runtime-only session affordance

For a Runtime-only user, the normal shell should expose a compact session control with a small icon/avatar **and the current user's name beside it**, rather than a large Engineering-style identity menu.

Visible identity rules:

- prefer `displayName` when available;
- otherwise show `username`;
- the visible name must update immediately after a successful user switch;
- it must never continue showing the previous identity while commands are already attributed to the new identity;
- long names may be visually truncated to protect Runtime space, provided the full identity remains available through accessible text/tooltip and inside the system popup;
- the identity cue must remain readable without becoming dominant HMI chrome.

The control must remain:

- visible but visually discreet;
- reachable with keyboard navigation;
- usable in normal Runtime presentation;
- usable while Runtime is in product fullscreen mode;
- outside the authored logical HMI coordinate space;
- independent from the current Screen, Popup or navigation authored by the project.

The system popup should show at least:

- current display name / username as available;
- enough identity information to make operator attribution unambiguous;
- **Trocar usuário**;
- **Sair**.

Role/capability details may be shown when useful, but the Runtime popup must remain concise and operator-oriented.

## 4. `Sair` semantics

`Sair` keeps the existing security intent:

1. terminate/invalidate the current local EliteSCADA session;
2. clear the current authenticated profile in the client;
3. leave no operator action available under the old identity;
4. present the normal authentication surface.

A failed logout must fail visibly. The client must not pretend the user is logged out while continuing to operate under a still-valid server session.

External identity providers may require provider-specific logout behavior, but backend/session authority remains canonical.

## 5. `Trocar usuário` semantics

`Trocar usuário` is not merely a username field placed over an active session.

The safe contract is:

1. terminate/invalidate the current user session first;
2. immediately lock interaction with the Runtime behind a system-owned authentication overlay;
3. request credentials for the next identity through the normal supported authentication mechanism;
4. after successful authentication, reload the authenticated profile and effective capabilities from the backend;
5. update the visible current-user name in the Runtime shell;
6. resume only surfaces authorized for the new identity.

While the switch-user authentication overlay is active:

- the previous operator must have **zero interactive authority**;
- project commands, buttons, popup actions and navigation behind the overlay must not be actionable;
- merely dismissing the overlay must not restore the old session after it has been invalidated;
- no cached frontend role/capability state may be treated as authority.

For local authentication, the next user may authenticate in the system overlay itself. For external authentication, the product may redirect to or invoke the configured provider as required.

## 6. Authorization boundary

This UX must not weaken the current capability model.

A Runtime-only operator:

- may see Runtime surfaces granted by backend capabilities;
- must not gain Engineering, Diagnostics, Audit, Licensing or other privileged areas through the session popup;
- must continue to receive server-side authorization enforcement even if a route is entered manually.

After switching users, every capability-dependent surface must reflect the **new** backend-effective capability set. A frontend navigation refresh alone is not sufficient evidence of security.

## 7. Fullscreen behavior

Native/product fullscreen cannot remove the operator's ability to change identity indefinitely.

The implementation must provide a system-owned route to the session control in fullscreen. The exact visual treatment may evolve, but the session affordance cannot depend on an Engineering header that is intentionally absent for Runtime-only operation.

The current-user identity should remain available with the fullscreen session affordance without materially obstructing HMI operation or becoming a large permanent chrome element.

## 8. Audit / operational attribution

Where the existing security/audit model records authenticated actions, commands executed after a user switch must be attributable to the newly authenticated identity.

No command after successful switch may continue to carry the previous identity due to stale client/session state.

The identity shown beside the Runtime session icon must agree with the identity used for authorization and audit attribution.

Session change/logout events should be auditable where the existing audit architecture supports authentication/session events. This requirement does not merge Audit with Alarm or Operational Event.

## 9. Failure behavior

The flow must fail closed.

Examples:

- if logout/session invalidation fails, display the failure and do not claim that switch-user completed;
- if authentication of the next user fails, Runtime remains locked from operator interaction;
- if capability retrieval fails after login, do not optimistically restore privileged controls;
- if the new user has no Runtime capability, route to the normal authorized product outcome rather than retaining the previous Runtime authorization;
- if identity refresh fails, do not display a stale previous-user name as if it represented the active session.

## 10. Acceptance tests required when implemented

At minimum prove:

1. Runtime-only user sees the discreet system session control;
2. current authenticated display name/username is visible beside the session icon;
3. Runtime-only user does not see Engineering/Audit/Licensing navigation;
4. `Sair` invalidates the server session and returns to authentication;
5. `Trocar usuário` invalidates the first identity before the second becomes active;
6. Runtime is non-interactive during the switch authentication state;
7. successful switch reloads profile and effective capabilities;
8. the visible current-user name changes to the new identity immediately after successful switch;
9. old-user authorization cannot be reused after switch;
10. new-user restrictions take effect immediately;
11. direct navigation to unauthorized Engineering endpoints/routes still fails server-side;
12. session control and current identity remain usable/visible in Runtime fullscreen;
13. the session popup is system-owned and does not depend on `.escadapkg` content;
14. keyboard/Escape/focus handling is deterministic and accessible without creating an authorization bypass.

## 11. Implementation classification

As of this design lock:

- Logout capability: **IMPLEMENTED** in the current product shell/auth flow;
- current-user identity presentation: **IMPLEMENTED** and must be preserved beside the compact Runtime session icon;
- Runtime-only capability isolation: **IMPLEMENTED** as the current generic shell model and still requires preservation;
- dedicated compact Runtime session popup: **NOT IMPLEMENTED / UX REFINEMENT REQUIRED**;
- explicit `Trocar usuário` flow: **NOT IMPLEMENTED**;
- this document: product requirement/design authority only.
