import type { EngineeringLocale } from '../i18n';

export const MAX_TREND_AXIS_LABEL_CHARACTERS = 7;
const MAX_FRACTION_DIGITS = 2;

/**
 * Keeps numeric trend ticks readable in a narrow axis column.
 * Thousands grouping is intentionally disabled so separators do not consume
 * the space needed for the value. If ordinary notation cannot fit, compact
 * scientific notation preserves the order of magnitude instead of clipping.
 */
export function formatTrendAxisLabel(value: number, locale: EngineeringLocale): string {
  if (!Number.isFinite(value)) return '—';
  if (value === 0) return '0';

  // Avoid displaying a non-zero measurement as zero when two decimal places
  // are insufficient to distinguish it.
  if (Math.abs(value) < 0.005) return formatScientific(value, locale);

  for (let fractionDigits = MAX_FRACTION_DIGITS; fractionDigits >= 0; fractionDigits--) {
    const formatted = new Intl.NumberFormat(locale, {
      useGrouping: false,
      maximumFractionDigits: fractionDigits
    }).format(value);
    if (formatted.length <= MAX_TREND_AXIS_LABEL_CHARACTERS) return formatted;
  }

  return formatScientific(value, locale);
}

function formatScientific(value: number, locale: EngineeringLocale): string {
  for (let fractionDigits = MAX_FRACTION_DIGITS; fractionDigits >= 0; fractionDigits--) {
    const formatted = new Intl.NumberFormat(locale, {
      notation: 'scientific',
      useGrouping: false,
      maximumFractionDigits: fractionDigits
    }).format(value);
    if (formatted.length <= MAX_TREND_AXIS_LABEL_CHARACTERS) return formatted;
  }

  // Finite IEEE-754 values in the supported locales fit at zero mantissa
  // decimals; keep a safe failure value if a future locale does not.
  return '—';
}
