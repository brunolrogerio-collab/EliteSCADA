import { useId, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import type { AuthProfile } from './AuthGate';
import {
  buildUserSessionPresentation,
  type UserSessionMenuLabels
} from './sessionMenuModel';

export type UserSessionMenuViewProps = {
  profile: AuthProfile | null;
  labels: UserSessionMenuLabels;
  canSwitchUser: boolean;
  onSwitchUser: () => Promise<void>;
  onLogout: () => Promise<void>;
};

type SessionAction = 'switch' | 'logout' | null;

export function UserSessionMenuView({
  profile,
  labels,
  canSwitchUser,
  onSwitchUser,
  onLogout
}: UserSessionMenuViewProps) {
  const detailsRef = useRef<HTMLDetailsElement | null>(null);
  const summaryRef = useRef<HTMLElement | null>(null);
  const [activeAction, setActiveAction] = useState<SessionAction>(null);
  const [failedAction, setFailedAction] = useState<SessionAction>(null);
  const rolesHeadingId = useId();
  const presentation = buildUserSessionPresentation(profile);

  if (!presentation) return null;

  const { displayName, secondaryIdentity, initials, roles } = presentation;
  const busy = activeAction !== null;

  const handleKeyDown = (event: KeyboardEvent<HTMLDetailsElement>) => {
    if (event.key !== 'Escape' || !detailsRef.current?.open) return;

    detailsRef.current.open = false;
    summaryRef.current?.focus();
    event.stopPropagation();
  };

  const runAction = async (action: Exclude<SessionAction, null>, operation: () => Promise<void>) => {
    if (busy) return;

    setActiveAction(action);
    setFailedAction(null);
    try {
      await operation();
      if (detailsRef.current) detailsRef.current.open = false;
    } catch {
      setFailedAction(action);
    } finally {
      setActiveAction(null);
    }
  };

  return (
    <details ref={detailsRef} className="user-session-menu" onKeyDown={handleKeyDown}>
      <summary
        ref={summaryRef}
        className="user-session-menu__trigger"
        aria-label={`${labels.account}: ${displayName}`}
        data-testid="session-menu-toggle"
      >
        <span className="user-session-menu__avatar" aria-hidden="true">{initials}</span>
        <span className="user-session-menu__trigger-copy">
          <strong>{displayName}</strong>
          {secondaryIdentity && <span>{secondaryIdentity}</span>}
        </span>
        <span className="user-session-menu__chevron" aria-hidden="true">⌄</span>
      </summary>

      <div
        className="user-session-menu__panel"
        aria-label={labels.account}
        aria-busy={busy}
        data-testid="session-menu-popup"
      >
        <div className="user-session-menu__identity">
          <span className="user-session-menu__avatar user-session-menu__avatar--large" aria-hidden="true">
            {initials}
          </span>
          <div>
            <strong>{displayName}</strong>
            {secondaryIdentity && <span>{secondaryIdentity}</span>}
          </div>
        </div>

        <div className="user-session-menu__roles" aria-labelledby={rolesHeadingId}>
          <span id={rolesHeadingId} className="user-session-menu__section-label">{labels.roles}</span>
          {roles.length > 0 ? (
            <ul>
              {roles.map(role => <li key={role}>{role}</li>)}
            </ul>
          ) : (
            <span className="user-session-menu__empty-roles">{labels.noRoles}</span>
          )}
        </div>

        {failedAction === 'switch' && (
          <p className="user-session-menu__error" role="alert">{labels.switchUserFailed}</p>
        )}
        {failedAction === 'logout' && (
          <p className="user-session-menu__error" role="alert">{labels.logoutFailed}</p>
        )}

        <div className="user-session-menu__actions">
          {canSwitchUser && (
            <button
              type="button"
              className="user-session-menu__action"
              disabled={busy}
              onClick={() => void runAction('switch', onSwitchUser)}
              data-testid="session-switch-user"
            >
              {activeAction === 'switch' ? labels.switchingUser : labels.switchUser}
            </button>
          )}
          <button
            type="button"
            className="user-session-menu__action"
            disabled={busy}
            onClick={() => void runAction('logout', onLogout)}
            data-testid="session-logout"
          >
            {activeAction === 'logout' ? labels.loggingOut : labels.logout}
          </button>
        </div>
      </div>
    </details>
  );
}
