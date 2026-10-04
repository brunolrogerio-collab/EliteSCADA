import React, { useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import { testEngineeringDraftPointRead, testEngineeringPointRead, type DriverPointReadSampleView, type DriverPointReadTestResultView, type DriverPointReadTestStatusView } from './driverEngineeringApi';
import { resolveTagDataSource, type TagSourceAwareEngineering } from './TagSourceSelector.logic';
import type { DataSourceEngineering } from './types';

type Props = Readonly<{ tag: TagSourceAwareEngineering; sources: readonly DataSourceEngineering[]; locale: EngineeringLocale; persisted: boolean; onChange: (tag: TagSourceAwareEngineering) => void }>;

export function TagCommissioningPanel({ tag, sources, locale, persisted, onChange }: Props) {
  const copy = useMemo(() => commissioningCopy(locale), [locale]);
  const source = resolveTagDataSource(tag, sources).source;
  const binding = tag.communicationBinding;
  const [busy, setBusy] = useState<'single' | 'monitor' | null>(null);
  const [result, setResult] = useState<DriverPointReadTestResultView | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!source || !binding) return <section className="eng-dictionary-editor eng-editor-field-wide" data-testid="tag-commissioning"><header><strong>{copy.title}</strong><span>{copy.configureFirst}</span></header></section>;

  const transform = binding.valueTransform ?? { contractVersion: 1, byteSwap: false, wordSwap: false };
  const setTransform = (field: 'byteSwap' | 'wordSwap', value: boolean) => {
    onChange({ ...tag, communicationBinding: { ...binding, valueTransform: { contractVersion: 1, byteSwap: field === 'byteSwap' ? value : Boolean(transform.byteSwap), wordSwap: field === 'wordSwap' ? value : Boolean(transform.wordSwap) } } });
    setResult(null); setError(null);
  };
  const run = async (kind: 'single' | 'monitor') => {
    setBusy(kind); setError(null);
    try {
      const request = { binding, dataType: tag.dataType, addressSelector: tag.addressSelector ?? null, engineeringUnit: tag.engineeringUnit ?? null, sampleCount: kind === 'monitor' ? 5 : 1, sampleIntervalMilliseconds: kind === 'monitor' ? 500 : 0, timeoutMilliseconds: kind === 'monitor' ? 7000 : 5000 };
      const result = persisted && source.id
        ? await testEngineeringPointRead(source.id, request)
        : await testEngineeringDraftPointRead(
            { sourceKey: source.key, sourceName: source.name, driverType: source.driver, settings: source.settings ?? {}, secretReferences: source.secretReferences ?? {} },
            request);
      setResult(result);
    } catch (reason) { setResult(null); setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(null); }
  };

  return <section className="eng-dictionary-editor eng-editor-field-wide" data-testid="tag-commissioning">
    <header><strong>{copy.title}</strong><span>{copy.help}</span></header>
    <div className="eng-editor-form-grid">
      <label className="eng-editor-field"><span>{copy.byteSwap}</span><input type="checkbox" checked={Boolean(transform.byteSwap)} onChange={e => setTransform('byteSwap', e.currentTarget.checked)} data-testid="tag-commissioning-byte-swap" /></label>
      <label className="eng-editor-field"><span>{copy.wordSwap}</span><input type="checkbox" checked={Boolean(transform.wordSwap)} onChange={e => setTransform('wordSwap', e.currentTarget.checked)} data-testid="tag-commissioning-word-swap" /></label>
    </div>
    <div className="eng-editor-actions">
      <button type="button" className="secondary" disabled={busy !== null} onClick={() => void run('single')} data-testid="tag-test-read">{busy === 'single' ? copy.testing : copy.testRead}</button>
      <button type="button" className="secondary" disabled={busy !== null} onClick={() => void run('monitor')} data-testid="tag-short-monitor">{busy === 'monitor' ? copy.monitoring : copy.shortMonitor}</button>
      {persisted ? <a href="/engineering/monitor" data-testid="tag-development-monitor-handoff">{copy.developmentMonitor}</a> : null}
    </div>
    {result ? <PointReadEvidence result={result} locale={locale} /> : null}
    {error ? <pre className="eng-preview-error" role="alert" data-testid="tag-test-read-error">{error}</pre> : null}
  </section>;
}

