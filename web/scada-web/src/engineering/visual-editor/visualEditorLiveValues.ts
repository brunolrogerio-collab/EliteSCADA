import { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import type {
  BindingEngineering,
  TagValueReferenceEngineering,
  VisualElementEngineering,
  VisualExpressionEngineering,
  VisualValueSourceEngineering
} from '../types';
import { initializeClientMemory, readClientMemoryValue } from '../../runtime/clientMemory';
import {
  loadReadableRuntimeTags,
  openRuntimeTagSocket,
  parseRuntimeTagRealtimeMessage
} from '../../runtime/liveTagTransport';
import { visualTagSampleKey, type VisualDynamicSample } from './visualDynamicRuntime';

export type VisualLiveScalarSample = VisualDynamicSample;

type RuntimeSourceRequest = Readonly<{
  kind: 'tag' | 'clientmemory';
  target: string;
  tagReference?: TagValueReferenceEngineering | null;
  dataType?: string | null;
}>;

export function useVisualBindingSamples(
  elements: readonly VisualElementEngineering[] | null | undefined
): ReadonlyMap<string, VisualLiveScalarSample> {
  const bindings = useMemo(() => collectBindings(elements), [elements]);
  const requests = useMemo(() => collectRuntimeSourceRequests(elements), [elements]);
  const tagRequests = useMemo(() => requests.filter(request => request.kind === 'tag'), [requests]);
  const clientRequests = useMemo(() => requests.filter(request => request.kind === 'clientmemory'), [requests]);
  const [samples, setSamples] = useState<ReadonlyMap<string, VisualLiveScalarSample>>(() => new Map());

  useEffect(() => {
    if (tagRequests.length === 0) return undefined;
    let cancelled = false;
    const wantedIds = new Set(tagRequests.map(request => request.tagReference?.tagId?.trim().toLocaleLowerCase()).filter(Boolean) as string[]);
    const wantedPaths = new Set(tagRequests.map(request => request.target).filter(Boolean));

    const refresh = async () => {
      try {
        const tags = await loadReadableRuntimeTags();
        if (cancelled) return;
        setSamples(current => {
          const next = new Map(current);
          const seenIds = new Set<string>();
          const seenPaths = new Set<string>();
          for (const tag of tags) {
            const id = tag.id.trim().toLocaleLowerCase();
            if (!wantedIds.has(id) && !wantedPaths.has(tag.path)) continue;
            seenIds.add(id);
            seenPaths.add(tag.path);
            const currentValue = tag.current;
            const sample: VisualLiveScalarSample = currentValue ? Object.freeze({
              reference: tag.path,
              tagId: tag.id,
              value: currentValue.value,
              dataType: tag.dataType,
              quality: currentValue.quality ?? null,
              timestamp: currentValue.sourceTimestamp ?? currentValue.serverTimestamp ?? currentValue.timestamp ?? null
            }) : Object.freeze({
              reference: tag.path,
              tagId: tag.id,
              value: null,
              dataType: tag.dataType,
              state: 'Unavailable'
            });
            next.set(tag.path, sample);
            next.set(visualTagSampleKey(tag.id), sample);
          }
          for (const request of tagRequests) {
            const id = request.tagReference?.tagId?.trim().toLocaleLowerCase();
            const found = (id && seenIds.has(id)) || seenPaths.has(request.target);
            if (found) continue;
            const unavailable = Object.freeze({
              reference: request.target,
              tagId: request.tagReference?.tagId ?? null,
              value: null,
              dataType: request.dataType ?? bindingDataType(bindings, request.target),
              state: 'Unavailable'
            });
            if (request.target) next.set(request.target, unavailable);
            if (request.tagReference?.tagId) next.set(visualTagSampleKey(request.tagReference.tagId), unavailable);
          }
          return next;
        });
      } catch {
        if (!cancelled) markRequestsUnavailable(setSamples, tagRequests, bindings);
      }
    };

    void refresh();
    const refreshTimer = window.setInterval(() => void refresh(), 3000);
    const socket = openRuntimeTagSocket();
    socket.addEventListener('message', event => {
      const message = parseRuntimeTagRealtimeMessage(String(event.data));
      if (!message) return;
      const id = message.tag.id.trim().toLocaleLowerCase();
      if (!wantedIds.has(id) && !wantedPaths.has(message.tag.path)) return;
      setSamples(current => {
        const next = new Map(current);
        const existing = next.get(visualTagSampleKey(message.tag.id)) ?? next.get(message.tag.path);
        const sample = Object.freeze({
          reference: message.tag.path,
          tagId: message.tag.id,
          value: message.value,
          dataType: existing?.dataType ?? requestDataType(tagRequests, message.tag.id, message.tag.path),
          quality: message.quality,
          timestamp: message.timestamp
        });
        next.set(message.tag.path, sample);
        next.set(visualTagSampleKey(message.tag.id), sample);
        return next;
      });
    });
    socket.addEventListener('close', () => {
      if (!cancelled) markRequestsUnavailable(setSamples, tagRequests, bindings, true);
    });

    return () => {
      cancelled = true;
      window.clearInterval(refreshTimer);
      socket.close();
    };
  }, [tagRequests, bindings]);

  useEffect(() => {
    if (clientRequests.length === 0) return undefined;
    let cancelled = false;
    const unique = uniqueRequests(clientRequests);
    const refresh = () => {
      if (cancelled) return;
      const now = new Date().toISOString();
      setSamples(current => {
        const next = new Map(current);
        for (const request of unique) {
          const sample = Object.freeze({
            reference: request.target,
            tagId: request.tagReference?.tagId ?? null,
            value: readClientMemoryValue(request.target),
            dataType: request.dataType ?? bindingDataType(bindings, request.target),
            state: 'LocalSession',
            timestamp: now
          });
          next.set(request.target, sample);
          if (request.tagReference?.tagId) next.set(visualTagSampleKey(request.tagReference.tagId), sample);
        }
        return next;
      });
    };
    void initializeClientMemory().then(refresh).catch(() => markRequestsUnavailable(setSamples, unique, bindings));
    const timer = window.setInterval(refresh, 500);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [clientRequests, bindings]);

  return samples;
}

export function formatVisualScalarText(
  sample: VisualLiveScalarSample | undefined,
  binding: BindingEngineering,
  locale: EngineeringLocale
): Readonly<{ text: string; available: boolean; state: string }> {
  if (!sample) return Object.freeze({ text: '—', available: false, state: unavailableLabel(locale) });
  const state = qualityState(sample);
  const available = state.available;
  const sourceType = binding.metadata?.sourceDataType || sample.dataType || 'String';
  const unit = binding.metadata?.engineeringUnit?.trim() || '';
  if (!available || sample.value === null || sample.value === undefined) {
    return Object.freeze({ text: '—', available: false, state: state.label });
  }

  let text: string;
  const normalizedType = sourceType.trim().toLowerCase();
  if (normalizedType === 'boolean') {
    text = typeof sample.value === 'boolean'
      ? localizedBoolean(sample.value, locale)
      : String(sample.value);
  } else if (['int16', 'int32', 'int64', 'enum'].includes(normalizedType)) {
    text = typeof sample.value === 'string' ? sample.value : String(sample.value);
  } else if (['float', 'double'].includes(normalizedType)) {
    const numeric = typeof sample.value === 'number' ? sample.value : Number(sample.value);
    if (!Number.isFinite(numeric)) return Object.freeze({ text: '—', available: false, state: state.label });
    const decimalPlaces = parseDecimalPlaces(binding.metadata?.decimalPlaces);
    text = new Intl.NumberFormat(locale, {
      maximumFractionDigits: decimalPlaces ?? 6,
      minimumFractionDigits: decimalPlaces ?? 0,
      useGrouping: false
    }).format(numeric);
  } else if (normalizedType === 'datetime') {
    const date = new Date(String(sample.value));
    text = Number.isNaN(date.getTime())
      ? String(sample.value)
      : new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
  } else {
    text = String(sample.value);
  }

  const prefix = binding.metadata?.prefix ?? '';
  const suffix = binding.metadata?.suffix ?? '';
  const joined = `${prefix}${text}${unit ? ` ${unit}` : ''}${suffix}`;
  return Object.freeze({ text: joined, available: true, state: state.label });
}

function collectBindings(elements: readonly VisualElementEngineering[] | null | undefined): readonly BindingEngineering[] {
  const result: BindingEngineering[] = [];
  const visit = (element: VisualElementEngineering) => {
    for (const binding of element.bindings ?? []) result.push(binding);
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements ?? []) visit(element);
  return Object.freeze(result);
}

function collectRuntimeSourceRequests(elements: readonly VisualElementEngineering[] | null | undefined): readonly RuntimeSourceRequest[] {
  const result: RuntimeSourceRequest[] = [];
  const addExpression = (expression: VisualExpressionEngineering | null | undefined) => {
    for (const dependency of expression?.dependencies ?? []) {
      result.push(Object.freeze({
        kind: dependency.kind === 'ClientMemory' ? 'clientmemory' : 'tag',
        target: dependency.target ?? dependency.symbol,
        tagReference: dependency.tagReference,
        dataType: dependency.valueType === 'Boolean' ? 'Boolean' : null
      }));
    }
  };
  const addSource = (source: VisualValueSourceEngineering | null | undefined) => {
    if (!source) return;
    if (source.kind === 'Expression') {
      addExpression(source.expression);
      return;
    }
    result.push(Object.freeze({
      kind: source.kind === 'ClientMemory' ? 'clientmemory' : 'tag',
      target: source.target ?? source.tagReference?.tagId ?? '',
      tagReference: source.tagReference ?? null,
      dataType: source.valueType === 'Boolean' ? 'Boolean' : null
    }));
  };
  const visit = (element: VisualElementEngineering) => {
    for (const binding of element.bindings ?? []) {
      const kind = binding.kind?.trim().toLowerCase();
      if (kind === 'tag' || kind === 'clientmemory') {
        result.push(Object.freeze({
          kind,
          target: binding.target,
          tagReference: binding.tagReference ?? null,
          dataType: binding.metadata?.sourceDataType ?? null
        }));
      }
    }
    for (const configured of element.propertyExpressions ?? []) addExpression(configured.expression);
    for (const condition of element.booleanConditions ?? []) addSource(condition.source);
    addSource(element.analogFill?.source);
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements ?? []) visit(element);
  return Object.freeze(uniqueRequests(result));
}

function uniqueRequests(requests: readonly RuntimeSourceRequest[]): readonly RuntimeSourceRequest[] {
  const seen = new Set<string>();
  const result: RuntimeSourceRequest[] = [];
  for (const request of requests) {
    const key = `${request.kind}|${request.tagReference?.tagId?.toLocaleLowerCase() ?? ''}|${request.target}`;
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(request);
  }
  return Object.freeze(result);
}

function bindingDataType(bindings: readonly BindingEngineering[], reference: string): string {
  return bindings.find(binding => binding.target === reference)?.metadata?.sourceDataType ?? 'String';
}

function requestDataType(requests: readonly RuntimeSourceRequest[], tagId: string, path: string): string {
  const normalizedId = tagId.trim().toLocaleLowerCase();
  return requests.find(request => request.tagReference?.tagId?.trim().toLocaleLowerCase() === normalizedId || request.target === path)?.dataType ?? 'String';
}

function markRequestsUnavailable(
  setter: (value: (current: ReadonlyMap<string, VisualLiveScalarSample>) => ReadonlyMap<string, VisualLiveScalarSample>) => void,
  requests: readonly RuntimeSourceRequest[],
  bindings: readonly BindingEngineering[],
  disconnected = false
) {
  setter(current => {
    const next = new Map(current);
    for (const request of requests) {
      const existing = request.tagReference?.tagId
        ? next.get(visualTagSampleKey(request.tagReference.tagId)) ?? next.get(request.target)
        : next.get(request.target);
      const sample = Object.freeze({
        reference: request.target,
        tagId: request.tagReference?.tagId ?? existing?.tagId ?? null,
        value: existing?.value ?? null,
        dataType: existing?.dataType ?? request.dataType ?? bindingDataType(bindings, request.target),
        quality: existing?.quality ?? null,
        state: disconnected ? 'Disconnected' : 'Unavailable',
        timestamp: existing?.timestamp ?? null
      });
      if (request.target) next.set(request.target, sample);
      if (request.tagReference?.tagId) next.set(visualTagSampleKey(request.tagReference.tagId), sample);
    }
    return next;
  });
}

function qualityState(sample: VisualLiveScalarSample): Readonly<{ available: boolean; label: string }> {
  if (sample.state && sample.state !== 'LocalSession') return Object.freeze({ available: false, label: sample.state });
  if (sample.quality === undefined || sample.quality === null) {
    return Object.freeze({ available: true, label: sample.state ?? 'N/A' });
  }
  if (sample.quality === 0 || String(sample.quality).toLowerCase() === 'good') return Object.freeze({ available: true, label: 'Good' });
  return Object.freeze({ available: false, label: String(sample.quality) });
}

function localizedBoolean(value: boolean, locale: EngineeringLocale): string {
  if (locale === 'en') return value ? 'True' : 'False';
  if (locale === 'es') return value ? 'Verdadero' : 'Falso';
  return value ? 'Verdadeiro' : 'Falso';
}

function unavailableLabel(locale: EngineeringLocale): string {
  if (locale === 'en') return 'Unavailable';
  if (locale === 'es') return 'No disponible';
  return 'Indisponível';
}

function parseDecimalPlaces(value: string | undefined): number | undefined {
  if (value === undefined) return undefined;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 && parsed <= 12 ? parsed : undefined;
}
