import React, { useEffect, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import {
  testEngineeringDataSourceConnection,
  testEngineeringDataSourceDraftConnection,
  type DriverConnectionTestResultView,
  type DriverDraftDataSourceView
} from './driverEngineeringApi';
import type { DataSourceEngineering } from './types';

type Props = Readonly<{
  draft: DataSourceEngineering;
  persistedId?: string;
  unchanged: boolean;
  enabled: boolean;
  locale: EngineeringLocale;
}>;

export function DataSourceConnectionTest({ draft, persistedId, unchanged, enabled, locale }: Props) {
  const [result, setResult] = useState<DriverConnectionTestResultView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setResult(null);
    setError(null);
  }, [draft]);

  const test = async () => {
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      const response = persistedId && unchanged
        ? await testEngineeringDataSourceConnection(persistedId)
        : await testEngineeringDataSourceDraftConnection(toDraft(draft));
      setResult(response);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };

  const text = locale === 'en'
    ? { title: 'Connection test', button: 'Test connection', busy: 'Testing…', connected: 'Connection test succeeded', failed: 'Connection test failed', draft: 'Tests this unsaved draft; it does not apply changes.', saved: 'Tests the saved Data Source configuration.', issues: 'Diagnostic details' }
    : locale === 'es'
      ? { title: 'Prueba de conexión', button: 'Probar conexión', busy: 'Probando…', connected: 'Prueba de conexión correcta', failed: 'Prueba de conexión fallida', draft: 'Prueba este borrador sin guardar; no aplica cambios.', saved: 'Prueba la configuración guardada de la Fuente de datos.', issues: 'Detalles del diagnóstico' }
      : { title: 'Teste de conexão', button: 'Testar conexão', busy: 'Testando…', connected: 'Teste de conexão concluído com sucesso', failed: 'Teste de conexão falhou', draft: 'Testa este rascunho sem aplicar as alterações.', saved: 'Testa a configuração salva da Fonte de dados.', issues: 'Detalhes do diagnóstico' };

  return <section className="eng-preview-panel" data-testid="data-source-connection-test">
    <header><strong>{text.title}</strong><span>{persistedId && unchanged ? text.saved : text.draft}</span></header>
    <div className="eng-editor-actions">
      <button type="button" className="secondary" disabled={!enabled || busy} onClick={() => void test()}>
        {busy ? text.busy : text.button}
      </button>
    </div>
    {result && <div className={result.succeeded ? 'eng-mutation-detail' : 'eng-preview-error'} role="status" data-testid="data-source-connection-result">
      <strong>{result.succeeded ? text.connected : text.failed}</strong>
      {result.sanitizedEndpoint && <code>{result.sanitizedEndpoint}</code>}
      {result.observedIdentity && <span>{result.observedIdentity}</span>}
      {result.issues?.length ? <div><small>{text.issues}</small>{result.issues.map((issue, index) =>
        <p key={`${issue.code}-${index}`}><code>{issue.code}</code> {issue.message}</p>)}</div> : null}
    </div>}
    {error && <pre className="eng-preview-error" role="alert">{error}</pre>}
  </section>;
}

function toDraft(source: DataSourceEngineering): DriverDraftDataSourceView {
  return {
    sourceKey: source.key.trim(),
    sourceName: source.name.trim() || source.key.trim(),
    driverType: source.driver,
    settings: { ...(source.settings ?? {}) },
    secretReferences: { ...(source.secretReferences ?? {}) }
  };
}
