import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { haAdminApi, HaAdminHttpError } from './api';
import { haCopy } from './i18n';
import type {
  HaActionKind,
  HaHostConfigurationUpdateRequest,
  HaHostConfigurationView,
  HaLocale,
  HaProtectionOperation,
  HaWorkspaceSnapshot
} from './types';
import './HighAvailabilityAdminWorkspace.css';

type Props = { locale?: HaLocale };

type Draft = HaHostConfigurationView & { peerSharedSecret: string };

const HA_STATE_NAMES = [
  'Standalone', 'Synchronizing', 'Standby', 'Ready Standby', 'Promoting',
  'Active', 'Demoting', 'Isolated', 'Maintenance', 'Faulted'
];

function stateName(value: number | string) {
  if (typeof value === 'number') return HA_STATE_NAMES[value] ?? String(value);
  return value.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
}

function asDraft(view: HaHostConfigurationView): Draft {
  return {
    ...view,
    nodes: view.nodes.map(node => ({ ...node })),
    peerTransport: { ...view.peerTransport },
    protection: { ...view.protection },
    peerSharedSecret: ''
  };
}

function sameConfig(a: HaHostConfigurationView, b: HaHostConfigurationView) {
  return JSON.stringify(a) === JSON.stringify(b);
}

function latestContact(snapshot: HaWorkspaceSnapshot) {
  const peer = snapshot.peer;
  const candidates = [peer.lastInboundAtUtc, peer.lastOutboundSuccessAtUtc].filter(Boolean) as string[];
  return candidates.sort().at(-1) ?? null;
}

function relativeTime(value?: string | null) {
  if (!value) return '—';
  const age = Math.max(0, Date.now() - new Date(value).getTime());
  if (age < 1_000) return '<1s';
  if (age < 60_000) return Math.round(age / 1_000) + 's';
  return Math.round(age / 60_000) + 'm';
}

