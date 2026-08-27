import React, { useEffect, useMemo, useState } from 'react';
import { AlarmEditor as BaseAlarmEditor } from './EngineeringMutationPanels';
import { EngineeringEntityBrowser, type EngineeringEntityBrowserMessages } from './EngineeringEntityBrowser';
import {
  alarmConditionSummary,
  alarmEngineeringKey,
  alarmSearchText,
  alarmTagReference,
  buildAlarmWorkspaceFilters
} from './AlarmWorkspace.logic';
import type { EngineeringLocale } from './i18n';
import type { AlarmEngineering, EngineeringPackageView } from './types';
import './engineering-alarm-workspace.css';

export function AlarmEditor({ model, locale }: { model: EngineeringPackageView; locale: EngineeringLocale }) {
  const alarms = model.alarms;
  const copy = useMemo(() => alarmWorkspaceCopy(locale), [locale]);
  const [selectedKey, setSelectedKey] = useState<string | null>(() => alarms[0] ? alarmEngineeringKey(alarms[0]) : null);

  useEffect(() => {
    if (selectedKey && alarms.some(alarm => alarmEngineeringKey(alarm) === selectedKey)) return;
    setSelectedKey(alarms[0] ? alarmEngineeringKey(alarms[0]) : null);
  }, [alarms, selectedKey]);

  const filters = useMemo(
    () => buildAlarmWorkspaceFilters(alarms, {
      enabled: copy.enabled,
      disabled: copy.disabled,
      requiresAck: copy.requiresAck,
      priority: copy.filterPriority,
      area: copy.filterArea,
      type: copy.filterType
    }),
    [alarms, copy]
  );

  return (
    <>
      <section className="eng-alarm-workspace" aria-label={copy.workspaceLabel}>
        <header className="eng-alarm-workspace__header">
          <div>
            <span>{copy.eyebrow}</span>
            <h2>{copy.title}</h2>
            <p>{copy.description}</p>
          </div>
          <div className="eng-alarm-workspace__count">
            <strong>{alarms.length}</strong>
            <span>{copy.configured}</span>
          </div>
        </header>

        <EngineeringEntityBrowser
          items={alarms}
          selectedKey={selectedKey}
          onSelectionChange={key => setSelectedKey(key)}
          getKey={alarmEngineeringKey}
          getLabel={alarm => alarm.name}
          getDescription={alarm => alarmTagReference(alarm)}
          getSearchText={alarmSearchText}
          renderItemMeta={alarm => (
            <span className="eng-alarm-workspace__item-meta">
              <span>{alarm.priority}</span>
              {alarm.enabled === false && <span>{copy.disabled}</span>}
            </span>
          )}
          renderDetail={alarm => <AlarmWorkspaceDetail alarm={alarm} copy={copy} />}
          filters={filters}
          messages={alarmBrowserMessages(copy)}
        />
      </section>

      <BaseAlarmEditor model={model} locale={locale} />
    </>
  );
}

function AlarmWorkspaceDetail({ alarm, copy }: { alarm: AlarmEngineering; copy: ReturnType<typeof alarmWorkspaceCopy> }) {
  const rows: Array<[string, React.ReactNode]> = [
    [copy.identity, alarm.id ? <code>{alarm.id}</code> : copy.notPersisted],
    [copy.tag, <code>{alarmTagReference(alarm)}</code>],
    [copy.type, alarm.type],
    [copy.priority, alarm.priority],
    [copy.area, alarm.area || '—'],
    [copy.alarmClass, alarm.alarmClass || '—'],
    [copy.condition, alarmConditionSummary(alarm, copy.notConfigured)],
    [copy.delay, formatDelay(alarm.activationDelayMilliseconds, copy)],
    [copy.acknowledgement, alarm.requiresAcknowledgement ? copy.required : copy.notRequired],
    [copy.shelving, alarm.shelvingAllowed ? copy.allowed : copy.notAllowed],
    [copy.status, alarm.enabled === false ? copy.disabled : copy.enabled]
  ];

  return (
    <div className="eng-alarm-workspace__detail">
      <div className="eng-alarm-workspace__detail-heading">
        <div>
          <span>{copy.detailEyebrow}</span>
          <h3>{alarm.name}</h3>
        </div>
        <div className="eng-alarm-workspace__badges" aria-label={copy.summaryLabel}>
          <span>{alarm.priority}</span>
          {alarm.area && <span>{alarm.area}</span>}
          <span>{alarm.enabled === false ? copy.disabled : copy.enabled}</span>
        </div>
      </div>

      <dl className="eng-alarm-workspace__detail-grid">
        {rows.map(([label, value]) => (
          <React.Fragment key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </React.Fragment>
        ))}
      </dl>

      <section className="eng-alarm-workspace__message">
        <span>{copy.message}</span>
        <p>{alarm.message || copy.noMessage}</p>
      </section>

      <p className="eng-alarm-workspace__authority-note">{copy.authorityNote}</p>
    </div>
  );
}

