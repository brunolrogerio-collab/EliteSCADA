import {
  SECURITY_CAPABILITIES,
  type AuthorityPolicyDocument,
  type AuthorityRole,
  type LocalUser
} from './userAdministrationApi';

type CapabilityDefinition = (typeof SECURITY_CAPABILITIES)[number];

const capabilityByValue = new Map<number, CapabilityDefinition>();
const capabilityById = new Map<string, CapabilityDefinition>();
for (const capability of SECURITY_CAPABILITIES) {
  capabilityByValue.set(capability.value, capability);
  capabilityById.set(capability.id.toLowerCase(), capability);
}

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

export function baselineRoleFor(
  baseline: AuthorityPolicyDocument,
  role: AuthorityRole
): AuthorityRole | null {
  if (!role.id) return null;
  return baseline.roles.find(candidate => candidate.id === role.id) ?? null;
}

export function isRoleKeyEditable(
  baseline: AuthorityPolicyDocument,
  role: AuthorityRole
) {
  return baselineRoleFor(baseline, role) === null;
}

export function roleAssignmentKey(
  baseline: AuthorityPolicyDocument,
  role: AuthorityRole
) {
  return baselineRoleFor(baseline, role)?.key ?? role.key;
}

export function assignedUsersForRole(
  users: readonly LocalUser[],
  baseline: AuthorityPolicyDocument,
  role: AuthorityRole
) {
  const normalized = roleAssignmentKey(baseline, role).toLowerCase();
  return users.filter(user => user.roles.some(assigned => assigned.toLowerCase() === normalized));
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