function PointReadEvidence({ result, locale }: Readonly<{ result: DriverPointReadTestResultView; locale: EngineeringLocale }>) {
  const copy = commissioningCopy(locale); const sample = result.samples.at(-1) ?? null; const stateCopy = copy.states[normalizeStatus(result.status)];
  return <div className="eng-mutation-detail" data-testid="tag-test-read-result">
    <div data-testid="tag-test-read-state" role="status"><strong><span aria-hidden="true">{stateCopy.icon}</span> {stateCopy.label}</strong></div>
    <dl>
      <dt>{copy.endpoint}</dt><dd><code>{result.sanitizedEndpoint ?? '—'}</code></dd><dt>{copy.address}</dt><dd><code>{result.portableAddress}</code></dd>
      <dt>{copy.quality}</dt><dd>{sample ? String(sample.quality) : '—'}</dd><dt>{copy.observed}</dt><dd>{formatTimestamp(sample?.observedAtUtc, locale)}</dd>
      <dt>{copy.sourceTimestamp}</dt><dd>{formatTimestamp(sample?.sourceTimestampUtc, locale)}</dd><dt>{copy.latency}</dt><dd>{sample?.latencyMilliseconds == null ? '—' : sample.latencyMilliseconds.toFixed(1) + ' ms'}</dd>
      <dt>{copy.raw}</dt><dd><RawEvidence sample={sample} /></dd><dt>{copy.decoded}</dt><dd><ValueEvidence sample={sample} engineering={false} /></dd>
      <dt>{copy.engineering}</dt><dd><ValueEvidence sample={sample} engineering /></dd><dt>{copy.transform}</dt><dd>{formatTransform(sample)}</dd>
      <dt>{copy.summary}</dt><dd>{formatSummary(result, copy)}</dd>
    </dl><IssueList result={result} copy={copy} />
  </div>;
}
function RawEvidence({ sample }: { sample: DriverPointReadSampleView | null }) {
  if (!sample?.raw) return <>—</>;
  return <span><strong>{sample.raw.kind}</strong>{sample.raw.hex ? <> <code>{sample.raw.hex}</code></> : null}{sample.raw.elements?.length ? <> <code>{sample.raw.elements.join(' ')}</code></> : null}{sample.raw.metadata ? <small>{Object.entries(sample.raw.metadata).map(p => p[0] + '=' + p[1]).join(' · ')}</small> : null}</span>;
}
function ValueEvidence({ sample, engineering }: { sample: DriverPointReadSampleView | null; engineering: boolean }) {
  const value = engineering ? sample?.engineering : sample?.decoded;
  if (!value || value.value == null) return <>—</>;
  const rendered = typeof value.value === 'string' ? value.value : JSON.stringify(value.value);
  return <span><code>{rendered}</code><small>{value.valueType}{value.engineeringUnit ? ' · ' + value.engineeringUnit : ''}</small></span>;
}
function IssueList({ result, copy }: { result: DriverPointReadTestResultView; copy: ReturnType<typeof commissioningCopy> }) {
  const issues = [...(result.issues ?? []), ...result.samples.flatMap(s => s.issues ?? [])];
  const unique = [...new Map(issues.map(i => [i.code + '\u0000' + i.message, i])).values()];
  return unique.length ? <div data-testid="tag-test-read-issues"><strong>{copy.issues}</strong><ul>{unique.map(i => <li key={i.code + ':' + i.message}><code>{i.code}</code> {i.message}</li>)}</ul></div> : null;
}
function normalizeStatus(status: DriverPointReadTestStatusView): 'good' | 'bad' | 'noData' | 'uncertain' {
  if (status === 0 || String(status).toLowerCase() === 'good') return 'good';
  if (status === 2 || String(status).toLowerCase() === 'nodata') return 'noData';
  if (status === 3 || String(status).toLowerCase() === 'intermittentoruncertain') return 'uncertain';
  return 'bad';
}
function formatTimestamp(value: string | null | undefined, locale: EngineeringLocale) { if (!value) return '—'; const d = new Date(value); return Number.isNaN(d.getTime()) ? value : new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(d); }
function formatTransform(sample: DriverPointReadSampleView | null) { const t = sample?.effectiveValueTransform; return t ? 'Byte Swap=' + (t.byteSwap ? 'ON' : 'OFF') + ' · Word Swap=' + (t.wordSwap ? 'ON' : 'OFF') : '—'; }
function formatSummary(result: DriverPointReadTestResultView, copy: ReturnType<typeof commissioningCopy>) { const s = result.summary; return s.completedSamples + '/' + s.requestedSamples + ' · ' + copy.good + ': ' + s.goodSamples + ' · ' + copy.bad + ': ' + s.badSamples + ' · ' + copy.noData + ': ' + s.noDataSamples + ' · ' + copy.uncertain + ': ' + s.uncertainSamples; }

