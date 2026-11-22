import type { EngineeringLocale } from '../i18n';
import {
  canAcknowledgeRuntimeAlarm,
  normalizeRuntimeAlarmPriority,
  normalizeRuntimeAlarmState,
  type RuntimeAlarmPriorityKey,
  type RuntimeAlarmStateKey
} from '../../runtime/alarmCenterModel';
import type { RuntimeAlarmCenterItem } from '../../runtime/alarmCenterTypes';
import { formatHistoricalDateTime } from '../../runtime/historical-browser/historicalBrowserPresentation';

const stateLabels: Readonly<Record<EngineeringLocale, Readonly<Record<RuntimeAlarmStateKey, string>>>> = Object.freeze({
  'pt-BR': Object.freeze({ normal: 'Normal', active: 'Ativo', acknowledged: 'Reconhecido', returned: 'Retornado', disabled: 'Desabilitado', shelved: 'Suprimido', unknown: 'Desconhecido' }),
  en: Object.freeze({ normal: 'Normal', active: 'Active', acknowledged: 'Acknowledged', returned: 'Returned', disabled: 'Disabled', shelved: 'Shelved', unknown: 'Unknown' }),
  es: Object.freeze({ normal: 'Normal', active: 'Activa', acknowledged: 'Reconocida', returned: 'Retornada', disabled: 'Deshabilitada', shelved: 'Suprimida', unknown: 'Desconocida' })
});

const priorityLabels: Readonly<Record<EngineeringLocale, Readonly<Record<RuntimeAlarmPriorityKey, string>>>> = Object.freeze({
  'pt-BR': Object.freeze({ low: 'Baixa', medium: 'Média', high: 'Alta', critical: 'Crítica', unknown: 'Desconhecida' }),
  en: Object.freeze({ low: 'Low', medium: 'Medium', high: 'High', critical: 'Critical', unknown: 'Unknown' }),
  es: Object.freeze({ low: 'Baja', medium: 'Media', high: 'Alta', critical: 'Crítica', unknown: 'Desconocida' })
});

const missingTagPath: Readonly<Record<EngineeringLocale, string>> = Object.freeze({
  'pt-BR': 'Caminho TAG indisponível',
  en: 'TAG path unavailable',
  es: 'Ruta TAG no disponible'
});

export type BrowserAlarmPresentation = Readonly<{
  timestamp: string;
  state: string;
  priority: string;
  tagPath: string;
  canAcknowledge: boolean;
}>;

export function presentCurrentBrowserAlarm(
  alarm: RuntimeAlarmCenterItem,
  tagPath: string,
  locale: EngineeringLocale
): BrowserAlarmPresentation {
  const state = normalizeRuntimeAlarmState(alarm.state);
  const priority = normalizeRuntimeAlarmPriority(alarm.priority);
  return Object.freeze({
    timestamp: formatHistoricalDateTime(alarm.lastTransition, locale) || alarm.lastTransition,
    state: stateLabels[locale][state],
    priority: priorityLabels[locale][priority],
    tagPath: tagPath || missingTagPath[locale],
    canAcknowledge: canAcknowledgeRuntimeAlarm(alarm)
  });
}

export function isVisibleInActiveAlarmBrowser(alarm: RuntimeAlarmCenterItem): boolean {
  const state = normalizeRuntimeAlarmState(alarm.state);
  return state === 'active' || state === 'acknowledged' || state === 'returned' && canAcknowledgeRuntimeAlarm(alarm);
}
