import { useEffect, useRef, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import type { WindowValue } from './AssetProcessIntelligenceCoveragePanel';
import { matchesIntelligenceWindow } from './IntelligenceComparisonPeriod';

const API=(import.meta.env?.VITE_SCADA_API??'').replace(/\/$/,'');
type Endpoint={timestampUtc:string;value:number};
type Metric={status:string;reasonCodes:string[];value:number|null;unit:string|null;elapsedSeconds:number|null;
  rawObservationCount:number;acceptedSampleCount:number;rejectedSampleCount:number;qualityCounts:Record<string,number>;
  firstAccepted:Endpoint|null;lastAccepted:Endpoint|null};
const finite=(value:unknown)=>typeof value==='number'&&Number.isFinite(value);
const count=(value:unknown)=>Number.isInteger(value)&&Number(value)>=0&&Number(value)<=50000;
function validEndpoint(value:Endpoint|null,window:WindowValue){return value===null||!!value&&finite(value.value)&&typeof value.timestampUtc==='string'&&
  Date.parse(value.timestampUtc)>=Date.parse(window.fromUtc)&&Date.parse(value.timestampUtc)<=Date.parse(window.toUtc);}

export function IntelligenceNetObservedRate({locale,scopeId,tagId,projectKey,activeRevision,window,unit}:{
  locale:EngineeringLocale;scopeId:string;tagId:string;projectKey:string;activeRevision:number;window:WindowValue;unit:string|null;
}){
  const t=(pt:string,en:string,es:string)=>locale==='pt-BR'?pt:locale==='es'?es:en;
  const n=(value:number|null)=>value===null?t('Não disponível','Unavailable','No disponible'):value.toLocaleString(locale,{maximumFractionDigits:6});
  const [result,setResult]=useState<Metric|null>(null),[error,setError]=useState(false),[busy,setBusy]=useState(false);
  const controller=useRef<AbortController|null>(null),sequence=useRef(0);
  useEffect(()=>{controller.current?.abort();sequence.current++;setResult(null);setError(false);setBusy(false);return()=>{controller.current?.abort();sequence.current++;};},[scopeId,tagId,projectKey,activeRevision,window.fromUtc,window.toUtc,unit]);
  async function query(){
    controller.current?.abort();const abort=new AbortController();controller.current=abort;const request=++sequence.current;setResult(null);setError(false);setBusy(true);
    try{
      const response=await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/net-observed-rate/v1/query`,{method:'POST',credentials:'include',headers:{'content-type':'application/json'},signal:abort.signal,body:JSON.stringify({scopeKind:0,scopeId,tagId,window})});
      const data=await response.json(),metric=data?.metric as Metric|undefined;
      if(!response.ok||data.contractVersion!==1||data.catalogVersion!==1||data.ruleId!=='net-observed-rate.v1'||data.ruleVersion!=='1.0.0'||
        data.projectKey!==projectKey||data.activeRevision!==activeRevision||![0,'Equipment'].includes(data.scopeKind)||data.scopeId?.toLowerCase()!==scopeId.toLowerCase()||data.tagId?.toLowerCase()!==tagId.toLowerCase()||
        !matchesIntelligenceWindow(data.window,window)||data.provenance?.dataset!=='historian.samples'||data.provenance?.datasetContractVersion!==1||
        !metric||!['available','insufficient_data','unsupported'].includes(metric.status)||
        !Array.isArray(metric.reasonCodes)||metric.reasonCodes.length>8||!metric.reasonCodes.every(reason=>typeof reason==='string')||
        ![metric.rawObservationCount,metric.acceptedSampleCount,metric.rejectedSampleCount].every(count)||metric.acceptedSampleCount+metric.rejectedSampleCount!==metric.rawObservationCount||
        !(metric.value===null||finite(metric.value))||!(metric.elapsedSeconds===null||finite(metric.elapsedSeconds))||
        metric.unit!==(unit?.trim()?`${unit}/s`:null)||!validEndpoint(metric.firstAccepted,window)||!validEndpoint(metric.lastAccepted,window)||
        !metric.qualityCounts||typeof metric.qualityCounts!=='object'||Array.isArray(metric.qualityCounts)||Object.keys(metric.qualityCounts).length>32||!Object.values(metric.qualityCounts).every(count)||
        (metric.status==='available'?(metric.value===null||metric.elapsedSeconds===null||metric.elapsedSeconds<=0||metric.acceptedSampleCount<2||!metric.firstAccepted||!metric.lastAccepted||metric.unit===null):metric.value!==null))throw Error('Invalid rate response');
      if(!abort.signal.aborted&&sequence.current===request)setResult(metric);
    }catch{if(!abort.signal.aborted&&sequence.current===request)setError(true);}finally{if(sequence.current===request)setBusy(false);}
  }
  const reason=(code:string)=>({
    fewer_than_two_accepted_good_finite_samples:t('Há menos de duas leituras Boas numéricas utilizáveis.','There are fewer than two usable Good numeric readings.','Hay menos de dos lecturas Buenas numéricas utilizables.'),
    non_positive_endpoint_elapsed_seconds:t('Os extremos não têm um intervalo de tempo positivo.','The endpoints have no positive elapsed time.','Los extremos no tienen un intervalo de tiempo positivo.'),
    engineering_unit_missing:t('O TAG não tem unidade informada. A taxa fica indisponível; não invente uma unidade para habilitá-la.','The TAG has no unit. The rate remains unavailable; do not invent a unit to enable it.','El TAG no tiene unidad. La tasa queda no disponible; no invente una unidad para habilitarla.'),
    non_finite_calculated_rate:t('O cálculo excedeu o limite numérico.','The calculation exceeded the numeric limit.','El cálculo superó el límite numérico.')
  }[code]??t('Não há evidência utilizável para esta condição.','No usable evidence is available for this condition.','No hay evidencia utilizable para esta condición.'));
  const qualityLabel=(quality:string)=>({
    Good:t('Boa','Good','Buena'),Uncertain:t('Incerta','Uncertain','Incierta'),Bad:t('Ruim','Bad','Mala'),
    BadCommunication:t('Falha de comunicação','Communication failure','Fallo de comunicación'),
    BadConfiguration:t('Configuração inválida','Invalid configuration','Configuración no válida'),
    BadDevice:t('Falha do equipamento','Device failure','Fallo del dispositivo'),
    Stale:t('Desatualizada','Stale','Desactualizada'),Disabled:t('Desabilitada','Disabled','Deshabilitada'),
    Unavailable:t('Indisponível','Unavailable','No disponible'),Missing:t('Ausente','Missing','Ausente')
  }[quality]??t('Qualidade não reconhecida','Unrecognized quality','Calidad no reconocida'));
  const endpoint=(value:Endpoint|null)=>value?`${n(value.value)} ${unit??''} · ${new Date(value.timestampUtc).toLocaleString(locale)}`:n(null);
  return <details data-testid={`intelligence-net-observed-rate-${tagId}`}>
    <summary>{t('Taxa líquida observada','Net observed rate','Tasa neta observada')}</summary>
    <p>{t('No período da cobertura, esta consulta usa último menos primeiro valor aceito e divide pelos segundos entre os dois registros no Historian. Não é derivada instantânea nem média de todas as taxas. A unidade é a do TAG por segundo, sem conversão automática.','For the coverage period, this query uses last minus first accepted value divided by seconds between their Historian records. It is neither an instantaneous derivative nor a mean of all rates. The unit is the TAG unit per second, without automatic conversion.','En el período de cobertura, esta consulta usa último menos primer valor aceptado y divide por los segundos entre sus registros en el Historian. No es derivada instantánea ni media de todas las tasas. La unidad es la del TAG por segundo, sin conversión automática.')}</p>
    <button type="button" disabled={busy} onClick={()=>void query()}>{t('Consultar taxa líquida','Query net rate','Consultar tasa neta')}</button>
    {busy&&<button type="button" onClick={()=>{controller.current?.abort();sequence.current++;setBusy(false);}}>{t('Cancelar consulta da taxa','Cancel rate query','Cancelar consulta de tasa')}</button>}
    {error&&<p role="alert">{t('Não foi possível obter uma taxa válida. Confira acesso, revisão e disponibilidade dos dados antes de tentar novamente.','A valid rate could not be obtained. Check access, revision and data availability before trying again.','No se pudo obtener una tasa válida. Revise acceso, revisión y disponibilidad de datos antes de intentarlo de nuevo.')}</p>}
    {result&&<div data-testid="intelligence-net-observed-rate-result"><dl>
      <dt>{t('Taxa no período','Rate over the period','Tasa en el período')}</dt><dd>{n(result.value)}{result.value!==null?` ${result.unit}`:''}</dd>
      <dt>{t('Primeira leitura aceita','First accepted reading','Primera lectura aceptada')}</dt><dd>{endpoint(result.firstAccepted)}</dd>
      <dt>{t('Última leitura aceita','Last accepted reading','Última lectura aceptada')}</dt><dd>{endpoint(result.lastAccepted)}</dd>
      <dt>{t('Segundos entre os extremos','Seconds between endpoints','Segundos entre extremos')}</dt><dd>{n(result.elapsedSeconds)}</dd>
      <dt>{t('Recebidas / aceitas / rejeitadas','Received / accepted / rejected','Recibidas / aceptadas / rechazadas')}</dt><dd>{result.rawObservationCount} / {result.acceptedSampleCount} / {result.rejectedSampleCount}</dd></dl>
      {result.reasonCodes.map(code=><p key={code}>{reason(code)}</p>)}
      <details><summary>{t('Qualidade das leituras','Reading quality','Calidad de lecturas')}</summary>{Object.entries(result.qualityCounts).filter(([,total])=>total>0).map(([quality,total])=><p key={quality}>{qualityLabel(quality)}: {total}</p>)}</details>
      <p>{t('Uma taxa positiva ou negativa descreve apenas os extremos registrados. Não comprova perda de eficiência, defeito, completude dos dados ou condições equivalentes de operação.','A positive or negative rate describes only the recorded endpoints. It does not establish efficiency loss, a fault, data completeness or equivalent operating conditions.','Una tasa positiva o negativa describe solo los extremos registrados. No demuestra pérdida de eficiencia, fallo, integridad de datos ni condiciones equivalentes de operación.')}</p>
    </div>}
  </details>;
}
