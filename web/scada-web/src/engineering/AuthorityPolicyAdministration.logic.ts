import {
  SECURITY_CAPABILITIES,
  type AuthorityPolicyDocument,
  type AuthorityRole,
  type LocalUser
} from './userAdministrationApi';

const capabilityByValue = new Map(
  SECURITY_CAPABILITIES.map(capability => [capability.value, capability] as const)
);
const capabilityById = new Map(
  SECURITY_CAPABILITIES.map(capability => [capability.id.toLowerCase(), capability] as const)
);

export function capabilityDescriptor(value: number | string) {
  if (typeof value === 'number') return capabilityByValue.get(value);
  const numeric = Number(value);
  if (Number.isInteger(numeric) && String(numeric) === value.trim()) return capabilityByValue.get(numeric);
  return capabilityById.get(value.toLowerCase());
}

export function capabilityKey(value: number | string) {
  const descriptor = capabilityDescriptor(value);
  return descriptor?.id ?? String(value);
}

export function nextRoleKey(policy: AuthorityPolicyDocument, base = 'custom-role') {
  const used = new Set(policy.roles.map(role => role.key.toLowerCase()));
  if (!used.has(base)) return base;
  let suffix = 2;
  while (used.has(`${base}-${suffix}`)) suffix += 1;
  return `${base}-${suffix}`;
}

export function userGrantPreview(user: LocalUser | null, roles: readonly AuthorityRole[]) {
  if (!user) return [];
  const assigned = new Set(user.roles.map(role => role.toLowerCase()));
  const rows = roles
    .filter(role => assigned.has(role.key.toLowerCase()))
    .flatMap(role => (role.grants ?? []).map(grant => ({
      role: role.key,
      capability: capabilityKey(grant.capability),
      scopeNodeId: grant.scope?.scopeNodeId ?? null,
      includeDescendants: grant.scope?.includeDescendants ?? false
    })));
  return rows.sort((a, b) =>
    a.capability.localeCompare(b.capability) || a.role.localeCompare(b.role)
  );
}
