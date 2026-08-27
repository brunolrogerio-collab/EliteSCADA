import type { LocalUser } from './userAdministrationApi';

export type UserStatusFilter = 'all' | 'enabled' | 'disabled';
export type AdministrationErrorKind = 'validation' | 'unauthorized' | 'forbidden' | 'not-found' | 'conflict' | 'unknown';

export type UserDraft = {
  displayName: string;
  isEnabled: boolean;
  roles: string[];
};

export type UserChange = 'displayName' | 'status' | 'roles';

export function filterAdministrationUsers(
  users: readonly LocalUser[],
  query: string,
  status: UserStatusFilter
): LocalUser[] {
  const needle = query.trim().toLocaleLowerCase();

  return users.filter(user => {
    if (status === 'enabled' && !user.isEnabled) return false;
    if (status === 'disabled' && user.isEnabled) return false;
    if (!needle) return true;

    return [user.username, user.displayName, ...user.roles]
      .some(value => value.toLocaleLowerCase().includes(needle));
  });
}

export function summarizeUserChanges(user: LocalUser, draft: UserDraft): UserChange[] {
  const changes: UserChange[] = [];

  if (user.displayName.trim() !== draft.displayName.trim()) changes.push('displayName');
  if (user.isEnabled !== draft.isEnabled) changes.push('status');
  if (!sameRoles(user.roles, draft.roles)) changes.push('roles');

  return changes;
}

export function sameRoles(left: readonly string[], right: readonly string[]) {
  const normalize = (roles: readonly string[]) => [...new Set(roles.map(role => role.trim().toLocaleLowerCase()))]
    .sort((a, b) => a.localeCompare(b));
  const a = normalize(left);
  const b = normalize(right);
  return a.length === b.length && a.every((role, index) => role === b[index]);
}

export function countAdministrationUsers(users: readonly LocalUser[]) {
  return users.reduce(
    (summary, user) => {
      if (user.isEnabled) summary.enabled += 1;
      else summary.disabled += 1;
      return summary;
    },
    { total: users.length, enabled: 0, disabled: 0 }
  );
}

export function classifyAdministrationStatus(status: number): AdministrationErrorKind {
  switch (status) {
    case 400:
    case 422:
      return 'validation';
    case 401:
      return 'unauthorized';
    case 403:
      return 'forbidden';
    case 404:
      return 'not-found';
    case 409:
      return 'conflict';
    default:
      return 'unknown';
  }
}
