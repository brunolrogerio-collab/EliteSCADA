import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState
} from 'react';
import type {
  BindingEngineering,
  EngineeringPackageView,
  VisualElementEngineering
} from '../../engineering/types';
import {
  collectRuntimeVisualSourceRequests,
  type RuntimeVisualSourceRequest,
  type VisualLiveScalarSample
} from '../../engineering/visual-editor/visualEditorLiveValues';
import { asCanonicalVisualElement } from '../visual-navigation/runtimeVisualNavigationModel';
import {
  createHistoricalTimeRangeState,
  resolveHistoricalTimeRange,
  validateHistoricalTimeRange,
  type HistoricalTimeRangeState
} from '../historicalTimeRange';
import {
  loadHistoricalPlaybackSamples,
  type HistoricalPlaybackResolvedRange
} from './historicalPlaybackApi';
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
  canReturnLive: boolean;
  enterPlayback: () => void;
  exitPlayback: () => void;
  setTimeRange: (range: HistoricalTimeRangeState) => void;
  setPosition: (position: number) => void;
  refresh: () => void;
}>;

const DEFAULT_RANGE_SECONDS = 60 * 60;
const DEFAULT_POSITION = 0.5;

const HistoricalPlaybackContext = createContext<HistoricalPlaybackContextValue | null>(null);

export function HistoricalPlaybackProvider({
  engineeringPackage,
  children
}: Readonly<{
  engineeringPackage: EngineeringPackageView;
  children: React.ReactNode;
}>) {
  const [mode, setMode] = useState<RuntimeTemporalMode>('live');
  const [timeRange, setTimeRangeState] = useState<HistoricalTimeRangeState>(() =>
    createHistoricalTimeRangeState({ mode: 'relative', durationSeconds: DEFAULT_RANGE_SECONDS })
  );
  const [position, setPositionState] = useState(DEFAULT_POSITION);
  const [anchorUtc, setAnchorUtc] = useState(() => new Date());
  const [refreshRevision, setRefreshRevision] = useState(0);
  const [loadState, setLoadState] = useState<HistoricalPlaybackLoadState>('idle');
  const [error, setError] = useState<string | null>(null);
  const [samples, setSamples] = useState<ReadonlyMap<string, VisualLiveScalarSample>>(() => new Map());
  const [resolvedTagCount, setResolvedTagCount] = useState(0);
  const [gapCount, setGapCount] = useState(0);
  const generation = useRef(0);

  const requests = useMemo(
    () => collectHistoricalPlaybackRequests(engineeringPackage),
    [engineeringPackage]
  );

  const resolvedRange = useMemo<HistoricalPlaybackResolvedRange | null>(() => {
    if (mode !== 'historicalPlayback' || !validateHistoricalTimeRange(timeRange).ok) return null;
    const resolved = resolveHistoricalTimeRange(timeRange, anchorUtc);
    return Object.freeze({ fromUtc: resolved.from, toUtc: resolved.to });
  }, [mode, timeRange, anchorUtc]);

  const atUtc = useMemo(() => {
    if (!resolvedRange) return null;
    const from = new Date(resolvedRange.fromUtc).getTime();
    const to = new Date(resolvedRange.toUtc).getTime();
    if (!Number.isFinite(from) || !Number.isFinite(to) || to <= from) return null;
    const ratio = clampPosition(position);
    return new Date(from + ((to - from) * ratio)).toISOString();
  }, [resolvedRange, position]);

  useEffect(() => {
    setRuntimeHistoricalPlaybackActive(mode === 'historicalPlayback');
    return () => setRuntimeHistoricalPlaybackActive(false);
  }, [mode]);

  useEffect(() => {
    if (mode !== 'historicalPlayback' || !resolvedRange || !atUtc) {
      generation.current += 1;
      setLoadState('idle');
      setError(null);
      setSamples(new Map());
      setResolvedTagCount(0);
      setGapCount(0);
      return undefined;
    }

    const currentGeneration = ++generation.current;
    const controller = new AbortController();
    setLoadState('loading');
    setError(null);
    setSamples(new Map());
    setResolvedTagCount(0);
    setGapCount(0);

    void loadHistoricalPlaybackSamples(requests, resolvedRange, atUtc, controller.signal)
      .then(result => {
        if (controller.signal.aborted || generation.current !== currentGeneration) return;
        setSamples(result.samples);
        setResolvedTagCount(result.resolvedTags);
        setGapCount(result.gaps);
        setLoadState('ready');
      })
      .catch(reason => {
        if (controller.signal.aborted || generation.current !== currentGeneration) return;
        setSamples(new Map());
        setResolvedTagCount(0);
        setGapCount(0);
        setError(reason instanceof Error ? reason.message : String(reason));
        setLoadState('error');
      });

    return () => controller.abort();
  }, [mode, requests, resolvedRange, atUtc, refreshRevision]);

  const enterPlayback = useCallback(() => {
    setAnchorUtc(new Date());
    setPositionState(DEFAULT_POSITION);
    setMode('historicalPlayback');
  }, []);

  const exitPlayback = useCallback(() => {
    generation.current += 1;
    setMode('live');
    setLoadState('idle');
    setError(null);
    setSamples(new Map());
    setResolvedTagCount(0);
    setGapCount(0);
  }, []);

  const setTimeRange = useCallback((range: HistoricalTimeRangeState) => {
    if (range.mode === 'live') {
      exitPlayback();
      return;
    }
    setAnchorUtc(new Date());
    setTimeRangeState(range);
  }, [exitPlayback]);

  const setPosition = useCallback((next: number) => {
    setPositionState(clampPosition(next));
  }, []);

  const refresh = useCallback(() => {
    setAnchorUtc(new Date());
    setRefreshRevision(value => value + 1);
  }, []);

  const value = useMemo<HistoricalPlaybackContextValue>(() => Object.freeze({
    mode,
    timeRange,
    resolvedRange,
    atUtc,
    position,
    loadState,
    error,
    samples,
    resolvedTagCount,
    gapCount,
    canReturnLive: mode === 'historicalPlayback',
    enterPlayback,
    exitPlayback,
    setTimeRange,
    setPosition,
    refresh
  }), [
    mode,
    timeRange,
    resolvedRange,
    atUtc,
    position,
    loadState,
    error,
    samples,
    resolvedTagCount,
    gapCount,
    enterPlayback,
    exitPlayback,
    setTimeRange,
    setPosition,
    refresh
  ]);

  return <HistoricalPlaybackContext.Provider value={value}>
    {children}
  </HistoricalPlaybackContext.Provider>;
}

