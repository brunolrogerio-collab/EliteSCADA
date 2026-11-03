import './intelligence-signal-statistics.css';
import { useEffect, useRef, useState } from 'react';
import { parseWindow, type SignalSummary, type WindowValue } from './AssetProcessIntelligenceCoveragePanel';
import { matchesIntelligenceWindow } from './IntelligenceComparisonPeriod';
import type { EngineeringLocale } from './i18n';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type Comparison = { period: { window: WindowValue; summary: SignalSummary }; validSampleCountDelta: number | null;
  minimumDelta: number | null; maximumDelta: number | null; sampleMeanDelta: number | null;
  firstObservedDelta: number | null; lastObservedDelta: number | null; deltaDelta: number | null; regimeEquivalence: string };
export function IntelligenceSignalStatistics({ locale, scopeId, profileId, tagId, projectKey, activeRevision, window, summary, unit }: {
  locale: EngineeringLocale; scopeId: string; profileId: string; tagId: string; projectKey: string; activeRevision: number;
  window: WindowValue; summary: SignalSummary; unit: string | null;
}) {
  const t = (pt: string,en: string,es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const n = (value: number | null | undefined) => typeof value === 'number' && Number.isFinite(value) ? value.toLocaleString(locale,{maximumFractionDigits:6}) : t('Não disponível','Unavailable','No disponible');
  const time = (value: string) => Number.isFinite(Date.parse(value)) ? new Date(value).toLocaleString(locale) : t('Horário inválido','Invalid time','Hora no válida');
  const [showBuckets,setShowBuckets]=useState(false);
  const [from,setFrom]=useState(''),[to,setTo]=useState(''),[bucket,setBucket]=useState(900),[result,setResult]=useState<Comparison|null>(null),[error,setError]=useState(false),[busy,setBusy]=useState(false);
  const abort=useRef<AbortController|null>(null),sequence=useRef(0);
  const comparison=parseWindow(from,to).window;
  const valid=!!comparison && Date.parse(comparison.toUtc)-Date.parse(comparison.fromUtc)===Date.parse(window.toUtc)-Date.parse(window.fromUtc);
  useEffect(()=>{abort.current?.abort();sequence.current++;setResult(null);setError(false);setBusy(false);return()=>{abort.current?.abort();sequence.current++;};},[from,to,bucket,scopeId,profileId,tagId,projectKey,activeRevision,window.fromUtc,window.toUtc]);
  async function query(){
    if(!valid||!comparison)return;
    abort.current?.abort();const controller=new AbortController();abort.current=controller;const request=++sequence.current;setBusy(true);setError(false);setResult(null);
    try{
      const response=await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/query`,{method:'POST',credentials:'include',headers:{'content-type':'application/json'},signal:controller.signal,
        body:JSON.stringify({scopeKind:0,scopeId,tagId,window,comparisonWindow:comparison,bucketSeconds:bucket})});
      const data=await response.json();const c=data?.comparison as Comparison|undefined;
      if(!response.ok||data.contractVersion!==1||data.catalogVersion!==1||data.ruleVersion!=='1.0.0'||data.projectKey!==projectKey||data.activeRevision!==activeRevision||
        ![0,'Equipment'].includes(data.scopeKind)||data.unit!==unit||data.provenance?.dataset!=='historian.samples'||data.provenance?.datasetContractVersion!==1||data.provenance?.regimeEquivalence!=='not_assessed'||data.scopeId?.toLowerCase()!==scopeId.toLowerCase()||data.tagId?.toLowerCase()!==tagId.toLowerCase()||!matchesIntelligenceWindow(data.period?.window,window)||
        !c||!matchesIntelligenceWindow(c.period?.window,comparison)||c.regimeEquivalence!=='not_assessed'||
        ![c.validSampleCountDelta,c.minimumDelta,c.maximumDelta,c.sampleMeanDelta,c.firstObservedDelta,c.lastObservedDelta,c.deltaDelta].every(value=>value===null||typeof value==='number'&&Number.isFinite(value))||
        !data.provenance?.assignments?.some((item:{profileId:string})=>item.profileId?.toLowerCase()===profileId.toLowerCase()))throw Error('Invalid comparison');
      if(!controller.signal.aborted&&sequence.current===request)setResult(c);
    }catch{if(!controller.signal.aborted&&sequence.current===request)setError(true);}finally{if(sequence.current===request)setBusy(false);}
  }
  return <details className="intelligence-signal-statistics" data-testid={`intelligence-signal-statistics-${tagId}`}>
    <summary>{t('Detalhes e comparação desta medição','Measurement details and comparison','Detalles y comparación de esta medición')}</summary>
    <p>{t('Os horários abaixo são os registros de observação do Historian. Primeiro e último significam primeira e última leitura Boa aceita; não garantem o instante de aquisição do sensor.','Times below are Historian observation records. First and last mean the first and last accepted Good readings; they do not establish sensor acquisition time.','Las horas siguientes son registros de observación del Historian. Primero y último son las primeras y últimas lecturas Buenas aceptadas; no garantizan la hora de adquisición del sensor.')}</p>
    <dl><dt>{t('Primeiro valor observado','First observed value','Primer valor observado')}</dt><dd>{n(summary.firstObserved?.value)}{summary.firstObserved ? ` ${unit??''} · ${time(summary.firstObserved.timestampUtc)}`:''}</dd>
      <dt>{t('Último valor observado','Last observed value','Último valor observado')}</dt><dd>{n(summary.lastObserved?.value)}{summary.lastObserved ? ` ${unit??''} · ${time(summary.lastObserved.timestampUtc)}`:''}</dd>
      <dt>{t('Variação no período (último menos primeiro)','Period change (last minus first)','Variación del período (último menos primero)')}</dt><dd>{n(summary.delta)} {unit??''}</dd></dl>
    <details onToggle={event=>setShowBuckets(event.currentTarget.open)}><summary>{t('Leituras por intervalo','Readings by interval','Lecturas por intervalo')}</summary>
      <p>{t('Intervalos vazios permanecem sem valor. A média é de amostras, não ponderada pelo tempo.','Empty intervals retain unavailable values. The mean is a sample mean, not time-weighted.','Los intervalos vacíos mantienen valores no disponibles. La media es de muestras, no ponderada por tiempo.')}</p>
      {showBuckets&&<div style={{maxWidth:'100%',overflowX:'auto'}}><table><thead><tr>{[t('Início','Start','Inicio'),t('Fim','End','Fin'),t('Aceitas / rejeitadas','Accepted / rejected','Aceptadas / rechazadas'),t('Mínimo','Minimum','Mínimo'),t('Máximo','Maximum','Máximo'),t('Média','Mean','Media')].map(label=><th key={label}>{label}</th>)}</tr></thead>
        <tbody>{(Array.isArray(summary.trend)?summary.trend:[]).filter(row=>row&&typeof row==='object').slice(0,3000).map((row,index)=><tr key={index}><td>{time(row.fromUtc)}</td><td>{time(row.toUtc)}</td><td>{n(row.acceptedSampleCount)} / {n(row.rejectedSampleCount)}</td><td>{n(row.minimum)}</td><td>{n(row.maximum)}</td><td>{n(row.sampleMean)}</td></tr>)}</tbody></table></div>}
    </details>
    <p>{t('Para comparar, escolha outro período de mesma duração (até 31 dias). Pode vir antes, depois ou se sobrepor ao atual. Sobreposição reutiliza dados e não representa uma comparação independente.','To compare, choose another equal-duration period (up to 31 days). It may precede, follow or overlap the current period. Overlap reuses data and is not an independent comparison.','Para comparar elija otro período de igual duración (hasta 31 días). Puede ser anterior, posterior o superponerse al actual. La superposición reutiliza datos y no representa comparación independiente.')}</p>
    <label>{t('Início da comparação estatística','Statistical comparison start','Inicio de comparación estadística')}<input type="datetime-local" value={from} onChange={event=>setFrom(event.currentTarget.value)}/></label>
    <label>{t('Fim da comparação estatística','Statistical comparison end','Fin de comparación estadística')}<input type="datetime-local" value={to} onChange={event=>setTo(event.currentTarget.value)}/></label>
    <label>{t('Agrupamento da comparação','Comparison grouping','Agrupación de comparación')}<select value={bucket} onChange={event=>setBucket(Number(event.currentTarget.value))}>{[900,1800,3600,21600,86400].map(value=><option key={value} value={value}>{value<3600?`${value/60} min`:`${value/3600} h`}</option>)}</select></label>
    <button type="button" disabled={!valid||busy} onClick={()=>void query()}>{t('Comparar estatísticas','Compare statistics','Comparar estadísticas')}</button>
    {busy&&<button type="button" onClick={()=>{abort.current?.abort();sequence.current++;setBusy(false);}}>{t('Cancelar comparação','Cancel comparison','Cancelar comparación')}</button>}
    {error&&<p role="alert">{t('Não foi possível obter uma comparação válida. Confira o acesso, a revisão ativa e os períodos antes de tentar novamente.','A valid comparison could not be obtained. Check access, active revision and periods before trying again.','No se pudo obtener una comparación válida. Revise acceso, revisión activa y períodos antes de intentarlo de nuevo.')}</p>}
    {result&&<div data-testid="intelligence-statistical-comparison-result"><p>{t('Período comparado: mínimo / máximo / média','Comparison period: minimum / maximum / mean','Período comparado: mínimo / máximo / media')}: {n(result.period.summary?.minimum)} / {n(result.period.summary?.maximum)} / {n(result.period.summary?.sampleMean)} {unit??''}</p><h5>{t('Diferenças: período atual menos comparação','Differences: current period minus comparison','Diferencias: período actual menos comparación')}</h5><dl>
      {[[t('Quantidade de amostras','Sample count','Cantidad de muestras'),result.validSampleCountDelta],[t('Mínimo','Minimum','Mínimo'),result.minimumDelta],[t('Máximo','Maximum','Máximo'),result.maximumDelta],[t('Média','Mean','Media'),result.sampleMeanDelta],[t('Primeiro valor','First value','Primer valor'),result.firstObservedDelta],[t('Último valor','Last value','Último valor'),result.lastObservedDelta],[t('Variação no período','Period change','Variación del período'),result.deltaDelta]].map(([label,value])=><div key={String(label)}><dt>{label}</dt><dd>{n(value as number|null)}</dd></div>)}</dl>
      <p>{t('Não se comprovou equivalência das condições de operação. Diferenças descritivas não comprovam eficiência ou defeito.','Equivalent operating conditions have not been established. Descriptive differences do not establish efficiency or a fault.','No se ha comprobado equivalencia de condiciones de operación. Las diferencias descriptivas no demuestran eficiencia ni un fallo.')}</p></div>}
  </details>;
}
