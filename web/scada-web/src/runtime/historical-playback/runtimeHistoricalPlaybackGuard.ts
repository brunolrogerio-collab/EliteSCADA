let historicalPlaybackActive = false;

export class RuntimeHistoricalPlaybackReadOnlyError extends Error {
  constructor(public readonly operation: string) {
    super(`Runtime operation '${operation}' is blocked while Historical Playback is active.`);
    this.name = 'RuntimeHistoricalPlaybackReadOnlyError';
  }
}

export function setRuntimeHistoricalPlaybackActive(active: boolean): void {
  historicalPlaybackActive = active;
}

export function isRuntimeHistoricalPlaybackActive(): boolean {
  return historicalPlaybackActive;
}

export function assertRuntimeProcessMutationAllowed(operation: string): void {
  if (historicalPlaybackActive) throw new RuntimeHistoricalPlaybackReadOnlyError(operation);
}
