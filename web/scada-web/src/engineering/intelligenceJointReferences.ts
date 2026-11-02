export type IntelligenceJointSignalReference = Readonly<{ id: string; tagId: string; label: string }>;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function readIntelligenceJointSignals(json: string): readonly IntelligenceJointSignalReference[] | null {
  if (typeof json !== 'string' || json.length > 16384) return null;
  try {
    const values = JSON.parse(json);
    if (!Array.isArray(values) || values.length < 2 || values.length > 16) return null;
    const ids = new Set<string>(), tags = new Set<string>();
    for (const item of values) {
      if (!item || typeof item !== 'object' || Array.isArray(item) || Object.keys(item).some(key => !['id', 'tagId', 'label'].includes(key)) ||
          typeof item.id !== 'string' || !UUID.test(item.id) || ids.has(item.id.toLowerCase()) ||
          typeof item.tagId !== 'string' || !UUID.test(item.tagId) || tags.has(item.tagId.toLowerCase()) ||
          typeof item.label !== 'string' || !item.label.trim() || item.label !== item.label.trim() || item.label.length > 320) return null;
      ids.add(item.id.toLowerCase()); tags.add(item.tagId.toLowerCase());
    }
    return values.map(item => ({ id: item.id, tagId: item.tagId, label: item.label }));
  } catch { return null; }
}
