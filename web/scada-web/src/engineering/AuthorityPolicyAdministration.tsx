import { useCallback, useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import {
  AdministrationHttpError,
  SECURITY_CAPABILITIES,
  authorityPolicyAdministrationApi,
  toAuthorityMutationRequest,
  type AuthorityGrant,
  type AuthorityPolicyDocument,
  type AuthorityRole,
  type AuthorityScopeNode,
  type EffectiveCapabilities,
  type LocalUser
} from './userAdministrationApi';

type PolicyStrings = {
  title: string;
  subtitle: string;
  loading: string;
  retry: string;
  policyVersion: string;
  roles: string;
  scopes: string;
  newRole: string;
  duplicateRole: string;
  deleteRole: string;
  assignedUsers: string;
  roleKey: string;
  roleName: string;
  description: string;
  grants: string;
  addGrant: string;
  removeGrant: string;
  capability: string;
  scope: string;
  globalScope: string;
  includeDescendants: string;
  selectScope: string;
  hierarchy: string;
  noScopes: string;
  scopeStableId: string;
  resourceStableId: string;
  configuredPreview: string;
  configuredPreviewNote: string;
  currentEffective: string;
  currentEffectiveNote: string;
  runtime: string;
  workspace: string;
  authDisabled: string;
  previewChanges: string;
  previewing: string;
  previewReady: string;
  applyChanges: string;
  applying: string;
  discard: string;
  dirty: string;
  clean: string;
  serverValidated: string;
  previewRequired: string;
  deleteAssigned: string;
  backendRejected: string;
  conflictHint: string;
  unknownCapability: string;
  unassigned: string;
  usersWithRole: string;
  roleId: string;
  noRole: string;
  capabilityGroups: Record<string, string>;
};

const policyStrings: Record<EngineeringLocale, PolicyStrings> = {
  'pt-BR': {
    title: 'Papéis, capabilities e escopos',
    subtitle: 'Edite a política canônica de Authority. O backend valida, audita e decide toda autorização.',
    loading: 'Carregando política de Authority…',
    retry: 'Tentar novamente',
    policyVersion: 'Versão da política',
    roles: 'Papéis',
    scopes: 'Nós de escopo',
    newRole: 'Novo papel',
    duplicateRole: 'Duplicar',
    deleteRole: 'Excluir',
    assignedUsers: 'Usuários atribuídos',
    roleKey: 'Chave estável',
    roleName: 'Nome',
    description: 'Descrição',
    grants: 'Grants explícitos',
    addGrant: 'Adicionar grant',
    removeGrant: 'Remover',
    capability: 'Capability',
    scope: 'Escopo',
    globalScope: 'Sem escopo (global para essa capability)',
    includeDescendants: 'Incluir descendentes',
    selectScope: 'Selecione um nó estável',
    hierarchy: 'Hierarquia canônica',
    noScopes: 'Nenhum nó de escopo foi definido.',
    scopeStableId: 'Scope ID',
    resourceStableId: 'Resource ID',
    configuredPreview: 'Prévia configurada do usuário',
    configuredPreviewNote: 'União explicativa dos grants das roles atribuídas. Não substitui a decisão do evaluator backend para um recurso concreto.',
    currentEffective: 'Capabilities efetivas da sessão atual',
    currentEffectiveNote: 'Resultado consultado diretamente do backend para a identidade desta sessão.',
    runtime: 'Runtime',
    workspace: 'Engineering Workspace',
    authDisabled: 'Autenticação desabilitada: o backend reporta capabilities irrestritas para esta sessão.',
    previewChanges: 'Validar no backend',
    previewing: 'Validando…',
    previewReady: 'Preview backend válido. Revise antes de aplicar.',
    applyChanges: 'Aplicar política',
    applying: 'Aplicando…',
    discard: 'Descartar rascunho',
    dirty: 'Rascunho não aplicado',
    clean: 'Sem alterações',
    serverValidated: 'Validado pelo backend',
    previewRequired: 'Valide novamente no backend antes de aplicar.',
    deleteAssigned: 'Não é possível remover localmente uma role ainda atribuída; o backend também rejeitará a política órfã.',
    backendRejected: 'O backend rejeitou a política.',
    conflictHint: 'A versão da Authority mudou. Recarregue e refaça a alteração.',
    unknownCapability: 'Capability desconhecida',
    unassigned: 'Nenhum usuário atribuído',
    usersWithRole: 'Usuários com esta role',
    roleId: 'Role ID',
    noRole: 'Nenhum papel selecionado.',
    capabilityGroups: {
      runtime: 'Runtime',
      alarms: 'Alarmes',
      trends: 'Trends',
      engineering: 'Engineering',
      users: 'Usuários / Sistema',
      ha: 'HA'
    }
  },
  en: {
    title: 'Roles, capabilities and scopes',
    subtitle: 'Edit the canonical Authority policy. The backend validates, audits and decides every authorization.',
    loading: 'Loading Authority policy…',
    retry: 'Retry',
    policyVersion: 'Policy version',
    roles: 'Roles',
    scopes: 'Scope nodes',
    newRole: 'New role',
    duplicateRole: 'Duplicate',
    deleteRole: 'Delete',
    assignedUsers: 'Assigned users',
    roleKey: 'Stable key',
    roleName: 'Name',
    description: 'Description',
    grants: 'Explicit grants',
    addGrant: 'Add grant',
    removeGrant: 'Remove',
    capability: 'Capability',
    scope: 'Scope',
    globalScope: 'Unscoped (global for this capability)',
    includeDescendants: 'Include descendants',
    selectScope: 'Select a stable node',
    hierarchy: 'Canonical hierarchy',
    noScopes: 'No scope nodes are defined.',
    scopeStableId: 'Scope ID',
    resourceStableId: 'Resource ID',
    configuredPreview: 'Configured user preview',
    configuredPreviewNote: 'Explanatory union of grants from assigned roles. It does not replace the backend evaluator decision for a concrete resource.',
    currentEffective: 'Current session effective capabilities',
    currentEffectiveNote: 'Result queried directly from the backend for this session identity.',
    runtime: 'Runtime',
    workspace: 'Engineering Workspace',
    authDisabled: 'Authentication is disabled: the backend reports unrestricted capabilities for this session.',
    previewChanges: 'Validate on backend',
    previewing: 'Validating…',
    previewReady: 'Backend preview is valid. Review before applying.',
    applyChanges: 'Apply policy',
    applying: 'Applying…',
    discard: 'Discard draft',
    dirty: 'Unapplied draft',
    clean: 'No changes',
    serverValidated: 'Backend validated',
    previewRequired: 'Validate again on the backend before applying.',
    deleteAssigned: 'A role still assigned to a local user cannot be removed here; the backend also rejects an orphaned policy.',
    backendRejected: 'The backend rejected the policy.',
    conflictHint: 'Authority version changed. Reload and recreate the change.',
    unknownCapability: 'Unknown capability',
    unassigned: 'No assigned users',
    usersWithRole: 'Users with this role',
    roleId: 'Role ID',
    noRole: 'No role selected.',
    capabilityGroups: {
      runtime: 'Runtime',
      alarms: 'Alarms',
      trends: 'Trends',
      engineering: 'Engineering',
      users: 'Users / System',
      ha: 'HA'
    }
  },
  es: {
    title: 'Roles, capabilities y alcances',
    subtitle: 'Edite la política canónica de Authority. El backend valida, audita y decide toda autorización.',
    loading: 'Cargando política de Authority…',
    retry: 'Reintentar',
    policyVersion: 'Versión de la política',
    roles: 'Roles',
    scopes: 'Nodos de alcance',
    newRole: 'Nuevo rol',
    duplicateRole: 'Duplicar',
    deleteRole: 'Eliminar',
    assignedUsers: 'Usuarios asignados',
    roleKey: 'Clave estable',
    roleName: 'Nombre',
    description: 'Descripción',
    grants: 'Grants explícitos',
    addGrant: 'Agregar grant',
    removeGrant: 'Eliminar',
    capability: 'Capability',
    scope: 'Alcance',
    globalScope: 'Sin alcance (global para esta capability)',
    includeDescendants: 'Incluir descendientes',
    selectScope: 'Seleccione un nodo estable',
    hierarchy: 'Jerarquía canónica',
    noScopes: 'No hay nodos de alcance definidos.',
    scopeStableId: 'Scope ID',
    resourceStableId: 'Resource ID',
    configuredPreview: 'Vista previa configurada del usuario',
    configuredPreviewNote: 'Unión explicativa de los grants de los roles asignados. No reemplaza la decisión del evaluator backend para un recurso concreto.',
    currentEffective: 'Capabilities efectivas de la sesión actual',
    currentEffectiveNote: 'Resultado consultado directamente del backend para la identidad de esta sesión.',
    runtime: 'Runtime',
    workspace: 'Engineering Workspace',
    authDisabled: 'Autenticación deshabilitada: el backend informa capabilities irrestrictas para esta sesión.',
    previewChanges: 'Validar en backend',
    previewing: 'Validando…',
    previewReady: 'Preview backend válido. Revise antes de aplicar.',
    applyChanges: 'Aplicar política',
    applying: 'Aplicando…',
    discard: 'Descartar borrador',
    dirty: 'Borrador no aplicado',
    clean: 'Sin cambios',
    serverValidated: 'Validado por backend',
    previewRequired: 'Valide nuevamente en el backend antes de aplicar.',
    deleteAssigned: 'No se puede eliminar aquí un rol todavía asignado; el backend también rechaza una política huérfana.',
    backendRejected: 'El backend rechazó la política.',
    conflictHint: 'La versión de Authority cambió. Recargue y rehaga el cambio.',
    unknownCapability: 'Capability desconocida',
    unassigned: 'Ningún usuario asignado',
    usersWithRole: 'Usuarios con este rol',
    roleId: 'Role ID',
    noRole: 'Ningún rol seleccionado.',
    capabilityGroups: {
      runtime: 'Runtime',
      alarms: 'Alarmas',
      trends: 'Trends',
      engineering: 'Engineering',
      users: 'Usuarios / Sistema',
      ha: 'HA'
    }
  }
};

const capabilityByValue = new Map<number, typeof SECURITY_CAPABILITIES[number]>(
  SECURITY_CAPABILITIES.map(capability => [capability.value, capability])
);
const capabilityById = new Map<string, typeof SECURITY_CAPABILITIES[number]>(
  SECURITY_CAPABILITIES.map(capability => [capability.id.toLowerCase(), capability])
);

function capabilityDescriptor(value: number | string) {
  if (typeof value === 'number') return capabilityByValue.get(value);
  const numeric = Number(value);
  if (Number.isInteger(numeric) && String(numeric) === value.trim()) return capabilityByValue.get(numeric);
  return capabilityById.get(value.toLowerCase());
}

function capabilityKey(value: number | string) {
  const descriptor = capabilityDescriptor(value);
  return descriptor?.id ?? String(value);
}

function clonePolicy(policy: AuthorityPolicyDocument): AuthorityPolicyDocument {
  return structuredClone(policy);
}

function stablePolicyShape(policy: AuthorityPolicyDocument) {
  return JSON.stringify({
    roles: policy.roles,
    scopes: policy.scopes
  });
}

function assignedUsersForRole(users: readonly LocalUser[], roleKey: string) {
  const normalized = roleKey.toLowerCase();
  return users.filter(user => user.roles.some(role => role.toLowerCase() === normalized));
}

function nextRoleKey(policy: AuthorityPolicyDocument, base = 'custom-role') {
  const used = new Set(policy.roles.map(role => role.key.toLowerCase()));
  if (!used.has(base)) return base;
  let suffix = 2;
  while (used.has(`${base}-${suffix}`)) suffix += 1;
  return `${base}-${suffix}`;
}

function userGrantPreview(user: LocalUser | null, roles: readonly AuthorityRole[]) {
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

export function AuthorityPolicyAdministration({
  locale,
  users,
  selectedUser
}: {
  locale: EngineeringLocale;
  users: LocalUser[];
  selectedUser: LocalUser | null;
}) {
  const s = policyStrings[locale];
  const [baseline, setBaseline] = useState<AuthorityPolicyDocument | null>(null);
  const [draft, setDraft] = useState<AuthorityPolicyDocument | null>(null);
  const [effective, setEffective] = useState<EffectiveCapabilities | null>(null);
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<'preview' | 'apply' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewValid, setPreviewValid] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [policy, currentEffective] = await Promise.all([
        authorityPolicyAdministrationApi.load(),
        authorityPolicyAdministrationApi.effectiveCapabilities()
      ]);
      const clean = clonePolicy(policy);
      setBaseline(clean);
      setDraft(clonePolicy(policy));
      setEffective(currentEffective);
      setSelectedRoleId(current => {
        if (current && policy.roles.some(role => role.id === current)) return current;
        return policy.roles[0]?.id ?? null;
      });
      setPreviewValid(false);
    } catch (reason) {
      setError(policyErrorMessage(reason, s));
    } finally {
      setLoading(false);
    }
  }, [s]);

  useEffect(() => {
    void load();
  }, [load]);

  const dirty = useMemo(() =>
    baseline !== null && draft !== null && stablePolicyShape(baseline) !== stablePolicyShape(draft),
  [baseline, draft]);

  const selectedRole = useMemo(() =>
    draft?.roles.find(role => role.id === selectedRoleId) ?? null,
  [draft, selectedRoleId]);

  const selectedRoleUsers = useMemo(() =>
    selectedRole ? assignedUsersForRole(users, selectedRole.key) : [],
  [selectedRole, users]);

  const configuredPreview = useMemo(() =>
    draft ? userGrantPreview(selectedUser, draft.roles) : [],
  [draft, selectedUser]);

  const mutateDraft = (mutator: (next: AuthorityPolicyDocument) => void) => {
    setDraft(current => {
      if (!current) return current;
      const next = clonePolicy(current);
      mutator(next);
      return next;
    });
    setPreviewValid(false);
    setError(null);
  };

  const updateSelectedRole = (patch: Partial<AuthorityRole>) => {
    if (!selectedRoleId) return;
    mutateDraft(next => {
      const index = next.roles.findIndex(role => role.id === selectedRoleId);
      if (index >= 0) next.roles[index] = { ...next.roles[index], ...patch };
    });
  };

  const createRole = () => {
    if (!draft) return;
    const key = nextRoleKey(draft);
    const id = crypto.randomUUID();
    mutateDraft(next => {
      next.roles.push({
        id,
        key,
        name: key,
        description: '',
        grants: []
      });
    });
    setSelectedRoleId(id);
  };

  const duplicateRole = () => {
    if (!draft || !selectedRole) return;
    const key = nextRoleKey(draft, selectedRole.key + '-copy');
    const id = crypto.randomUUID();
    const copy = structuredClone(selectedRole);
    mutateDraft(next => {
      next.roles.push({
        ...copy,
        id,
        key,
        name: selectedRole.name + ' Copy'
      });
    });
    setSelectedRoleId(id);
  };

  const deleteRole = () => {
    if (!draft || !selectedRole || selectedRoleUsers.length > 0) return;
    const remaining = draft.roles.filter(role => role.id !== selectedRole.id);
    mutateDraft(next => {
      next.roles = next.roles.filter(role => role.id !== selectedRole.id);
    });
    setSelectedRoleId(remaining[0]?.id ?? null);
  };

  const addGrant = () => {
    if (!selectedRole) return;
    const first = SECURITY_CAPABILITIES[0];
    updateSelectedRole({
      grants: [...(selectedRole.grants ?? []), { capability: first.value, scope: null }]
    });
  };

  const updateGrant = (index: number, grant: AuthorityGrant) => {
    if (!selectedRole) return;
    const grants = [...(selectedRole.grants ?? [])];
    grants[index] = grant;
    updateSelectedRole({ grants });
  };

  const removeGrant = (index: number) => {
    if (!selectedRole) return;
    updateSelectedRole({
      grants: (selectedRole.grants ?? []).filter((_, grantIndex) => grantIndex !== index)
    });
  };

  const preview = async () => {
    if (!draft || busy) return;
    setBusy('preview');
    setError(null);
    try {
      const response = await authorityPolicyAdministrationApi.preview(toAuthorityMutationRequest(draft));
      setDraft(clonePolicy(response.policy));
      setPreviewValid(response.valid);
    } catch (reason) {
      setPreviewValid(false);
      setError(policyErrorMessage(reason, s));
    } finally {
      setBusy(null);
    }
  };

  const apply = async () => {
    if (!draft || !previewValid || busy) return;
    setBusy('apply');
    setError(null);
    try {
      const applied = await authorityPolicyAdministrationApi.apply(toAuthorityMutationRequest(draft));
      setBaseline(clonePolicy(applied));
      setDraft(clonePolicy(applied));
      setPreviewValid(false);
      const nextEffective = await authorityPolicyAdministrationApi.effectiveCapabilities();
      setEffective(nextEffective);
    } catch (reason) {
      setPreviewValid(false);
      setError(policyErrorMessage(reason, s));
    } finally {
      setBusy(null);
    }
  };

  if (loading) {
    return <div className="authority-policy-state">{s.loading}</div>;
  }

  if (!draft || !baseline) {
    return (
      <div className="authority-policy-state error" role="alert">
        <span>{error ?? s.backendRejected}</span>
        <button type="button" className="user-admin-button secondary" onClick={() => void load()}>{s.retry}</button>
      </div>
    );
  }

  return (
    <section className="authority-policy" data-testid="authority-policy-administration">
      <header className="authority-policy-header">
        <div>
          <span>Security Authority</span>
          <h3>{s.title}</h3>
          <p>{s.subtitle}</p>
        </div>
        <div className="authority-policy-version">
          <span>{s.policyVersion}</span>
          <strong>{draft.version}</strong>
          <small>{dirty ? s.dirty : s.clean}</small>
        </div>
      </header>

      {error && <div className="authority-policy-state error" role="alert">{error}</div>}

      <div className="authority-policy-overview">
        <div><strong>{draft.roles.length}</strong><span>{s.roles}</span></div>
        <div><strong>{draft.scopes.length}</strong><span>{s.scopes}</span></div>
        <div><strong>{configuredPreview.length}</strong><span>{s.grants}</span></div>
      </div>

      <div className="authority-policy-layout">
        <aside className="authority-role-list">
          <div className="authority-section-heading">
            <div><span>{s.roles}</span><strong>{draft.roles.length}</strong></div>
            <button type="button" className="user-admin-button primary" onClick={createRole}>{s.newRole}</button>
          </div>
          <div className="authority-role-items">
            {draft.roles.map(role => {
              const assigned = assignedUsersForRole(users, role.key);
              return (
                <button
                  type="button"
                  key={role.id ?? role.key}
                  className={`authority-role-item ${selectedRoleId === role.id ? 'selected' : ''}`}
                  onClick={() => setSelectedRoleId(role.id ?? null)}
                >
                  <strong>{role.name}</strong>
                  <code>{role.key}</code>
                  <small>{assigned.length > 0 ? `${assigned.length} ${s.assignedUsers.toLowerCase()}` : s.unassigned}</small>
                </button>
              );
            })}
          </div>

          <HierarchyTree scopes={draft.scopes} s={s} />
        </aside>

        <main className="authority-role-editor">
          {selectedRole ? (
            <>
              <div className="authority-section-heading">
                <div>
                  <span>{s.roleId}</span>
                  <code>{selectedRole.id}</code>
                </div>
                <div className="authority-inline-actions">
                  <button type="button" className="user-admin-button secondary" onClick={duplicateRole}>{s.duplicateRole}</button>
                  <button
                    type="button"
                    className="user-admin-button danger"
                    disabled={selectedRoleUsers.length > 0}
                    title={selectedRoleUsers.length > 0 ? s.deleteAssigned : undefined}
                    onClick={deleteRole}
                  >
                    {s.deleteRole}
                  </button>
                </div>
              </div>

              <div className="authority-role-fields">
                <label>
                  {s.roleKey}
                  <input value={selectedRole.key} onChange={event => updateSelectedRole({ key: event.target.value })} />
                </label>
                <label>
                  {s.roleName}
                  <input value={selectedRole.name} onChange={event => updateSelectedRole({ name: event.target.value })} />
                </label>
                <label className="authority-wide">
                  {s.description}
                  <textarea value={selectedRole.description ?? ''} rows={2} onChange={event => updateSelectedRole({ description: event.target.value })} />
                </label>
              </div>

              <div className="authority-assignment-summary">
                <strong>{s.usersWithRole}</strong>
                {selectedRoleUsers.length === 0
                  ? <span>{s.unassigned}</span>
                  : <div>{selectedRoleUsers.map(user => <code key={user.id}>{user.username}</code>)}</div>}
                {selectedRoleUsers.length > 0 && <small>{s.deleteAssigned}</small>}
              </div>

              <div className="authority-grants">
                <div className="authority-section-heading">
                  <div><span>{s.grants}</span><strong>{selectedRole.grants?.length ?? 0}</strong></div>
                  <button type="button" className="user-admin-button secondary" onClick={addGrant}>{s.addGrant}</button>
                </div>

                {(selectedRole.grants ?? []).map((grant, index) => (
                  <GrantEditor
                    key={index}
                    grant={grant}
                    scopes={draft.scopes}
                    s={s}
                    onChange={next => updateGrant(index, next)}
                    onRemove={() => removeGrant(index)}
                  />
                ))}
              </div>
            </>
          ) : <div className="authority-policy-state">{s.noRole}</div>}
        </main>
      </div>

      <div className="authority-preview-grid">
        <ConfiguredUserPreview user={selectedUser} rows={configuredPreview} scopes={draft.scopes} s={s} />
        <EffectiveCapabilityPreview effective={effective} s={s} />
      </div>

      <footer className="authority-policy-actions">
        <div className="authority-validation-status">
          <strong>{previewValid ? s.serverValidated : dirty ? s.previewRequired : s.clean}</strong>
          {previewValid && <span>{s.previewReady}</span>}
        </div>
        <div className="authority-inline-actions">
          <button
            type="button"
            className="user-admin-button secondary"
            disabled={!dirty || busy !== null}
            onClick={() => {
              setDraft(clonePolicy(baseline));
              setPreviewValid(false);
              setError(null);
            }}
          >
            {s.discard}
          </button>
          <button
            type="button"
            className="user-admin-button"
            disabled={!dirty || busy !== null}
            onClick={() => void preview()}
          >
            {busy === 'preview' ? s.previewing : s.previewChanges}
          </button>
          <button
            type="button"
            className="user-admin-button primary"
            disabled={!dirty || !previewValid || busy !== null}
            onClick={() => void apply()}
          >
            {busy === 'apply' ? s.applying : s.applyChanges}
          </button>
        </div>
      </footer>
    </section>
  );
}

