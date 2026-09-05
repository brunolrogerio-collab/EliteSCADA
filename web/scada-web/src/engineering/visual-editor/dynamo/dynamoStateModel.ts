export type DynamoQualityState = 'good' | 'uncertain' | 'bad' | 'stale' | 'unknown';

export type DynamoCommandIntent =
  | 'start'
  | 'stop'
  | 'open'
  | 'close'
  | 'increase'
  | 'decrease'
  | 'setpoint'
  | null;

export type DynamoSettledState = 'active' | 'inactive' | 'transitioning' | 'unknown';

export type DynamoStateInputs = Readonly<{
  quality?: DynamoQualityState;
  fault?: boolean;
  alarm?: boolean;
  commandIntent?: DynamoCommandIntent;
  settledState?: DynamoSettledState;
}>;

export type DynamoResolvedVisualStateKind =
  | 'bad-quality'
  | 'fault'
  | 'alarm'
  | 'uncertain-quality'
  | 'command-intent'
  | 'transitioning'
  | 'active'
  | 'inactive'
  | 'unknown';

export type DynamoResolvedVisualState = Readonly<{
  kind: DynamoResolvedVisualStateKind;
  priority: number;
  quality: DynamoQualityState;
  commandIntent: DynamoCommandIntent;
  settledState: DynamoSettledState;
}>;

export const DYNAMO_STATE_PRECEDENCE = Object.freeze({
  badQuality: 600,
  fault: 500,
  alarm: 400,
  uncertainQuality: 350,
  commandIntent: 300,
  transitioning: 200,
  active: 100,
  inactive: 90,
  unknown: 0
});

/**
 * Resolves one deterministic visual state for an industrial Dynamo instance.
 * Safety/diagnostic conditions intentionally dominate operator intent and normal
 * process state, so a command cannot visually hide bad quality, a fault or alarm.
 */
export function resolveDynamoVisualState(input: DynamoStateInputs): DynamoResolvedVisualState {
  const quality = input.quality ?? 'unknown';
  const commandIntent = input.commandIntent ?? null;
  const settledState = input.settledState ?? 'unknown';

  if (quality === 'bad' || quality === 'stale' || quality === 'unknown') {
    return state('bad-quality', DYNAMO_STATE_PRECEDENCE.badQuality, quality, commandIntent, settledState);
  }

  if (input.fault === true) {
    return state('fault', DYNAMO_STATE_PRECEDENCE.fault, quality, commandIntent, settledState);
  }

  if (input.alarm === true) {
    return state('alarm', DYNAMO_STATE_PRECEDENCE.alarm, quality, commandIntent, settledState);
  }

  if (quality === 'uncertain') {
    return state(
      'uncertain-quality',
      DYNAMO_STATE_PRECEDENCE.uncertainQuality,
      quality,
      commandIntent,
      settledState
    );
  }

  if (commandIntent !== null) {
    return state('command-intent', DYNAMO_STATE_PRECEDENCE.commandIntent, quality, commandIntent, settledState);
  }

  if (settledState === 'transitioning') {
    return state('transitioning', DYNAMO_STATE_PRECEDENCE.transitioning, quality, commandIntent, settledState);
  }

  if (settledState === 'active') {
    return state('active', DYNAMO_STATE_PRECEDENCE.active, quality, commandIntent, settledState);
  }

  if (settledState === 'inactive') {
    return state('inactive', DYNAMO_STATE_PRECEDENCE.inactive, quality, commandIntent, settledState);
  }

  return state('unknown', DYNAMO_STATE_PRECEDENCE.unknown, quality, commandIntent, settledState);
}

function state(
  kind: DynamoResolvedVisualStateKind,
  priority: number,
  quality: DynamoQualityState,
  commandIntent: DynamoCommandIntent,
  settledState: DynamoSettledState
): DynamoResolvedVisualState {
  return Object.freeze({ kind, priority, quality, commandIntent, settledState });
}
