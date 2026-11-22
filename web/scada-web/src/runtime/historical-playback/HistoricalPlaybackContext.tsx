import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import type { VisualLiveScalarSample } from '../../engineering/visual-editor/visualEditorLiveValues';
import { createHistoricalTimeRangeState, resolveHistoricalTimeRange, validateHistoricalTimeRange, type HistoricalTimeRangeState } from '../historicalTimeRange';
import { loadHistoricalPlaybackSamples, type HistoricalPlaybackResolvedRange, type HistoricalPlaybackVisualScope } from './historicalPlaybackApi';
import { setRuntimeHistoricalPlaybackActive } from './runtimeHistoricalPlaybackGuard';

export type RuntimeTemporalMode = 'live' | 'historicalPlayback';
export type HistoricalPlaybackLoadState = 'idle' | 'loading' | 'ready' | 'error';

export type HistoricalPlaybackContextValue = Readonly<{
  mode: RuntimeTemporalMode;
  timeRange: HistoricalTimeRangeState;
  resolvedRange: HistoricalPlaybackResolvedRange | null;
  atUtc: string | null;
  position: number;
  loadState: HistoricalPlaybackLoadState;
  error: string | null;
  samples: ReadonlyMap<string, VisualLiveScalarSample>;
  resolvedTagCount: number;
  gapCount: number;
  visualScope: HistoricalPlaybackVisualScope | null;
  enterPlayback: () => void;
  exitPlayback: () => void;
  setTimeRange: (range: HistoricalTimeRangeState) => void;
  setPosition: (position: number) => void;
  setVisualScope: (scope: HistoricalPlaybackVisualScope) => void;
  refresh: () => void;
}>;

const DEFAULT_POSITION = 1;
const HistoricalPlaybackContext = createContext<HistoricalPlaybackContextValue | null>(null);

