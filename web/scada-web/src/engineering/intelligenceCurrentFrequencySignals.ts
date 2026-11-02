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