function GrantEditor({
  grant,
  scopes,
  s,
  onChange,
  onRemove
}: {
  grant: AuthorityGrant;
  scopes: AuthorityScopeNode[];
  s: PolicyStrings;
  onChange: (grant: AuthorityGrant) => void;
  onRemove: () => void;
}) {
  const descriptor = capabilityDescriptor(grant.capability);
  const scopeNodeId = grant.scope?.scopeNodeId ?? '';
  const includeDescendants = grant.scope?.includeDescendants ?? false;

  const chooseScope = (id: string) => {
    if (!id) {
      onChange({ ...grant, scope: null });
      return;
    }

    onChange({
      ...grant,
      scope: {
        scopeNodeId: id,
        includeDescendants
      }
    });
  };

  return (
    <div className="authority-grant-row">
      <label>
        {s.capability}
        <select
          value={descriptor?.value ?? String(grant.capability)}
          onChange={event => {
            const numeric = Number(event.target.value);
            onChange({ ...grant, capability: Number.isInteger(numeric) ? numeric : event.target.value });
          }}
        >
          {SECURITY_CAPABILITIES.map(capability => (
            <option key={capability.value} value={capability.value}>
              {s.capabilityGroups[capability.group]} · {capability.id}
            </option>
          ))}
          {!descriptor && <option value={String(grant.capability)}>{s.unknownCapability}: {String(grant.capability)}</option>}
        </select>
      </label>

      <label>
        {s.scope}
        <select value={scopeNodeId} onChange={event => chooseScope(event.target.value)}>
          <option value="">{s.globalScope}</option>
          {scopes.map(scope => <option key={scope.id} value={scope.id}>{scope.name} · {scope.key}</option>)}
        </select>
      </label>

      <label className="authority-descendants">
        <input
          type="checkbox"
          checked={includeDescendants}
          disabled={!scopeNodeId}
          onChange={event => onChange({
            ...grant,
            scope: scopeNodeId ? { scopeNodeId, includeDescendants: event.target.checked } : null
          })}
        />
        {s.includeDescendants}
      </label>

      <button type="button" className="user-admin-button danger" onClick={onRemove}>{s.removeGrant}</button>
    </div>
  );
}

