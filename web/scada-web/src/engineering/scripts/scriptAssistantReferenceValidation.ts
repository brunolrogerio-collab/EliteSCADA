import type {
  ScriptAssistantCatalog,
  ScriptAssistantVisualObject
} from './scriptAssistantModel';

export type ScriptAssistantReferenceDiagnosticCode =
  | 'SCRIPT_REFERENCE_TAG_MISSING'
  | 'SCRIPT_REFERENCE_OBJECT_MISSING'
  | 'SCRIPT_REFERENCE_PROPERTY_MISSING'
  | 'SCRIPT_REFERENCE_CLIENT_MEMORY_MISSING'
  | 'SCRIPT_REFERENCE_LEGACY_OBJECT_KEY'
  | 'SCRIPT_REFERENCE_LEGACY_OBJECT_KEY_AMBIGUOUS';

export type ScriptAssistantReferenceDiagnostic = Readonly<{
  code: ScriptAssistantReferenceDiagnosticCode;
  line: number;
  column: number;
  reference: string;
  propertyKey?: string;
  canonicalReference?: string;
}>;

/**
 * Conservative literal-reference validation. Stable references are resolved
 * exactly. Legacy object Key aliases are detected but never auto-retargeted or
 * rewritten, even when the current project happens to contain one matching Key.
 */
export function validateScriptAssistantReferences(
  source: string,
  catalog: ScriptAssistantCatalog
): readonly ScriptAssistantReferenceDiagnostic[] {
  const diagnostics: ScriptAssistantReferenceDiagnostic[] = [];
  const tagReferences = new Set(catalog.tags
    .map(tag => tag.canonicalReference)
    .filter((value): value is string => Boolean(value)));
  const clientMemoryReferences = new Set(catalog.clientMemory.flatMap(memory => [memory.id, memory.path].filter(Boolean)));
  const visualObjects = new Map<string, ScriptAssistantVisualObject>();
  const legacyKeys = new Map<string, ScriptAssistantVisualObject[]>();

  for (const definition of [...catalog.screens, ...catalog.popups]) {
    for (const object of definition.objects) indexVisualObject(object, visualObjects, legacyKeys);
  }

  for (const call of findSingleReferenceCalls(source, ['tag_read', 'tag_write'])) {
    if (!tagReferences.has(call.reference)) diagnostics.push(Object.freeze({
      code: 'SCRIPT_REFERENCE_TAG_MISSING', line: call.line, column: call.column, reference: call.reference
    }));
  }

  for (const call of findSingleReferenceCalls(source, ['client_memory_read', 'client_memory_write'])) {
    if (!clientMemoryReferences.has(call.reference)) diagnostics.push(Object.freeze({
      code: 'SCRIPT_REFERENCE_CLIENT_MEMORY_MISSING', line: call.line, column: call.column, reference: call.reference
    }));
  }

  for (const call of findVisualPropertyCalls(source)) {
    const canonical = visualObjects.get(call.reference);
    if (canonical) {
      validateProperty(canonical, call, diagnostics);
      continue;
    }

    const byKey = legacyKeys.get(call.reference.toLocaleLowerCase('en-US')) ?? [];
    if (byKey.length > 0) {
      diagnostics.push(Object.freeze({
        code: byKey.length === 1
          ? 'SCRIPT_REFERENCE_LEGACY_OBJECT_KEY'
          : 'SCRIPT_REFERENCE_LEGACY_OBJECT_KEY_AMBIGUOUS',
        line: call.line,
        column: call.column,
        reference: call.reference,
        propertyKey: call.propertyKey,
        canonicalReference: byKey.length === 1 ? byKey[0].canonicalReference ?? undefined : undefined
      }));
      continue;
    }

    diagnostics.push(Object.freeze({
      code: 'SCRIPT_REFERENCE_OBJECT_MISSING',
      line: call.line,
      column: call.column,
      reference: call.reference,
      propertyKey: call.propertyKey
    }));
  }

  return Object.freeze(diagnostics.sort((left, right) => left.line - right.line || left.column - right.column));
}

type LiteralReferenceCall = Readonly<{ reference: string; line: number; column: number }>;
type VisualPropertyCall = LiteralReferenceCall & Readonly<{ propertyKey: string }>;

function validateProperty(
  object: ScriptAssistantVisualObject,
  call: VisualPropertyCall,
  diagnostics: ScriptAssistantReferenceDiagnostic[]
): void {
  if (!object.properties.some(property => property.key === call.propertyKey)) diagnostics.push(Object.freeze({
    code: 'SCRIPT_REFERENCE_PROPERTY_MISSING',
    line: call.line,
    column: call.column,
    reference: call.reference,
    propertyKey: call.propertyKey
  }));
}

function findSingleReferenceCalls(source: string, functions: readonly string[]): LiteralReferenceCall[] {
  const names = functions.map(escapeRegex).join('|');
  const pattern = new RegExp(`\\b(?:${names})\\s*\\(\\s*(["'])([^"'\\r\\n]*)\\1`, 'g');
  const calls: LiteralReferenceCall[] = [];
  let match: RegExpExecArray | null;
  while ((match = pattern.exec(source)) !== null) {
    calls.push(Object.freeze({ reference: match[2], ...sourcePosition(source, match.index) }));
  }
  return calls;
}

function findVisualPropertyCalls(source: string): VisualPropertyCall[] {
  const calls: VisualPropertyCall[] = [];
  const propertyPattern = /\bvisual_property_(?:read|write|clear)\s*\(\s*(["'])([^"'\r\n]*)\1\s*,\s*(["'])([^"'\r\n]*)\3/g;
  let match: RegExpExecArray | null;
  while ((match = propertyPattern.exec(source)) !== null) {
    calls.push(Object.freeze({ reference: match[2], propertyKey: match[4], ...sourcePosition(source, match.index) }));
  }

  const tweenPattern = /\bvisual_tween_request\s*\(\s*\{[\s\S]*?["']targetReference["']\s*:\s*(["'])([^"'\r\n]*)\1[\s\S]*?["']propertyKey["']\s*:\s*(["'])([^"'\r\n]*)\3[\s\S]*?\}\s*\)/g;
  while ((match = tweenPattern.exec(source)) !== null) {
    calls.push(Object.freeze({ reference: match[2], propertyKey: match[4], ...sourcePosition(source, match.index) }));
  }
  return calls;
}

function indexVisualObject(
  object: ScriptAssistantVisualObject,
  index: Map<string, ScriptAssistantVisualObject>,
  legacyKeys: Map<string, ScriptAssistantVisualObject[]>
): void {
  if (object.canonicalReference) index.set(object.canonicalReference, object);
  const key = object.key.toLocaleLowerCase('en-US');
  const matches = legacyKeys.get(key) ?? [];
  matches.push(object);
  legacyKeys.set(key, matches);
  for (const child of object.children) indexVisualObject(child, index, legacyKeys);
}

function sourcePosition(source: string, index: number): { line: number; column: number } {
  const before = source.slice(0, index);
  const line = before.split('\n').length;
  const lastBreak = before.lastIndexOf('\n');
  return { line, column: index - lastBreak };
}

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^$()|[\]\\]/g, '\\$&');
}
