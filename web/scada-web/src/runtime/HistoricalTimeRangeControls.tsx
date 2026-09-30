import {
  HISTORICAL_TIME_RANGE_PRESETS,
  applyHistoricalPreset,
  historicalPresetSeconds,
  localTimeZoneLabel,
  validateHistoricalTimeRange,
  type HistoricalTimeRangeState,
  type HistoricalTimeRangeUnit
} from './historicalTimeRange';

export type HistoricalTimeRangeLocale = 'pt-BR' | 'en' | 'es';

type Copy = Readonly<{
  mode: string; live: string; relative: string; absolute: string; quickRange: string; amount: string; unit: string;
  seconds: string; minutes: string; hours: string; days: string; from: string; to: string; timezone: string; refresh: string;
  invalidAmount: string; tooLarge: string; required: string; order: string; ambiguous: string;
}>;

const COPY: Readonly<Record<HistoricalTimeRangeLocale, Copy>> = Object.freeze({
  'pt-BR': Object.freeze({
    mode: 'Período', live: 'Ao vivo', relative: 'Período relativo', absolute: 'Período absoluto',
    quickRange: 'Atalho', amount: 'Quantidade', unit: 'Unidade', seconds: 'segundos', minutes: 'minutos', hours: 'horas', days: 'dias',
    from: 'De', to: 'Até', timezone: 'Fuso horário', refresh: 'Atualizar',
    invalidAmount: 'A quantidade deve ser um número inteiro positivo.', tooLarge: 'O período não pode exceder 31 dias.',
    required: 'De e Até devem conter data e hora válidas.', order: 'De deve ser anterior a Até.',
    ambiguous: 'Este horário local é ambíguo por mudança de fuso/DST.'
  }),
  en: Object.freeze({
    mode: 'Period', live: 'Live', relative: 'Relative period', absolute: 'Absolute period',
    quickRange: 'Quick range', amount: 'Amount', unit: 'Unit', seconds: 'seconds', minutes: 'minutes', hours: 'hours', days: 'days',
    from: 'From', to: 'To', timezone: 'Time zone', refresh: 'Refresh',
    invalidAmount: 'Amount must be a positive whole number.', tooLarge: 'The period cannot exceed 31 days.',
    required: 'From and To must contain valid date/time values.', order: 'From must be before To.',
    ambiguous: 'This local time is ambiguous because of a time-zone/DST transition.'
  }),
  es: Object.freeze({
    mode: 'Período', live: 'En vivo', relative: 'Período relativo', absolute: 'Período absoluto',
    quickRange: 'Acceso rápido', amount: 'Cantidad', unit: 'Unidad', seconds: 'segundos', minutes: 'minutos', hours: 'horas', days: 'días',
    from: 'Desde', to: 'Hasta', timezone: 'Zona horaria', refresh: 'Actualizar',
    invalidAmount: 'La cantidad debe ser un número entero positivo.', tooLarge: 'El período no puede superar 31 días.',
    required: 'Desde y Hasta deben contener fecha y hora válidas.', order: 'Desde debe ser anterior a Hasta.',
    ambiguous: 'Esta hora local es ambigua por una transición de zona horaria/DST.'
  })
});