function HierarchyTree({ scopes, s }: { scopes: AuthorityScopeNode[]; s: PolicyStrings }) {
  const children = useMemo(() => {
    const map = new Map<string, AuthorityScopeNode[]>();
    for (const scope of scopes) {
      const key = scope.parentId ?? '';
      map.set(key, [...(map.get(key) ?? []), scope]);
    }
    for (const items of map.values()) items.sort((a, b) => a.name.localeCompare(b.name));
    return map;
  }, [scopes]);

  const render = (parentId: string, depth: number): React.ReactNode =>
    (children.get(parentId) ?? []).map(scope => (
      <div key={scope.id}>
        <div className="authority-scope-node" style={{ paddingLeft: depth * 14 }}>
          <strong>{scope.name}</strong>
          <code>{scope.key}</code>
          <small>{scopeKindLabel(scope.kind)} · {s.scopeStableId}: {scope.id}</small>
          {scope.resourceId && <small>{s.resourceStableId}: {scope.resourceId}</small>}
        </div>
        {render(scope.id, depth + 1)}
      </div>
    ));

  return (
    <section className="authority-hierarchy">
      <div className="authority-section-heading"><div><span>{s.hierarchy}</span><strong>{scopes.length}</strong></div></div>
      {scopes.length === 0 ? <div className="authority-policy-state">{s.noScopes}</div> : render('', 0)}
    </section>
  );
}

