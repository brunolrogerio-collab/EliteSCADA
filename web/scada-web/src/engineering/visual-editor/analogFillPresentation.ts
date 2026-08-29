export const ANALOG_FILL_DIRECTIONS = [
  'BottomToTop',
  'TopToBottom',
  'LeftToRight',
  'RightToLeft'
] as const;

export type AnalogFillDirection = typeof ANALOG_FILL_DIRECTIONS[number];

export type AnalogFillPresentationInput = Readonly<{
  value: number;
  inputMinimum: number;
  inputMaximum: number;
  direction: AnalogFillDirection;
  clamp?: boolean;
  invertScale?: boolean;
}>;

export type AnalogFillPresentation = Readonly<{
  normalized: number;
  percent: number;
  clipPath: string;
}>;

/**
 * Pure renderer-side projection for the canonical FOLLOW-B Analog Fill contract.
 * Engineering/evaluator code supplies an already-resolved numeric value plus the
 * persisted scale/direction flags; this helper owns presentation math only.
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

  const scaled = input.invertScale === true ? 1 - raw : raw;
  const normalized = input.clamp === false ? scaled : clamp01(scaled);
  const percent = normalized * 100;

  return Object.freeze({
    normalized,
    percent,
    clipPath: clipPathFor(input.direction, normalized)
  });
}

function clipPathFor(direction: AnalogFillDirection, normalized: number): string {
  const remaining = (1 - normalized) * 100;
  switch (direction) {
    case 'BottomToTop':
      return `inset(${formatPercent(remaining)} 0 0 0)`;
    case 'TopToBottom':
      return `inset(0 0 ${formatPercent(remaining)} 0)`;
    case 'LeftToRight':
      return `inset(0 ${formatPercent(remaining)} 0 0)`;
    case 'RightToLeft':
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