export function HistoricalTimeRangeControls({
  locale,
  value,
  onChange,
  onRefresh,
  disabled = false,
  compact = false
}: Readonly<{
  locale: HistoricalTimeRangeLocale;
  value: HistoricalTimeRangeState;
  onChange: (value: HistoricalTimeRangeState) => void;
  onRefresh?: () => void;
  disabled?: boolean;
  compact?: boolean;
}>) {
  const text = COPY[locale];
  const validation = validateHistoricalTimeRange(value);
  const preset = historicalPresetSeconds(value);
  const diagnostics = validation.issues.map(issue => {
    if (issue === 'relative-invalid') return text.invalidAmount;
    if (issue === 'range-too-large') return text.tooLarge;
    if (issue === 'absolute-required') return text.required;
    if (issue === 'absolute-order') return text.order;
    return text.ambiguous;
  });

  const containerStyle = compact ? {
    position: 'absolute' as const,
    right: 6,
    top: 6,
    zIndex: 20,
    display: 'flex',
    flexWrap: 'wrap' as const,
    alignItems: 'end',
    gap: 4,
    maxWidth: '94%',
    padding: 4,
    borderRadius: 4,
    background: '#020617E6',
    color: '#E2E8F0',
    fontSize: 9,
    pointerEvents: 'auto' as const
  } : undefined;

  const labelStyle = compact ? { display: 'grid', gap: 2, minWidth: 62 } : undefined;
  const inputStyle = compact ? { minWidth: 62, maxWidth: 138, fontSize: 10, padding: '2px 3px' } : undefined;

  function patch(next: Partial<HistoricalTimeRangeState>) {
    onChange(Object.freeze({ ...value, ...next }));
  }

  return <div
    data-runtime-session-control
    data-testid="historical-time-range-controls"
    style={containerStyle}
  >
    <label style={labelStyle}>
      <span>{text.mode}</span>
      <select
        aria-label={text.mode}
        value={value.mode}
        disabled={disabled}
        style={inputStyle}
        onChange={event => patch({ mode: event.target.value as HistoricalTimeRangeState['mode'] })}
      >
        <option value="live">{text.live}</option>
        <option value="relative">{text.relative}</option>
        <option value="absolute">{text.absolute}</option>
      </select>
    </label>

    {value.mode !== 'absolute' ? <>
      <label style={labelStyle}>
        <span>{text.quickRange}</span>
        <select
          aria-label={text.quickRange}
          value={preset ?? ''}
          disabled={disabled}
          style={inputStyle}
          onChange={event => {
            const seconds = Number(event.target.value);
            if (Number.isSafeInteger(seconds) && seconds > 0) onChange(applyHistoricalPreset(value, seconds));
          }}
        >
          <option value="">—</option>
          {HISTORICAL_TIME_RANGE_PRESETS.map(item => <option key={item.seconds} value={item.seconds}>{item.label}</option>)}
        </select>
      </label>
      <label style={labelStyle}>
        <span>{text.amount}</span>
        <input
          aria-label={text.amount}
          type="number"
          min={1}
          step={1}
          disabled={disabled}
          value={value.relativeAmount}
          style={inputStyle}
          onChange={event => patch({ relativeAmount: Number(event.target.value) })}
        />
      </label>
      <label style={labelStyle}>
        <span>{text.unit}</span>
        <select
          aria-label={text.unit}
          value={value.relativeUnit}
          disabled={disabled}
          style={inputStyle}
          onChange={event => patch({ relativeUnit: event.target.value as HistoricalTimeRangeUnit })}
        >
          <option value="seconds">{text.seconds}</option>
          <option value="minutes">{text.minutes}</option>
          <option value="hours">{text.hours}</option>
          <option value="days">{text.days}</option>
        </select>
      </label>
    </> : <>
      <label style={labelStyle}>
        <span>{text.from}</span>
        <input
          aria-label={text.from}
          type="datetime-local"
          step={1}
          disabled={disabled}
          value={value.absoluteFromLocal}
          style={inputStyle}
          onChange={event => patch({ absoluteFromLocal: event.target.value })}
        />
      </label>
      <label style={labelStyle}>
        <span>{text.to}</span>
        <input
          aria-label={text.to}
          type="datetime-local"
          step={1}
          disabled={disabled}
          value={value.absoluteToLocal}
          style={inputStyle}
          onChange={event => patch({ absoluteToLocal: event.target.value })}
        />
      </label>
    </>}

    <span title={text.timezone} style={compact ? { alignSelf: 'center', maxWidth: 110, overflow: 'hidden', textOverflow: 'ellipsis' } : undefined}>
      {localTimeZoneLabel()}
    </span>
    {onRefresh ? <button type="button" disabled={disabled || !validation.ok} onClick={onRefresh}>{text.refresh}</button> : null}
    {!validation.ok ? <span role="alert" style={compact ? { flexBasis: '100%', color: '#FCA5A5' } : undefined}>{diagnostics.join(' ')}</span> : null}
  </div>;
}
