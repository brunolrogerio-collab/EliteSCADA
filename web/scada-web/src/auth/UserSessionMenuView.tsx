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
  onLogout: () => Promise<void>;
};

export function UserSessionMenuView({ profile, labels, onLogout }: UserSessionMenuViewProps) {
  const detailsRef = useRef<HTMLDetailsElement | null>(null);
  const summaryRef = useRef<HTMLElement | null>(null);
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutFailed, setLogoutFailed] = useState(false);
  const rolesHeadingId = useId();
  const presentation = buildUserSessionPresentation(profile);

  if (!presentation) return null;

  const { displayName, secondaryIdentity, initials, roles } = presentation;

  const handleKeyDown = (event: KeyboardEvent<HTMLDetailsElement>) => {
    if (event.key !== 'Escape' || !detailsRef.current?.open) return;

    detailsRef.current.open = false;
    summaryRef.current?.focus();
    event.stopPropagation();
  };

  const handleLogout = async () => {
    if (loggingOut) return;

    setLoggingOut(true);
    setLogoutFailed(false);
    try {
      await onLogout();
      if (detailsRef.current) detailsRef.current.open = false;
    } catch {
      setLogoutFailed(true);
    } finally {
      setLoggingOut(false);
    }
  };

  return (
    <details ref={detailsRef} className="user-session-menu" onKeyDown={handleKeyDown}>
      <summary
        ref={summaryRef}
        className="user-session-menu__trigger"
        aria-label={`${labels.account}: ${displayName}`}
      >
        <span className="user-session-menu__avatar" aria-hidden="true">{initials}</span>
        <span className="user-session-menu__trigger-copy">
          <strong>{displayName}</strong>
          {secondaryIdentity && <span>{secondaryIdentity}</span>}
        </span>
        <span className="user-session-menu__chevron" aria-hidden="true">⌄</span>
      </summary>

      <div className="user-session-menu__panel" aria-label={labels.account} aria-busy={loggingOut}>
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

        {logoutFailed && (
          <p className="user-session-menu__error" role="alert">{labels.logoutFailed}</p>
        )}

        <button
          type="button"
          className="user-session-menu__logout"
          disabled={loggingOut}
          onClick={() => void handleLogout()}
        >
          {loggingOut ? labels.loggingOut : labels.logout}
        </button>
      </div>
    </details>
  );
}
