import type { EngineeringLocale } from './i18n';

const reasons: Record<string, readonly [string, string, string]> = {
  poor_or_missing_quality: ['Qualidade ruim ou não informada', 'Poor or missing quality', 'Calidad mala o no informada'],
  source_timestamp_missing: ['Horário de origem ausente', 'Missing source time', 'Hora de origen ausente'],
  source_timestamp_not_utc: ['Horário de origem sem UTC válido', 'Invalid UTC source time', 'Hora UTC de origen no válida'],
  source_timestamp_not_synchronized: ['Sincronização da origem não comprovada', 'Source synchronization not established', 'Sincronización de origen no comprobada'],
  source_timestamp_outside_window: ['Horário de origem fora do período', 'Source time outside the period', 'Hora de origen fuera del período'],
  source_timestamp_outside_requested_window: ['Horário de origem fora do período', 'Source time outside the period', 'Hora de origen fuera del período'],
  duplicate_source_timestamp: ['Leituras ambíguas no mesmo horário de origem', 'Ambiguous readings at the same source time', 'Lecturas ambiguas con la misma hora de origen'],
  value_missing: ['Valor ausente', 'Missing value', 'Valor ausente'],
  value_type_mismatch_or_non_finite: ['Valor incompatível ou não finito', 'Incompatible or nonfinite value', 'Valor incompatible o no finito'],
  context_match_available: ['Há leituras que atendem a todas as condições', 'Readings match all conditions', 'Hay lecturas que cumplen todas las condiciones'],
  insufficient_target_evidence: ['Faltam leituras utilizáveis desta medição', 'Not enough usable readings for this measurement', 'Faltan lecturas utilizables de esta medición'],
  insufficient_predicate_evidence: ['Faltam dados para conferir uma ou mais condições', 'Data is missing for one or more conditions', 'Faltan datos para comprobar una o más condiciones'],
  predicate_mismatch: ['As condições selecionadas não foram atendidas', 'Selected conditions were not met', 'No se cumplieron las condiciones seleccionadas'],
  no_exact_context_timestamp_match: ['Medição e condições não têm leituras no mesmo instante de origem', 'Measurement and conditions have no readings at the same source time', 'La medición y las condiciones no tienen lecturas en el mismo instante de origen'],
  target_type_not_applicable: ['Esta análise exige uma medição numérica', 'This analysis requires a numeric measurement', 'Este análisis requiere una medición numérica']
};
export function intelligenceEvidenceText(reason: string, locale: EngineeringLocale): string {
  const labels = reasons[reason] ?? ['Evidência indisponível para esta condição', 'Evidence unavailable for this condition', 'Evidencia no disponible para esta condición'];
  return labels[locale === 'pt-BR' ? 0 : locale === 'es' ? 2 : 1];
}