function commissioningCopy(locale: EngineeringLocale) {
  if (locale === 'en') return { title:'TAG commissioning', help:'Transient Engineering read only. It does not Apply, activate, write the process or store Historian samples.', configureFirst:'Choose a Data Source and configure a canonical address/binding before testing.', byteSwap:'Byte Swap', wordSwap:'Word Swap', testRead:'Test read', testing:'Testing…', shortMonitor:'Monitor for a few seconds', monitoring:'Monitoring…', developmentMonitor:'Open Development Monitor', endpoint:'Sanitized endpoint', address:'Portable address', quality:'Quality', observed:'Observed', sourceTimestamp:'Source timestamp', latency:'Latency', raw:'Raw evidence', decoded:'Decoded raw value', engineering:'Engineering value', transform:'Effective transform', summary:'Sample summary', issues:'Issues', good:'GOOD', bad:'BAD', noData:'NO_DATA', uncertain:'INTERMITTENT_OR_UNCERTAIN', states:{ good:{icon:'✓',label:'GOOD — Valid read'}, bad:{icon:'!',label:'BAD — Communication/protocol error'}, noData:{icon:'—',label:'NO_DATA — No readable value'}, uncertain:{icon:'~',label:'INTERMITTENT_OR_UNCERTAIN — Partial/unstable read'} } } as const;
  if (locale === 'es') return { title:'Comisionamiento del TAG', help:'Lectura transitoria de Engineering. No aplica, activa, escribe en el proceso ni almacena muestras en Historian.', configureFirst:'Seleccione una Data Source y configure una dirección/binding canónica antes de probar.', byteSwap:'Byte Swap', wordSwap:'Word Swap', testRead:'Probar lectura', testing:'Probando…', shortMonitor:'Monitorear por algunos segundos', monitoring:'Monitoreando…', developmentMonitor:'Abrir Monitor de Desarrollo', endpoint:'Endpoint sanitizado', address:'Dirección portable', quality:'Calidad', observed:'Observado', sourceTimestamp:'Timestamp de origen', latency:'Latencia', raw:'Evidencia raw', decoded:'Valor raw decodificado', engineering:'Valor de Engineering', transform:'Transformación efectiva', summary:'Resumen de muestras', issues:'Problemas', good:'GOOD', bad:'BAD', noData:'NO_DATA', uncertain:'INTERMITTENT_OR_UNCERTAIN', states:{ good:{icon:'✓',label:'GOOD — Lectura válida'}, bad:{icon:'!',label:'BAD — Error de comunicación/protocolo'}, noData:{icon:'—',label:'NO_DATA — Sin valor legible'}, uncertain:{icon:'~',label:'INTERMITTENT_OR_UNCERTAIN — Lectura parcial/inestable'} } } as const;
  return { title:'Comissionamento do TAG', help:'Leitura transitória de Engenharia. Não aplica, ativa, escreve no processo nem grava amostras no Historian.', configureFirst:'Selecione uma Data Source e configure um endereço/binding canônico antes de testar.', byteSwap:'Byte Swap', wordSwap:'Word Swap', testRead:'Testar leitura', testing:'Testando…', shortMonitor:'Monitorar por alguns segundos', monitoring:'Monitorando…', developmentMonitor:'Abrir Monitoramento de Desenvolvimento', endpoint:'Endpoint sanitizado', address:'Endereço portátil', quality:'Qualidade', observed:'Observado', sourceTimestamp:'Timestamp de origem', latency:'Latência', raw:'Evidência raw', decoded:'Valor raw decodificado', engineering:'Valor de Engineering', transform:'Transformação efetiva', summary:'Resumo de amostras', issues:'Issues', good:'GOOD', bad:'BAD', noData:'NO_DATA', uncertain:'INTERMITTENT_OR_UNCERTAIN', states:{ good:{icon:'✓',label:'GOOD — Leitura válida'}, bad:{icon:'!',label:'BAD — Erro de comunicação/protocolo'}, noData:{icon:'—',label:'NO_DATA — Sem valor legível'}, uncertain:{icon:'~',label:'INTERMITTENT_OR_UNCERTAIN — Leitura parcial/instável'} } } as const;
}
