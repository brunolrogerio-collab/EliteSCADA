import type { AssetProcessIntelligenceProfileEngineering, TagEngineering } from './types';

export type IntelligenceCurrentFrequencySignal = Readonly<{
  id: string; tagId: string; label: string; kind: 'current' | 'frequency'; basis: string;
}>;
const numeric = new Set(['byte','sbyte','int8','uint8','short','int16','uint16','int','integer','int32','uint32',
  'long','int64','uint64','single','float','float32','double','float64','decimal','number']);

/** Mirrors the bounded ADR-015 input contract; display units and legacy roles never infer eligibility. */
export function currentFrequencySignals(profile: AssetProcessIntelligenceProfileEngineering, tags: readonly TagEngineering[]): readonly IntelligenceCurrentFrequencySignal[] {
  if (new Set(profile.signals.map(signal => signal.id.toLowerCase())).size !== profile.signals.length ||
      new Set(profile.signals.map(signal => signal.tagId.toLowerCase())).size !== profile.signals.length) return [];
  return profile.signals.flatMap(signal => {
    const matches = tags.filter(tag => tag.id?.toLowerCase() === signal.tagId.toLowerCase());
    const tag = matches.length === 1 ? matches[0] : null;
    const binding = signal.semanticBinding, unit = tag?.controlledEngineeringUnit;
    if (!tag || !numeric.has(tag.dataType.toLowerCase()) || binding?.catalogVersion !== 1 || unit?.catalogVersion !== 1 ||
        binding.pressureReference != null || binding.flowDirection != null) return [];
    let kind: 'current' | 'frequency', basis: string;
    if (binding.roleCode === 'pump.electrical_current' && unit.code === 'unit.electric_current.ampere' &&
        binding.signalKind == null && binding.frequencyReference == null && ['line_rms','phase_rms','dc_bus'].includes(binding.currentBasis ?? '')) {
      kind = 'current'; basis = binding.currentBasis!;
    } else if (binding.roleCode === 'pump.drive_frequency' && unit.code === 'unit.frequency.hertz' &&
        binding.currentBasis == null && binding.signalKind === 'measured_output' && ['drive_electrical','shaft_rotational'].includes(binding.frequencyReference ?? '')) {
      kind = 'frequency'; basis = binding.frequencyReference!;
    } else return [];
    return [{ id: signal.id, tagId: signal.tagId, label: tag.name || tag.path, kind, basis }];
  });
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function readCurrentFrequencySignals(json: string): readonly IntelligenceCurrentFrequencySignal[] | null {
  if (typeof json !== 'string' || json.length > 16384) return null;
  try {
    const items: unknown = JSON.parse(json);
    if (!Array.isArray(items) || items.length < 2 || items.length > 16) return null;
    const ids = new Set<string>(), tags = new Set<string>();
    for (const item of items) {
      if (!item || typeof item !== 'object' || Array.isArray(item) || Object.keys(item).some(key => !['id','tagId','label','kind','basis'].includes(key)) ||
          typeof item.id !== 'string' || !UUID.test(item.id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(item.id) || ids.has(item.id.toLowerCase()) ||
          typeof item.tagId !== 'string' || !UUID.test(item.tagId) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(item.tagId) || tags.has(item.tagId.toLowerCase()) ||
          typeof item.label !== 'string' || !item.label.trim() || item.label !== item.label.trim() || item.label.length > 320 ||
          !(item.kind === 'current' && ['line_rms','phase_rms','dc_bus'].includes(item.basis) || item.kind === 'frequency' && ['drive_electrical','shaft_rotational'].includes(item.basis))) return null;
      ids.add(item.id.toLowerCase()); tags.add(item.tagId.toLowerCase());
    }
    if (!items.some(item => item.kind === 'current') || !items.some(item => item.kind === 'frequency')) return null;
    return items.map(item => ({ id: item.id, tagId: item.tagId, label: item.label, kind: item.kind, basis: item.basis }));
  } catch { return null; }
}
