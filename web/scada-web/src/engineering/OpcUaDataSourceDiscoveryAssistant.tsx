import React, { useEffect, useMemo, useState } from 'react';
import {
  isProtectedReference,
  type DataSourceTypeDefinition
} from './DataSourceCatalogEditor.logic';
import { c04DataSourceToolingText } from './c04DataSourceToolingI18n';
import {
  discoverEngineeringDataSourceDraft,
  testEngineeringDataSourceDraftConnection,
  type DriverConnectionTestResultView,
  type DriverDiscoveryCandidateView,
  type DriverDraftDataSourceView
} from './driverEngineeringApi';
import type { EngineeringLocale } from './i18n';
import type { DataSourceEngineering } from './types';

type Props = {
  draft: DataSourceEngineering;
  definition: DataSourceTypeDefinition;
  locale: EngineeringLocale;
  onChange: (draft: DataSourceEngineering) => void;
};

export function OpcUaDataSourceDiscoveryAssistant({ draft, definition, locale, onChange }: Props) {
  const text = useMemo(() => c04DataSourceToolingText(locale), [locale]);
  const [discoveryUrl, setDiscoveryUrl] = useState(draft.settings?.endpointUrl ?? '');
  const [candidates, setCandidates] = useState<DriverDiscoveryCandidateView[]>([]);
  const [connection, setConnection] = useState<DriverConnectionTestResultView | null>(null);
  const [busy, setBusy] = useState<'discover' | 'test' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    const endpoint = draft.settings?.endpointUrl?.trim();
    if (endpoint) setDiscoveryUrl(endpoint);
    setConnection(null);
  }, [draft.driver, draft.key, draft.settings]);

  if (definition.typeKey.toLowerCase() !== 'opc-ua') return null;
  if (!definition.capabilities.supportsDiscovery && !definition.capabilities.supportsConnectionTest) return null;

  const requestContext = (): DriverDraftDataSourceView | null => {
    if (!draft.key.trim()) {
      setError(text.keyRequired);
      return null;
    }
    return {
      sourceKey: draft.key.trim(),
      sourceName: draft.name.trim() || draft.key.trim(),
      driverType: draft.driver,
      settings: { ...(draft.settings ?? {}) },
      secretReferences: { ...(draft.secretReferences ?? {}) }
    };
  };

  const discover = async () => {
    const context = requestContext();
    if (!context) return;
    if (!discoveryUrl.trim()) {
      setError(text.discoveryUrlRequired);
      return;
    }

    setBusy('discover');
    setError(null);
    setNotice(null);
    setCandidates([]);
    try {
      const result = await discoverEngineeringDataSourceDraft(context, {
        parameters: { discoveryUrl: discoveryUrl.trim() },
        maximumResults: 100
      });
      setCandidates(result);
      if (result.length === 0) setNotice(text.noCandidates);
    } catch (reason) {
      setError(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const testConnection = async () => {
    const context = requestContext();
    if (!context) return;

    setBusy('test');
    setError(null);
    setNotice(null);
    setConnection(null);
    try {
      setConnection(await testEngineeringDataSourceDraftConnection(context));
    } catch (reason) {
      setError(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const useCandidate = (candidate: DriverDiscoveryCandidateView) => {
    const allowedFields = new Map(
      (definition.configurationSchema?.dataSourceFields ?? [])
        .filter(field => !isProtectedReference(field.valueKind))
        .map(field => [field.key.toLowerCase(), field.key] as const));
    const nextSettings = { ...(draft.settings ?? {}) };
    let ignored = false;

    for (const [key, value] of Object.entries(candidate.suggestedSettings ?? {})) {
      const canonicalKey = allowedFields.get(key.toLowerCase());
      if (!canonicalKey) {
        ignored = true;
        continue;
      }
      nextSettings[canonicalKey] = value;
    }

    onChange({ ...draft, settings: nextSettings });
    setConnection(null);
    setNotice(ignored ? `${text.selected} ${text.catalogMismatch}` : text.selected);
    const endpoint = candidate.suggestedSettings?.endpointUrl ?? candidate.sanitizedEndpoint;
    if (endpoint) setDiscoveryUrl(endpoint);
  };

  return (
    <section className="eng-dictionary-editor" data-testid="opcua-source-discovery-assistant">
      <header>
        <strong>{text.title}</strong>
        <span>{text.help}</span>
      </header>

      {definition.capabilities.supportsDiscovery && <>
        <label className="eng-editor-field eng-editor-field-wide">
          <span>{text.discoveryUrl}</span>
          <input
            className="mono"
            value={discoveryUrl}
            onChange={event => setDiscoveryUrl(event.target.value)}
            placeholder="opc.tcp://server:4840"
            data-testid="opcua-source-discovery-url"
          />
          <small>{text.discoveryUrlHelp}</small>
        </label>
        <div className="eng-editor-actions">
          <button
            type="button"
            className="secondary"
            disabled={busy !== null}
            onClick={() => void discover()}
            data-testid="opcua-source-discover"
          >
            {busy === 'discover' ? text.discovering : text.discover}
          </button>
        </div>
      </>}

      {candidates.length > 0 && <section className="eng-preview-panel" data-testid="opcua-source-discovery-results">
        <header><strong>{text.candidates}</strong><span>{candidates.length}</span></header>
        <div className="eng-bulk-entities">
          {candidates.map(candidate => {
            const suggested = candidate.suggestedSettings ?? {};
            return <div key={candidate.candidateId}>
              <strong>{candidate.displayName}</strong>
              <code>{candidate.sanitizedEndpoint ?? suggested.endpointUrl ?? candidate.stableIdentity}</code>
              {suggested.securityMode && <small>{text.securityMode}: {suggested.securityMode}</small>}
              {suggested.securityPolicyUri && <small>{text.securityPolicy}: {suggested.securityPolicyUri}</small>}
              {suggested.authenticationMode && <small>{text.authentication}: {suggested.authenticationMode}</small>}
              {suggested.serverCertificateSha256 && <small>{text.certificateFingerprint}: <code>{suggested.serverCertificateSha256}</code></small>}
              <button
                type="button"
                className="secondary"
                onClick={() => useCandidate(candidate)}
                data-testid={`opcua-source-use-${candidate.candidateId}`}
              >
                {text.useCandidate}
              </button>
            </div>;
          })}
        </div>
        <small>{text.trustNote}</small>
      </section>}

      {definition.capabilities.supportsConnectionTest && <div className="eng-editor-actions">
        <button
          type="button"
          className="secondary"
          disabled={busy !== null}
          onClick={() => void testConnection()}
          data-testid="opcua-source-test"
        >
          {busy === 'test' ? text.testing : text.test}
        </button>
      </div>}

      {connection && <div className="eng-mutation-detail" data-testid="opcua-source-test-result">
        <strong>{connection.succeeded ? text.connectionOk : text.connectionFailed}</strong>
        {connection.sanitizedEndpoint && <code>{connection.sanitizedEndpoint}</code>}
        {connection.observedIdentity && <span>{connection.observedIdentity}</span>}
      </div>}
      {notice && <small role="status">{notice}</small>}
      {error && <pre className="eng-preview-error" role="alert">{error}</pre>}
    </section>
  );
}

function asMessage(reason: unknown): string {
  return reason instanceof Error ? reason.message : String(reason);
}