export function HistoricalPlaybackProvider({ children }: Readonly<{ children: React.ReactNode }>) {
  const [mode, setMode] = useState<RuntimeTemporalMode>('live');
  const [timeRange, setTimeRangeState] = useState<HistoricalTimeRangeState>(() =>
    createHistoricalTimeRangeState({ mode: 'relative', durationSeconds: 60 * 60 }));
  const [position, setPositionState] = useState(DEFAULT_POSITION);
  const [anchorUtc, setAnchorUtc] = useState(() => new Date());
  const [visualScope, setVisualScopeState] = useState<HistoricalPlaybackVisualScope | null>(null);
  const [refreshRevision, setRefreshRevision] = useState(0);
  const [loadState, setLoadState] = useState<HistoricalPlaybackLoadState>('idle');
  const [error, setError] = useState<string | null>(null);
  const [samples, setSamples] = useState<ReadonlyMap<string, VisualLiveScalarSample>>(() => new Map());
  const [resolvedTagCount, setResolvedTagCount] = useState(0);
  const [gapCount, setGapCount] = useState(0);
  const generation = useRef(0);

  const resolvedRange = useMemo<HistoricalPlaybackResolvedRange | null>(() => {
    if (!validateHistoricalTimeRange(timeRange).ok) return null;
    const resolved = resolveHistoricalTimeRange(timeRange, anchorUtc);
    return Object.freeze({ fromUtc: resolved.from, toUtc: resolved.to });
  }, [timeRange, anchorUtc]);

  const atUtc = useMemo(() => {
    if (!resolvedRange) return null;
    const from = new Date(resolvedRange.fromUtc).getTime();
    const to = new Date(resolvedRange.toUtc).getTime();
    if (!Number.isFinite(from) || !Number.isFinite(to) || to <= from) return null;
    return new Date(from + ((to - from) * clamp(position))).toISOString();
  }, [resolvedRange, position]);

  useEffect(() => {
    setRuntimeHistoricalPlaybackActive(mode === 'historicalPlayback');
    return () => setRuntimeHistoricalPlaybackActive(false);
  }, [mode]);

  useEffect(() => {
    if (mode !== 'historicalPlayback' || !resolvedRange || !atUtc || !visualScope) {
      generation.current += 1;
      setLoadState('idle'); setError(null); setSamples(new Map());
      setResolvedTagCount(0); setGapCount(0);
      return undefined;
    }
    const current = ++generation.current;
    const controller = new AbortController();
    setLoadState('loading'); setError(null); setSamples(new Map());
    void loadHistoricalPlaybackSamples(visualScope, resolvedRange, atUtc, controller.signal)
      .then(result => {
        if (controller.signal.aborted || generation.current !== current) return;
        setSamples(result.samples); setResolvedTagCount(result.resolvedTags);
        setGapCount(result.gaps); setLoadState('ready');
      })
      .catch(reason => {
        if (controller.signal.aborted || generation.current !== current) return;
        setSamples(new Map()); setResolvedTagCount(0); setGapCount(0);
        setError(reason instanceof Error ? reason.message : String(reason)); setLoadState('error');
      });
    return () => controller.abort();
  }, [mode, resolvedRange, atUtc, visualScope, refreshRevision]);

  const enterPlayback = useCallback(() => {
    if (!validateHistoricalTimeRange(timeRange).ok || !visualScope) return;
    setRuntimeHistoricalPlaybackActive(true);
    if (timeRange.mode === 'relative') setAnchorUtc(new Date());
    setMode('historicalPlayback');
  }, [timeRange, visualScope]);

  const exitPlayback = useCallback(() => {
    generation.current += 1; setRuntimeHistoricalPlaybackActive(false); setMode('live');
    setLoadState('idle'); setError(null); setSamples(new Map());
    setResolvedTagCount(0); setGapCount(0);
  }, []);

  const setTimeRange = useCallback((range: HistoricalTimeRangeState) => {
    const normalized = range.mode === 'live' ? Object.freeze({ ...range, mode: 'relative' as const }) : range;
    setTimeRangeState(normalized);
    if (normalized.mode === 'relative') setAnchorUtc(new Date());
  }, []);
  const setPosition = useCallback((next: number) => setPositionState(clamp(next)), []);
  const setVisualScope = useCallback((scope: HistoricalPlaybackVisualScope) => {
    setVisualScopeState(current =>
      current?.screenKey === scope.screenKey &&
      current.popupKeys.length === scope.popupKeys.length &&
      current.popupKeys.every((key, index) => key === scope.popupKeys[index])
        ? current : Object.freeze({ screenKey: scope.screenKey, popupKeys: Object.freeze([...scope.popupKeys]) }));
  }, []);
  const refresh = useCallback(() => {
    if (timeRange.mode === 'relative') setAnchorUtc(new Date());
    setRefreshRevision(value => value + 1);
  }, [timeRange.mode]);

  const value = useMemo<HistoricalPlaybackContextValue>(() => Object.freeze({
    mode,timeRange,resolvedRange,atUtc,position,loadState,error,samples,resolvedTagCount,gapCount,visualScope,
    enterPlayback,exitPlayback,setTimeRange,setPosition,setVisualScope,refresh
  }), [mode,timeRange,resolvedRange,atUtc,position,loadState,error,samples,resolvedTagCount,gapCount,visualScope,
    enterPlayback,exitPlayback,setTimeRange,setPosition,setVisualScope,refresh]);

  return <HistoricalPlaybackContext.Provider value={value}>{children}</HistoricalPlaybackContext.Provider>;
}

export function useHistoricalPlayback(): HistoricalPlaybackContextValue {
  const value = useContext(HistoricalPlaybackContext);
  if (!value) throw new Error('Historical Playback Runtime context is not mounted.');
  return value;
}
export function useOptionalHistoricalPlayback(): HistoricalPlaybackContextValue | null {
  return useContext(HistoricalPlaybackContext);
}
function clamp(value:number){ return Number.isFinite(value) ? Math.max(0,Math.min(1,value)) : DEFAULT_POSITION; }
