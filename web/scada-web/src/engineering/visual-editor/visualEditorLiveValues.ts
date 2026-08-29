import { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { BindingEngineering, VisualElementEngineering } from '../types';
import { initializeClientMemory, readClientMemoryValue } from '../../runtime/clientMemory';
import {
  loadReadableRuntimeTags,
  openRuntimeTagSocket,
  parseRuntimeTagRealtimeMessage
} from '../../runtime/liveTagTransport';

export type VisualLiveScalarSample = Readonly<{
  reference: string;
  value: unknown;
  dataType: string;
  quality?: string | number | null;
  state?: string | null;
  timestamp?: string | null;
}>;

export function useVisualBindingSamples(
  elements: readonly VisualElementEngineering[] | null | undefined
): ReadonlyMap<string, VisualLiveScalarSample> {
  const bindings = useMemo(() => collectScalarBindings(elements), [elements]);
  const tagBindings = useMemo(() => bindings.filter(binding => binding.kind.toLowerCase() === 'tag'), [bindings]);
  const clientBindings = useMemo(() => bindings.filter(binding => binding.kind.toLowerCase() === 'clientmemory'), [bindings]);
  const [samples, setSamples] = useState<ReadonlyMap<string, VisualLiveScalarSample>>(() => new Map());

  useEffect(() => {
    if (tagBindings.length === 0) return undefined;
    let cancelled = false;
    const tagPaths = Object.freeze([...new Set(tagBindings.map(binding => binding.target))]);
    const wanted = new Set(tagPaths);

    const refresh = async () => {
      try {
        const tags = await loadReadableRuntimeTags();
        if (cancelled) return;
        setSamples(current => {
          const next = new Map(current);
          const seen = new Set<string>();
          for (const tag of tags) {
            if (!wanted.has(tag.path)) continue;
            seen.add(tag.path);
            const currentValue = tag.current;
            next.set(tag.path, currentValue ? Object.freeze({
              reference: tag.path,
              value: currentValue.value,
              dataType: tag.dataType,
              quality: currentValue.quality ?? null,
              timestamp: currentValue.sourceTimestamp ?? currentValue.serverTimestamp ?? currentValue.timestamp ?? null
            }) : Object.freeze({
              reference: tag.path,
              value: null,
              dataType: tag.dataType,
              state: 'Unavailable'
            }));
          }
          for (const path of tagPaths) {
            if (!seen.has(path)) next.set(path, Object.freeze({ reference: path, value: null, dataType: bindingDataType(bindings, path), state: 'Unavailable' }));
          }
          return next;
        });
      } catch {
        if (!cancelled) markUnavailable(setSamples, tagPaths, bindings);
      }
    };

    void refresh();
    const refreshTimer = window.setInterval(() => void refresh(), 3000);
    const socket = openRuntimeTagSocket();
    socket.addEventListener('message', event => {
      const message = parseRuntimeTagRealtimeMessage(String(event.data));
      if (!message || !wanted.has(message.tag.path)) return;
      setSamples(current => {
        const next = new Map(current);
        const existing = next.get(message.tag.path);
        next.set(message.tag.path, Object.freeze({
          reference: message.tag.path,
          value: message.value,
          dataType: existing?.dataType ?? bindingDataType(bindings, message.tag.path),
          quality: message.quality,
          timestamp: message.timestamp
        }));
        return next;
      });
    });
    socket.addEventListener('close', () => {
      if (!cancelled) markUnavailable(setSamples, tagPaths, bindings, true);
    });

    return () => {
      cancelled = true;
      window.clearInterval(refreshTimer);
      socket.close();
    };
  }, [tagBindings, bindings]);

  useEffect(() => {
    if (clientBindings.length === 0) return undefined;
    let cancelled = false;
    const references = Object.freeze([...new Set(clientBindings.map(binding => binding.target))]);
    const refresh = () => {
      if (cancelled) return;
      const now = new Date().toISOString();
      setSamples(current => {
        const next = new Map(current);
        for (const reference of references) {
          next.set(reference, Object.freeze({
            reference,
            value: readClientMemoryValue(reference),
            dataType: bindingDataType(bindings, reference),
            state: 'LocalSession',
            timestamp: now
          }));
        }
        return next;
      });
    };
    void initializeClientMemory().then(refresh).catch(() => markUnavailable(setSamples, references, bindings));
    const timer = window.setInterval(refresh, 500);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [clientBindings, bindings]);

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

function collectScalarBindings(elements: readonly VisualElementEngineering[] | null | undefined): readonly BindingEngineering[] {
  const result: BindingEngineering[] = [];
  const visit = (element: VisualElementEngineering) => {
    for (const binding of element.bindings ?? []) {
      const kind = binding.kind?.trim().toLowerCase();
      if (binding.key === 'text' && (kind === 'tag' || kind === 'clientmemory')) result.push(binding);
    }
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements ?? []) visit(element);
  return Object.freeze(result);
}

function bindingDataType(bindings: readonly BindingEngineering[], reference: string): string {
  return bindings.find(binding => binding.target === reference)?.metadata?.sourceDataType ?? 'String';
}

function markUnavailable(
  setter: (value: (current: ReadonlyMap<string, VisualLiveScalarSample>) => ReadonlyMap<string, VisualLiveScalarSample>) => void,
  references: readonly string[],
  bindings: readonly BindingEngineering[],
  disconnected = false
) {
  setter(current => {
    const next = new Map(current);
    for (const reference of references) {
      const existing = next.get(reference);
      next.set(reference, Object.freeze({
        reference,
        value: existing?.value ?? null,
        dataType: existing?.dataType ?? bindingDataType(bindings, reference),
        quality: existing?.quality ?? null,
        state: disconnected ? 'Disconnected' : 'Unavailable',
        timestamp: existing?.timestamp ?? null
      }));
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