function ConfiguredUserPreview({
  user,
  rows,
  scopes,
  s
}: {
  user: LocalUser | null;
  rows: ReturnType<typeof userGrantPreview>;
  scopes: AuthorityScopeNode[];
  s: PolicyStrings;
}) {
  const scopeById = useMemo(() => new Map(scopes.map(scope => [scope.id, scope])), [scopes]);

  return (
    <section className="authority-preview-card">
      <span>{s.configuredPreview}</span>
      <h4>{user?.displayName ?? s.unassigned}</h4>
      <p>{s.configuredPreviewNote}</p>
      <div className="authority-preview-list">
        {rows.length === 0 && <small>{s.unassigned}</small>}
        {rows.map((row, index) => {
          const scope = row.scopeNodeId ? scopeById.get(row.scopeNodeId) : null;
          return (
            <div key={`${row.role}-${row.capability}-${row.scopeNodeId ?? 'global'}-${index}`}>
              <strong>{row.capability}</strong>
              <code>{row.role}</code>
              <small>
                {scope ? `${scope.name} (${scope.key})${row.includeDescendants ? ' + descendants' : ''}` : s.globalScope}
              </small>
            </div>
          );
        })}
      </div>
    </section>
  );
}

function EffectiveCapabilityPreview({
  effective,
  s
}: {
  effective: EffectiveCapabilities | null;
  s: PolicyStrings;
}) {
  return (
    <section className="authority-preview-card backend">
      <span>{s.currentEffective}</span>
      <h4>Backend evaluator</h4>
      <p>{s.currentEffectiveNote}</p>
      {effective && !effective.authenticationEnabled && <div className="authority-policy-state warning">{s.authDisabled}</div>}
      <CapabilityList title={s.runtime} values={effective?.runtime ?? []} />
      <CapabilityList title={s.workspace} values={effective?.workspace ?? []} />
    </section>
  );
}

function CapabilityList({ title, values }: { title: string; values: string[] }) {
  return (
    <div className="authority-effective-list">
      <strong>{title}</strong>
      <div>{values.map(value => <code key={value}>{value}</code>)}</div>
    </div>
  );
}

function scopeKindLabel(kind: number | string) {
  const numeric = typeof kind === 'number' ? kind : Number(kind);
  if (Number.isInteger(numeric)) {
    return ['Plant', 'Area', 'Equipment', 'Tag', 'Screen', 'Command'][numeric] ?? String(kind);
  }
  return String(kind);
}

function policyErrorMessage(reason: unknown, s: PolicyStrings) {
  if (!(reason instanceof AdministrationHttpError)) {
    return reason instanceof Error ? reason.message : s.backendRejected;
  }

  const suffix = reason.status === 409 ? ' ' + s.conflictHint : '';
  return `${s.backendRejected} ${reason.message}${suffix}`;
}