function formatDelay(value: number | null | undefined, copy: ReturnType<typeof alarmWorkspaceCopy>): string {
  if (value === null || value === undefined) return copy.notConfigured;
  if (value === 0) return copy.immediate;
  if (value < 1000) return `${value} ms`;
  return `${value / 1000} s`;
}

function alarmBrowserMessages(copy: ReturnType<typeof alarmWorkspaceCopy>): EngineeringEntityBrowserMessages {
  return {
    searchLabel: copy.search,
    searchPlaceholder: copy.searchPlaceholder,
    filterLabel: copy.filter,
    allFilterLabel: copy.all,
    listLabel: copy.listLabel,
    detailLabel: copy.detailLabel,
    loadingTitle: copy.loading,
    emptyTitle: copy.empty,
    emptyDescription: copy.emptyDescription,
    noMatchesTitle: copy.noMatches,
    noMatchesDescription: copy.noMatchesDescription,
    detailEmptyTitle: copy.select,
    detailEmptyDescription: copy.selectDescription,
    formatResultSummary: (visibleCount, totalCount) => `${visibleCount} ${copy.of} ${totalCount}`
  };
}

function alarmWorkspaceCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    workspaceLabel: 'Engineering Alarm workspace', eyebrow: 'Canonical Engineering', title: 'Alarm definitions',
    description: 'Search and inspect Alarm definitions without changing the protected Preview / Apply mutation workflow.',
    configured: 'configured', search: 'Search', searchPlaceholder: 'Search by name, TAG, area, class, type, priority or message',
    filter: 'Filter', all: 'All alarms', listLabel: 'Engineering Alarm definitions', detailLabel: 'Selected Alarm definition',
    loading: 'Loading alarms…', empty: 'No Alarm definitions configured',
    emptyDescription: 'The canonical Engineering model does not contain Alarm definitions.', noMatches: 'No matching alarms',
    noMatchesDescription: 'Change the search text or filter to inspect other Alarm definitions.', select: 'Select an Alarm',
    selectDescription: 'Choose an Alarm definition to inspect its canonical configuration.', of: 'of',
    enabled: 'Enabled', disabled: 'Disabled', requiresAck: 'Requires ACK',
    filterPriority: 'Severity', filterArea: 'Zone', filterType: 'Kind',
    priority: 'Priority', area: 'Area', type: 'Type', identity: 'Stable ID', tag: 'TAG reference',
    alarmClass: 'Alarm class', condition: 'Condition', delay: 'Activation delay', acknowledgement: 'Acknowledgement',
    shelving: 'Shelving', status: 'Status', message: 'Operator message', noMessage: 'No operator message configured.',
    required: 'Required', notRequired: 'Not required', allowed: 'Allowed', notAllowed: 'Not allowed',
    notConfigured: 'Not configured', notPersisted: 'No persisted stable ID', immediate: 'Immediate',
    detailEyebrow: 'Selected definition', summaryLabel: 'Alarm summary',
    authorityNote: 'This browser is read-only. Changes remain subject to the existing protected Engineering Preview / Apply / CAS workflow.'
  };

  if (locale === 'es') return {
    workspaceLabel: 'Workspace de Alarmas de Engineering', eyebrow: 'Engineering canónico', title: 'Definiciones de Alarmas',
    description: 'Busque e inspeccione definiciones de Alarmas sin cambiar el flujo protegido de mutación Preview / Apply.',
    configured: 'configuradas', search: 'Buscar', searchPlaceholder: 'Buscar por nombre, TAG, área, clase, tipo, prioridad o mensaje',
    filter: 'Filtro', all: 'Todas las alarmas', listLabel: 'Definiciones de Alarmas de Engineering', detailLabel: 'Definición de Alarma seleccionada',
    loading: 'Cargando alarmas…', empty: 'No hay definiciones de Alarmas configuradas',
    emptyDescription: 'El modelo canónico de Engineering no contiene definiciones de Alarmas.', noMatches: 'Sin alarmas coincidentes',
    noMatchesDescription: 'Cambie la búsqueda o el filtro para inspeccionar otras definiciones de Alarmas.', select: 'Seleccione una Alarma',
    selectDescription: 'Elija una definición de Alarma para inspeccionar su configuración canónica.', of: 'de',
    enabled: 'Habilitada', disabled: 'Deshabilitada', requiresAck: 'Requiere ACK',
    filterPriority: 'Nivel', filterArea: 'Zona', filterType: 'Categoría',
    priority: 'Prioridad', area: 'Área', type: 'Tipo', identity: 'ID estable', tag: 'Referencia TAG',
    alarmClass: 'Clase de Alarma', condition: 'Condición', delay: 'Retardo de activación', acknowledgement: 'Reconocimiento',
    shelving: 'Shelving', status: 'Estado', message: 'Mensaje al operador', noMessage: 'No hay mensaje al operador configurado.',
    required: 'Requerido', notRequired: 'No requerido', allowed: 'Permitido', notAllowed: 'No permitido',
    notConfigured: 'No configurado', notPersisted: 'Sin ID estable persistido', immediate: 'Inmediato',
    detailEyebrow: 'Definición seleccionada', summaryLabel: 'Resumen de Alarma',
    authorityNote: 'Este navegador es de solo lectura. Los cambios continúan sujetos al flujo protegido Preview / Apply / CAS de Engineering.'
  };

  return {
    workspaceLabel: 'Workspace de Alarmes do Engineering', eyebrow: 'Engineering canônico', title: 'Definições de Alarmes',
    description: 'Pesquise e inspecione definições de Alarmes sem alterar o fluxo protegido de mutação Preview / Apply.',
    configured: 'configurados', search: 'Pesquisar', searchPlaceholder: 'Pesquise por nome, TAG, área, classe, tipo, prioridade ou mensagem',
    filter: 'Filtro', all: 'Todos os alarmes', listLabel: 'Definições de Alarmes do Engineering', detailLabel: 'Definição de Alarme selecionada',
    loading: 'Carregando alarmes…', empty: 'Nenhuma definição de Alarme configurada',
    emptyDescription: 'O modelo canônico de Engineering não contém definições de Alarmes.', noMatches: 'Nenhum alarme correspondente',
    noMatchesDescription: 'Altere a pesquisa ou o filtro para inspecionar outras definições de Alarmes.', select: 'Selecione um Alarme',
    selectDescription: 'Escolha uma definição de Alarme para inspecionar sua configuração canônica.', of: 'de',
    enabled: 'Habilitado', disabled: 'Desabilitado', requiresAck: 'Exige ACK',
    filterPriority: 'Nível', filterArea: 'Setor', filterType: 'Condição',
    priority: 'Prioridade', area: 'Área', type: 'Tipo', identity: 'ID estável', tag: 'Referência TAG',
    alarmClass: 'Classe do Alarme', condition: 'Condição', delay: 'Atraso de ativação', acknowledgement: 'Reconhecimento',
    shelving: 'Shelving', status: 'Status', message: 'Mensagem ao operador', noMessage: 'Nenhuma mensagem ao operador configurada.',
    required: 'Obrigatório', notRequired: 'Não obrigatório', allowed: 'Permitido', notAllowed: 'Não permitido',
    notConfigured: 'Não configurado', notPersisted: 'Sem ID estável persistido', immediate: 'Imediato',
    detailEyebrow: 'Definição selecionada', summaryLabel: 'Resumo do Alarme',
    authorityNote: 'Este navegador é somente leitura. Alterações continuam sujeitas ao fluxo protegido Preview / Apply / CAS do Engineering.'
  };
}