export function HighAvailabilityAdminWorkspace({ locale = 'pt-BR' }: Props) {
  const t = haCopy(locale);
  const [snapshot, setSnapshot] = useState<HaWorkspaceSnapshot | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<{ kind: HaActionKind; target?: string | null } | null>(null);
  const [activeOperation, setActiveOperation] = useState<HaProtectionOperation | null>(null);

  const load = useCallback(async (preserveDraft = false) => {
    setLoading(true);
    setFailure(null);
    try {
      const next = await haAdminApi.workspace();
      setSnapshot(next);
      if (!preserveDraft) setDraft(asDraft(next.configuration.desired));
    } catch (error) {
      setFailure(error instanceof Error ? error.message : t.loadError);
    } finally {
      setLoading(false);
    }
  }, [t.loadError]);

  useEffect(() => { void load(); }, [load]);

  const desiredChanged = useMemo(() => {
    if (!snapshot || !draft) return false;
    const { peerSharedSecret: _secret, ...view } = draft;
    return !sameConfig(view, snapshot.configuration.desired) || draft.peerSharedSecret.length > 0;
  }, [snapshot, draft]);

  const validation = useMemo(() => {
    if (!draft) return [] as string[];
    const errors: string[] = [];
    if (draft.freshnessSeconds < 1 || draft.freshnessSeconds > 120) errors.push('freshnessSeconds: 1–120');
    if (draft.protection.leaseSeconds < 3 || draft.protection.leaseSeconds > 120) errors.push('leaseSeconds: 3–120');
    if (draft.protection.pollMilliseconds < 100 || draft.protection.pollMilliseconds > 10_000) errors.push('pollMilliseconds: 100–10000');
    if (draft.protection.readyWitnessMaximumAgeSeconds < draft.protection.leaseSeconds ||
        draft.protection.readyWitnessMaximumAgeSeconds > 600) errors.push('readyWitnessMaximumAgeSeconds: lease–600');
    if (draft.protection.clockSkewSafetyMarginSeconds < 0 ||
        draft.protection.clockSkewSafetyMarginSeconds > Math.floor(draft.protection.leaseSeconds / 3)) {
      errors.push('clockSkewSafetyMarginSeconds: 0–lease/3');
    }
    if (draft.peerSharedSecret && new TextEncoder().encode(draft.peerSharedSecret).length < 32) errors.push('peer shared secret: ≥32 bytes');
    if (draft.enabled && draft.nodes.length !== 2) errors.push('Wave 15 HA requires exactly two nodes');
    return errors;
  }, [draft]);

  function updateNode(index: number, key: 'nodeId' | 'localEndpoint' | 'remoteEndpoint', value: string) {
    setDraft(current => current ? ({
      ...current,
      nodes: current.nodes.map((node, i) => i === index ? { ...node, [key]: value } : node)
    }) : current);
  }

  async function saveConfiguration() {
    if (!snapshot || !draft || validation.length) return;
    setSaving(true);
    setFailure(null);
    setNotice(null);
    const secret = draft.peerSharedSecret;
    setDraft(current => current ? { ...current, peerSharedSecret: '' } : current);
    const request: HaHostConfigurationUpdateRequest = {
      expectedGeneration: snapshot.configuration.generation,
      enabled: draft.enabled,
      clusterId: draft.clusterId || null,
      localNodeId: draft.localNodeId,
      initialActiveNodeId: draft.initialActiveNodeId || null,
      topologyVersion: draft.topologyVersion,
      freshnessSeconds: draft.freshnessSeconds,
      nodes: draft.nodes.map(node => ({ ...node })),
      peerTransport: {
        enabled: draft.peerTransport.enabled,
        peerEndpoint: draft.peerTransport.peerEndpoint || null,
        ...(secret ? { peerSharedSecret: secret } : {}),
        clearPeerSharedSecret: false
      },
      protection: { ...draft.protection, referencePath: draft.protection.referencePath || null }
    };
    try {
      const result = await haAdminApi.updateConfiguration(request);
      setNotice(result.snapshot.pendingRestart ? t.savedNotActive : t.saved);
      await load(false);
    } catch (error) {
      const http = error instanceof HaAdminHttpError ? error : null;
      setFailure([http?.message || t.configurationRejected, ...(http?.errors ?? [])].filter(Boolean).join(' · '));
      await load(true);
    } finally {
      setSaving(false);
    }
  }

  async function pollOperation(operation: HaProtectionOperation) {
    setActiveOperation(operation);
    if (operation.state !== 'running') return;
    for (let attempt = 0; attempt < 20; attempt += 1) {
      await new Promise(resolve => setTimeout(resolve, 750));
      const next = await haAdminApi.operation(operation.operationId);
      setActiveOperation(next);
      if (next.state !== 'running') {
        await load(false);
        return;
      }
    }
  }

  async function runConfirmedAction() {
    if (!confirm) return;
    const action = confirm;
    setConfirm(null);
    setFailure(null);
    setNotice(null);
    try {
      const operation = await haAdminApi.action(action.kind, action.target);
      await pollOperation(operation);
      await load(false);
    } catch (error) {
      setFailure(error instanceof Error ? error.message : String(error));
    }
  }

  if (loading && !snapshot) return <div className="ha-admin ha-admin--loading">{t.title}…</div>;
  if (!snapshot || !draft) return <div className="ha-admin ha-admin--error" role="alert">{failure || t.loadError}</div>;

  const { topology, authority, administration, configuration, peer } = snapshot;
  const protection = administration.protection;
  const nodes = topology.nodes;
  const local = nodes.find(node => node.nodeId.toLowerCase() === topology.localNodeId.toLowerCase());
  const peerNode = nodes.find(node => node.nodeId.toLowerCase() !== topology.localNodeId.toLowerCase());
  const status = topology.ambiguousAuthority ? 'ambiguous'
    : authority.blocked ? 'blocked'
      : protection.status;
  const operations = activeOperation
    ? [activeOperation, ...administration.operations.filter(op => op.operationId !== activeOperation.operationId)]
    : administration.operations;
  const suggestedTarget = peerNode?.nodeId ?? '';

  return (
    <section className="ha-admin" data-testid="ha-admin-workspace">
      <header className="ha-admin__header">
        <div>
          <span className="ha-admin__eyebrow">Engineering · HA</span>
          <h1>{t.title}</h1>
          <p>{t.subtitle}</p>
        </div>
        <button type="button" className="ha-button" onClick={() => void load(false)}>{t.refresh}</button>
      </header>

      {(failure || notice) && (
        <div className={failure ? 'ha-banner ha-banner--danger' : 'ha-banner ha-banner--info'} role={failure ? 'alert' : 'status'}>
          {failure || notice}
        </div>
      )}

      {configuration.pendingRestart && (
        <div className="ha-banner ha-banner--warning" data-testid="ha-restart-required">
          <strong>{t.restartRequired}</strong>
          <span>{t.savedNotActive}</span>
        </div>
      )}

      <div className="ha-admin__status-grid">
        <article className="ha-card ha-card--status">
          <span>{t.status}</span>
          <strong className={'ha-state ha-state--' + status}>{status}</strong>
          <small>{protection.reasonCode || peer.reasonCode || '—'}</small>
        </article>
        <article className="ha-card"><span>{t.effectiveActive}</span><strong>{topology.effectiveActiveNodeId || '—'}</strong><small>{t.epoch}: {topology.authorityEpoch}</small></article>
        <article className="ha-card"><span>{t.localNode}</span><strong>{topology.localNodeId}</strong><small>{local ? stateName(local.state) : '—'} · {local?.fresh ? t.fresh : t.stale}</small></article>
        <article className="ha-card"><span>{t.peerNode}</span><strong>{peer.peerNodeId || peerNode?.nodeId || '—'}</strong><small>{peerNode ? stateName(peerNode.state) : '—'} · {peer.connectionState} · {relativeTime(latestContact(snapshot))}</small></article>
        <article className="ha-card"><span>{t.topologyVersion}</span><strong>{topology.topologyVersion}</strong><small>state v{topology.stateVersion}</small></article>
        <article className="ha-card"><span>{t.generation}</span><strong>{configuration.generation}</strong><small>{configuration.pendingRestart ? t.restartRequired : configuration.applyMode}</small></article>
      </div>

      <div className="ha-admin__columns">
        <section className="ha-panel">
          <div className="ha-panel__title"><div><h2>{t.configuration}</h2><p>{t.running} ≠ {t.desired} quando há mudança aguardando restart.</p></div></div>
          <div className="ha-config-compare">
            <div className="ha-config-compare__running">
              <h3>{t.running}</h3>
              <code>{configuration.running.clusterId || '—'} · v{configuration.running.topologyVersion}</code>
              <span>{configuration.running.peerTransport.authenticationConfigured ? t.authenticationConfigured : 'Authentication not configured'}</span>
            </div>
            <div className="ha-config-compare__desired">
              <h3>{t.desired}</h3>
              <code>{configuration.desired.clusterId || '—'} · v{configuration.desired.topologyVersion}</code>
              <span>{configuration.desired.peerTransport.authenticationConfigured ? t.authenticationConfigured : 'Authentication not configured'}</span>
            </div>
          </div>

          <div className="ha-form-grid">
            <label>{t.enabled}<input type="checkbox" checked={draft.enabled} onChange={e => setDraft({ ...draft, enabled: e.target.checked })} /></label>
            <label>{t.clusterId}<input value={draft.clusterId || ''} onChange={e => setDraft({ ...draft, clusterId: e.target.value })} /></label>
            <label>{t.localNodeId}<input value={draft.localNodeId} onChange={e => setDraft({ ...draft, localNodeId: e.target.value })} /></label>
            <label>{t.initialActiveNodeId}<input value={draft.initialActiveNodeId || ''} onChange={e => setDraft({ ...draft, initialActiveNodeId: e.target.value })} /></label>
            <label>{t.topologyVersion}<input type="number" min={1} value={draft.topologyVersion} onChange={e => setDraft({ ...draft, topologyVersion: Number(e.target.value) })} /></label>
            <label>{t.freshnessSeconds}<input type="number" min={1} max={120} value={draft.freshnessSeconds} onChange={e => setDraft({ ...draft, freshnessSeconds: Number(e.target.value) })} /></label>
          </div>

          <h3>{t.nodes}</h3>
          <div className="ha-node-grid">
            {draft.nodes.map((node, index) => (
              <fieldset key={index} className="ha-node">
                <legend>{node.nodeId || 'Node ' + (index + 1)}</legend>
                <label>Node ID<input value={node.nodeId} onChange={e => updateNode(index, 'nodeId', e.target.value)} /></label>
                <label>{t.localEndpoint}<input value={node.localEndpoint || ''} onChange={e => updateNode(index, 'localEndpoint', e.target.value)} /></label>
                <label>{t.remoteEndpoint}<input value={node.remoteEndpoint || ''} onChange={e => updateNode(index, 'remoteEndpoint', e.target.value)} /></label>
              </fieldset>
            ))}
          </div>

          <h3>{t.peerTransport}</h3>
          <div className="ha-form-grid">
            <label>{t.enabled}<input type="checkbox" checked={draft.peerTransport.enabled} onChange={e => setDraft({ ...draft, peerTransport: { ...draft.peerTransport, enabled: e.target.checked } })} /></label>
            <label>{t.peerEndpoint}<input value={draft.peerTransport.peerEndpoint || ''} onChange={e => setDraft({ ...draft, peerTransport: { ...draft.peerTransport, peerEndpoint: e.target.value } })} /></label>
            <label className="ha-field--wide">{t.newSecret}<input data-testid="ha-peer-secret" type="password" autoComplete="new-password" value={draft.peerSharedSecret} onChange={e => setDraft({ ...draft, peerSharedSecret: e.target.value })} /><small>{t.secretHint}</small></label>
          </div>
          <div className="ha-secret-state" data-testid="ha-authentication-state">{draft.peerTransport.authenticationConfigured ? t.authenticationConfigured : 'Authentication not configured'}</div>

          <h3>{t.protection}</h3>
          <div className="ha-reference-note"><strong>{t.referenceStore}</strong><span>{t.referenceHint}</span><small>{configuration.referenceStoreRequirements.mode} · fail-closed: {configuration.referenceStoreRequirements.failClosedWhenUnavailable ? t.yes : t.no}</small></div>
          <div className="ha-form-grid">
            <label>{t.enabled}<input type="checkbox" checked={draft.protection.enabled} onChange={e => setDraft({ ...draft, protection: { ...draft.protection, enabled: e.target.checked } })} /></label>
            <label>{t.automaticFailover}<input type="checkbox" checked={draft.protection.automaticFailoverEnabled} onChange={e => setDraft({ ...draft, protection: { ...draft.protection, automaticFailoverEnabled: e.target.checked } })} /></label>
            <label className="ha-field--wide">{t.referencePath}<input value={draft.protection.referencePath || ''} onChange={e => setDraft({ ...draft, protection: { ...draft.protection, referencePath: e.target.value } })} /></label>
            <label>{t.leaseSeconds}<input type="number" min={3} max={120} value={draft.protection.leaseSeconds} onChange={e => setDraft({ ...draft, protection: { ...draft.protection, leaseSeconds: Number(e.target.value) } })} /></label>
            <label>{t.pollMilliseconds}<input type="number" min={100} max={10000} value={draft.protection.pollMilliseconds} onChange={e => setDraft({ ...draft, protection: { ...draft.protection, pollMilliseconds: Number(e.target.value) } })} /></label>
            <label>{t.witnessSeconds}<input type="number" min={draft.protection.leaseSeconds} max={600} value={draft.protection.readyWitnessMaximumAgeSeconds} onChange={e => setDraft({ ...draft, protection: { ...draft.protection, readyWitnessMaximumAgeSeconds: Number(e.target.value) } })} /></label>
            <label>{t.skewSeconds}<input type="number" min={0} max={Math.floor(draft.protection.leaseSeconds / 3)} value={draft.protection.clockSkewSafetyMarginSeconds} onChange={e => setDraft({ ...draft, protection: { ...draft.protection, clockSkewSafetyMarginSeconds: Number(e.target.value) } })} /></label>
          </div>
          {validation.length > 0 && <div className="ha-validation" role="alert">{validation.join(' · ')}</div>}
          <button type="button" className="ha-button ha-button--primary" disabled={!desiredChanged || saving || validation.length > 0} onClick={() => void saveConfiguration()}>{saving ? t.saving : t.save}</button>
        </section>

        <aside className="ha-panel">
          <div className="ha-panel__title"><div><h2>{t.operations}</h2><p>Backend-authoritative, audited and fail-closed.</p></div></div>
          <div className="ha-action-row">
            <button type="button" className="ha-button" onClick={() => setConfirm({ kind: 'switchover', target: suggestedTarget })}>{t.switchover}</button>
            <button type="button" className="ha-button" onClick={() => setConfirm({ kind: 'failback', target: draft.initialActiveNodeId })}>{t.failback}</button>
            <button type="button" className="ha-button" onClick={() => setConfirm({ kind: 'recovery', target: topology.localNodeId })}>{t.recovery}</button>
          </div>

          <dl className="ha-diagnostics">
            <div><dt>{t.fencing}</dt><dd>{protection.reference ? `${protection.reference.activeNodeId || 'fenced'} · epoch ${protection.reference.epoch}` : '—'}</dd></div>
            <div><dt>{t.peerTransport}</dt><dd>{peer.connectionState} · {peer.authenticationConfigured ? t.authenticationConfigured : 'auth unavailable'}</dd></div>
            <div><dt>Mirror</dt><dd>{peer.mirror.liveSynchronized ? 'live synchronized' : peer.mirror.reasonCode || 'not synchronized'}</dd></div>
            <div><dt>{t.automaticFailover}</dt><dd>{protection.automaticFailoverEnabled ? t.enabled : t.disabled}</dd></div>
          </dl>

          <div className="ha-operation-list" data-testid="ha-operation-list">
            {operations.length === 0 ? <p>{t.noOperations}</p> : operations.map(operation => (
              <article className={'ha-operation ha-operation--' + operation.state} key={operation.operationId}>
                <div><strong>{operation.kind}</strong><span>{operation.state}</span></div>
                <dl>
                  <div><dt>{t.source}</dt><dd>{operation.sourceNodeId || '—'}</dd></div>
                  <div><dt>{t.target}</dt><dd>{operation.targetNodeId || '—'}</dd></div>
                  <div><dt>{t.epoch}</dt><dd>{operation.epoch ?? '—'}</dd></div>
                  <div><dt>{operation.state === 'completed' ? t.result : t.error}</dt><dd>{operation.reasonCode}</dd></div>
                </dl>
              </article>
            ))}
          </div>
        </aside>
      </div>

      {confirm && (
        <div className="ha-modal-backdrop" role="presentation">
          <div className="ha-modal" role="dialog" aria-modal="true" aria-labelledby="ha-confirm-title">
            <h2 id="ha-confirm-title">{t.confirmTitle}</h2>
            <p>{t.confirmHint}</p>
            <dl>
              <div><dt>{t.requested}</dt><dd>{confirm.kind}</dd></div>
              <div><dt>{t.source}</dt><dd>{topology.effectiveActiveNodeId || topology.localNodeId}</dd></div>
              <div><dt>{t.target}</dt><dd>{confirm.target || 'backend default'}</dd></div>
              <div><dt>{t.epoch}</dt><dd>{topology.authorityEpoch}</dd></div>
            </dl>
            <div className="ha-modal__actions">
              <button type="button" className="ha-button" onClick={() => setConfirm(null)}>{t.cancel}</button>
              <button type="button" className="ha-button ha-button--danger" data-testid="ha-confirm-action" onClick={() => void runConfirmedAction()}>{t.confirm}</button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
