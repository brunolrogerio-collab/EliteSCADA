export type IntelligenceContextReference = Readonly<{
  id: string; displayLabel: string; version: number; predicateTagIds: readonly string[];
}>;

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Only stable references and version fences are stored, never predicate values. */
export function readIntelligenceContextReferences(json: string): readonly IntelligenceContextReference[] | null {
  if (typeof json !== 'string' || json.length > 32768) return null;
  try {
    const values: unknown = JSON.parse(json);
    if (!Array.isArray(values) || values.length < 1 || values.length > 32) return null;
    const ids = new Set<string>();
    for (const item of values) {
      if (!item || typeof item !== 'object' || Array.isArray(item) ||
          Object.keys(item).some(key => !['id', 'displayLabel', 'version', 'predicateTagIds'].includes(key)) ||
          typeof item.id !== 'string' || !UUID.test(item.id) || ids.has(item.id.toLowerCase()) ||
          typeof item.displayLabel !== 'string' || !item.displayLabel.trim() || item.displayLabel !== item.displayLabel.trim() || item.displayLabel.length > 128 ||
          !Number.isInteger(item.version) || item.version < 1 || item.version > 2147483647 ||
          !Array.isArray(item.predicateTagIds) || item.predicateTagIds.length < 1 || item.predicateTagIds.length > 16 ||
          item.predicateTagIds.some((id: unknown) => typeof id !== 'string' || !UUID.test(id)) ||
          new Set(item.predicateTagIds.map((id: string) => id.toLowerCase())).size !== item.predicateTagIds.length) return null;
      ids.add(item.id.toLowerCase());
    }
    return values.map(item => ({ id: item.id, displayLabel: item.displayLabel, version: item.version, predicateTagIds: [...item.predicateTagIds] }));
  } catch { return null; }
}