export function useHistoricalPlayback(): HistoricalPlaybackContextValue {
  const context = useContext(HistoricalPlaybackContext);
  if (!context) {
    throw new Error('Historical Playback Runtime context is not mounted.');
  }
  return context;
}

export function useOptionalHistoricalPlayback(): HistoricalPlaybackContextValue | null {
  return useContext(HistoricalPlaybackContext);
}

export function collectHistoricalPlaybackRequests(
  engineeringPackage: EngineeringPackageView
): readonly RuntimeVisualSourceRequest[] {
  const collected: RuntimeVisualSourceRequest[] = [];

  const collectElements = (elements: readonly VisualElementEngineering[] | null | undefined) => {
    collected.push(...collectRuntimeVisualSourceRequests(elements));
    const visit = (element: VisualElementEngineering) => {
      const canonical = asCanonicalVisualElement(element);
      for (const parameter of canonical.dynamoParameters ?? []) {
        if (parameter.kind === 'TagReference' && parameter.tagReference?.tagId) {
          collected.push(Object.freeze({
            kind: 'tag',
            target: parameter.tagReference.tagId,
            tagReference: parameter.tagReference
          }));
        }
      }
      for (const child of element.children ?? []) visit(child);
    };
    for (const element of elements ?? []) visit(element);
  };

  for (const screen of engineeringPackage.screens ?? []) collectElements(screen.elements);
  for (const popup of engineeringPackage.popups ?? []) collectElements(popup.elements);
  for (const dynamo of engineeringPackage.dynamos ?? []) {
    collectBindings(collected, dynamo.bindings);
    collectElements(dynamo.elements);
    for (const parameter of dynamo.parameters ?? []) {
      if (parameter.kind === 'TagReference' && parameter.defaultTagReference?.tagId) {
        collected.push(Object.freeze({
          kind: 'tag',
          target: parameter.defaultTagReference.tagId,
          tagReference: parameter.defaultTagReference
        }));
      }
    }
  }
  for (const template of engineeringPackage.templates ?? []) {
    collectBindings(collected, template.bindings);
    collectElements(template.elements);
  }
  for (const equipment of engineeringPackage.equipment ?? []) collectBindings(collected, equipment.bindings);

  const unique = new Map<string, RuntimeVisualSourceRequest>();
  for (const request of collected) {
    const key = [
      request.kind,
      request.tagReference?.tagId?.trim().toLocaleLowerCase() ?? '',
      request.target
    ].join('|');
    if (!unique.has(key)) unique.set(key, request);
  }
  return Object.freeze([...unique.values()]);
}

function collectBindings(
  target: RuntimeVisualSourceRequest[],
  bindings: readonly BindingEngineering[] | null | undefined
): void {
  for (const binding of bindings ?? []) {
    const kind = binding.kind?.trim().toLocaleLowerCase();
    if (kind !== 'tag' && kind !== 'clientmemory') continue;
    target.push(Object.freeze({
      kind,
      target: binding.target,
      tagReference: binding.tagReference ?? null,
      dataType: binding.metadata?.sourceDataType ?? null
    }));
  }
}

function clampPosition(value: number): number {
  if (!Number.isFinite(value)) return DEFAULT_POSITION;
  return Math.max(0, Math.min(1, value));
}
