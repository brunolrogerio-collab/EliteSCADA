import { useState } from 'react';
import type { EngineeringLocale } from './i18n';
import { parseWindow, type WindowValue } from './AssetProcessIntelligenceCoveragePanel';

export function useIntelligenceComparisonPeriod(reference: WindowValue | null) {
  const [enabled, setEnabled] = useState(false);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const parsed = parseWindow(from, to).window;
  const valid = !enabled || !!reference && !!parsed && Date.parse(parsed.fromUtc) > Date.parse(reference.toUtc);
  return { enabled, setEnabled, from, setFrom, to, setTo, valid,
    window: enabled && valid ? parsed : null, key: JSON.stringify([enabled, from, to]) };
}

export function matchesIntelligenceWindow(actual: unknown, expected: WindowValue | null): boolean {
  if (!expected) return actual === null || actual === undefined;
  if (!actual || typeof actual !== 'object') return false;
  const value = actual as Partial<WindowValue>;
  return Date.parse(value.fromUtc ?? '') === Date.parse(expected.fromUtc) && Date.parse(value.toUtc ?? '') === Date.parse(expected.toUtc);
}

export function IntelligenceComparisonPeriod({ locale, period }: {
  locale: EngineeringLocale; period: ReturnType<typeof useIntelligenceComparisonPeriod>;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  return <fieldset>
    <legend>{text('Comparação opcional', 'Optional comparison', 'Comparación opcional')}</legend>
    <label><input type="checkbox" checked={period.enabled} onChange={event => period.setEnabled(event.currentTarget.checked)}/>
      {text('Comparar com outro período', 'Compare with another period', 'Comparar con otro período')}</label>
    {period.enabled && <>
      <label>{text('Início da comparação (horário local)', 'Comparison start (local time)', 'Inicio de comparación (hora local)')}
        <input type="datetime-local" value={period.from} onChange={event => period.setFrom(event.currentTarget.value)}/></label>
      <label>{text('Fim da comparação (horário local)', 'Comparison end (local time)', 'Fin de comparación (hora local)')}
        <input type="datetime-local" value={period.to} onChange={event => period.setTo(event.currentTarget.value)}/></label>
      <p>{text('A comparação deve começar após o fim da referência, sem sobreposição. Use até 31 dias; os horários locais serão convertidos para UTC.',
        'Comparison must start after the reference ends, without overlap. Use up to 31 days; local times are converted to UTC.',
        'La comparación debe comenzar después de la referencia, sin superposición. Use hasta 31 días; las horas locales se convierten a UTC.')}</p>
      {!period.valid && <p role="alert">{text('Confira os limites e a ordem dos dois períodos.', 'Check the limits and order of both periods.', 'Revise los límites y el orden de ambos períodos.')}</p>}
    </>}
  </fieldset>;
}
