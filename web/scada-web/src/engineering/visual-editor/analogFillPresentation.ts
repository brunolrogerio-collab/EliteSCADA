export const ANALOG_FILL_DIRECTIONS = [
  'bottom-to-top',
  'top-to-bottom',
  'left-to-right',
  'right-to-left'
] as const;

export type AnalogFillDirection = typeof ANALOG_FILL_DIRECTIONS[number];

export type AnalogFillPresentationInput = Readonly<{
  value: number;
  inputMinimum: number;
  inputMaximum: number;
  direction: AnalogFillDirection;
  clamp?: boolean;
}>;

export type AnalogFillPresentation = Readonly<{
  normalized: number;
  percent: number;
  clipPath: string;
}>;

/**
 * Pure renderer-side projection for canonical Analog Fill configuration.
 *
 * This helper deliberately owns no Engineering DTO, expression evaluation or
 * persistence semantics. FOLLOW-B Engineering/evaluator contracts provide the
 * already-resolved numeric input and canonical scale/direction; this function
 * only turns that resolved presentation state into deterministic clipping.
 */
export function computeAnalogFillPresentation(
  input: AnalogFillPresentationInput
): AnalogFillPresentation {
  requireFinite(input.value, 'Analog Fill value');
  requireFinite(input.inputMinimum, 'Analog Fill input minimum');
  requireFinite(input.inputMaximum, 'Analog Fill input maximum');

  if (input.inputMinimum === input.inputMaximum) {
    throw new Error('Analog Fill input minimum and maximum must be different.');
  }
  if (!ANALOG_FILL_DIRECTIONS.includes(input.direction)) {
    throw new Error(`Unsupported Analog Fill direction '${String(input.direction)}'.`);
  }

  const raw = (input.value - input.inputMinimum) / (input.inputMaximum - input.inputMinimum);
  if (!Number.isFinite(raw)) {
    throw new Error('Analog Fill normalization produced a non-finite result.');
  }

  const normalized = input.clamp === false ? raw : clamp01(raw);
  const percent = normalized * 100;

  return Object.freeze({
    normalized,
    percent,
    clipPath: clipPathFor(input.direction, normalized)
  });
}

function clipPathFor(direction: AnalogFillDirection, normalized: number): string {
  // CSS inset percentages are allowed outside 0..100 when canonical Engineering
  // deliberately disables clamping. The default path remains bounded.
  const remaining = (1 - normalized) * 100;
  switch (direction) {
    case 'bottom-to-top':
      return `inset(${formatPercent(remaining)} 0 0 0)`;
    case 'top-to-bottom':
      return `inset(0 0 ${formatPercent(remaining)} 0)`;
    case 'left-to-right':
      return `inset(0 ${formatPercent(remaining)} 0 0)`;
    case 'right-to-left':
      return `inset(0 0 0 ${formatPercent(remaining)})`;
  }
}

function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}

function formatPercent(value: number): string {
  const rounded = Math.abs(value) < 1e-12 ? 0 : Number(value.toFixed(6));
  return `${rounded}%`;
}

function requireFinite(value: number, label: string): void {
  if (!Number.isFinite(value)) throw new Error(`${label} must be finite.`);
}
