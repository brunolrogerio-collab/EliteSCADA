import type { EngineeringLocale } from './i18n';

const reasons: Record<string, readonly [string, string, string]> = {
  evidence_available: ['Há leituras numéricas utilizáveis', 'Usable numeric readings are available', 'Hay lecturas numéricas utilizables'],
  unsupported_non_numeric_tag_type: ['Este tipo de TAG não é consultado nesta análise numérica. Contagem zero aqui não significa ausência de histórico.', 'This TAG type is not queried by this numeric analysis. A zero count here does not mean there is no history.', 'Este tipo de TAG no se consulta en este análisis numérico. Un conteo cero aquí no significa que no haya histórico.'],
  missing_historical_observations: ['Não há leituras deste sinal no período consultado', 'No readings for this signal were found in the period', 'No hay lecturas de esta señal en el período consultado'],
  insufficient_no_good_finite_numeric_samples: ['As leituras não têm valores numéricos válidos com qualidade boa', 'Readings lack valid finite numeric values with good quality', 'Las lecturas no tienen valores numéricos válidos con calidad buena'],
  insufficient_no_synchronized_accepted_samples: ['Nenhuma leitura comprovou horário de origem sincronizado', 'No reading established a synchronized source time', 'Ninguna lectura acreditó hora de origen sincronizada'],
  insufficient_evidence: ['Faltam leituras utilizáveis para conferir esta condição', 'Usable readings are missing for this condition', 'Faltan lecturas utilizables para comprobar esta condición'],
  required_signal_unsupported_non_numeric_tag_type: ['Um sinal obrigatório não é numérico. Confira separadamente a contagem dos sinais numéricos do perfil.', 'A required signal is not numeric. Check the numeric profile signals separately.', 'Una señal obligatoria no es numérica. Revise por separado las señales numéricas del perfil.'],
  no_required_signals_configured: ['O perfil não define sinais obrigatórios', 'The profile has no required signals', 'El perfil no define señales obligatorias'],
  no_required_numeric_signals: ['O perfil não define sinais numéricos obrigatórios', 'The profile has no required numeric signals', 'El perfil no define señales numéricas obligatorias'],
  required_signal_has_no_synchronized_accepted_rows: ['Faltam leituras sincronizadas de um sinal obrigatório', 'A required signal has no accepted synchronized readings', 'Faltan lecturas sincronizadas de una señal obligatoria'],
  no_complete_required_synchronized_cohort: ['Os sinais obrigatórios não têm leituras utilizáveis no mesmo instante de origem', 'Required signals have no usable readings at the same source time', 'Las señales obligatorias no tienen lecturas utilizables en el mismo instante de origen'],
  single_required_numeric_signal_cannot_demonstrate_cross_signal_alignment: ['Há apenas um sinal numérico obrigatório; não é possível conferir alinhamento entre sinais', 'There is only one required numeric signal; alignment between signals cannot be checked', 'Solo hay una señal numérica obligatoria; no se puede comprobar alineación entre señales'],
  no_selected_numeric_signals: ['Não há sinais numéricos neste perfil', 'This profile has no numeric signals', 'No hay señales numéricas en este perfil'],
  selected_signal_has_no_synchronized_accepted_rows: ['Faltam leituras sincronizadas de um sinal numérico do perfil', 'A numeric profile signal has no accepted synchronized readings', 'Faltan lecturas sincronizadas de una señal numérica del perfil'],
  no_complete_selected_synchronized_cohort: ['Os sinais numéricos do perfil não têm leituras utilizáveis no mesmo instante de origem', 'Numeric profile signals have no usable readings at the same source time', 'Las señales numéricas del perfil no tienen lecturas utilizables en el mismo instante de origen'],
  single_selected_numeric_signal_cannot_demonstrate_cross_signal_alignment: ['Há apenas um sinal numérico no perfil; não é possível conferir alinhamento entre sinais', 'The profile has only one numeric signal; alignment between signals cannot be checked', 'Solo hay una señal numérica en el perfil; no se puede comprobar alineación entre señales'],
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
