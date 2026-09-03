import type {
  ScriptAssistantCatalog,
  ScriptAssistantVisualObject
} from './scriptAssistantModel';

export type ScriptAssistantReferenceDiagnosticCode =
  | 'SCRIPT_REFERENCE_TAG_MISSING'
  | 'SCRIPT_REFERENCE_OBJECT_MISSING'
  | 'SCRIPT_REFERENCE_PROPERTY_MISSING'
  | 'SCRIPT_REFERENCE_CLIENT_MEMORY_MISSING';

export type ScriptAssistantReferenceDiagnostic = Readonly<{
  code: ScriptAssistantReferenceDiagnosticCode;
  line: number;
  column: number;
  reference: string;
  propertyKey?: string;
}>;

/**
 * Conservative reference validation for the literal calls emitted by the Script
 * Assistant. It intentionally does not try to evaluate arbitrary Python expressions
 * or repair references to a similar object. Dynamic references remain a Python
 * concern; missing generated literal references are reported exactly as authored.
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

  for (const definition of [...catalog.screens, ...catalog.popups]) {
    for (const object of definition.objects) indexVisualObject(object, visualObjects);
  }

  for (const call of findSingleReferenceCalls(source, ['tag_read', 'tag_write'])) {
    if (!tagReferences.has(call.reference)) {
      diagnostics.push(Object.freeze({
        code: 'SCRIPT_REFERENCE_TAG_MISSING',
        line: call.line,
        column: call.column,
        reference: call.reference
      }));
    }
  }

  for (const call of findSingleReferenceCalls(source, ['client_memory_read', 'client_memory_write'])) {
    if (!clientMemoryReferences.has(call.reference)) {
      diagnostics.push(Object.freeze({
        code: 'SCRIPT_REFERENCE_CLIENT_MEMORY_MISSING',
        line: call.line,
        column: call.column,
        reference: call.reference
      }));
    }
  }

  for (const call of findVisualPropertyCalls(source)) {
    const object = visualObjects.get(call.reference);
    if (!object) {
      diagnostics.push(Object.freeze({
        code: 'SCRIPT_REFERENCE_OBJECT_MISSING',
        line: call.line,
        column: call.column,
        reference: call.reference,
        propertyKey: call.propertyKey
      }));
      continue;
    }

    if (!object.properties.some(property => property.key === call.propertyKey)) {
      diagnostics.push(Object.freeze({
        code: 'SCRIPT_REFERENCE_PROPERTY_MISSING',
        line: call.line,
        column: call.column,
        reference: call.reference,
        propertyKey: call.propertyKey
      }));
    }
  }

  return Object.freeze(diagnostics.sort((left, right) => left.line - right.line || left.column - right.column));
}

type LiteralReferenceCall = Readonly<{
  reference: string;
  line: number;
  column: number;
}>;

type VisualPropertyCall = LiteralReferenceCall & Readonly<{
  propertyKey: string;
}>;

function findSingleReferenceCalls(source: string, functions: readonly string[]): LiteralReferenceCall[] {
  const names = functions.map(escapeRegex).join('|');
  const pattern = new RegExp(`\\b(?:${names})\\s*\\(\\s*(["'])([^"'\\r\\n]*)\\1`, 'g');
  const calls: LiteralReferenceCall[] = [];
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(source)) !== null) {
    const position = sourcePosition(source, match.index);
    calls.push(Object.freeze({ reference: match[2], ...position }));
  }
  return calls;
}

function findVisualPropertyCalls(source: string): VisualPropertyCall[] {
  const pattern = /\bvisual_property_(?:read|write|clear)\s*\(\s*(["'])([^"'\r\n]*)\1\s*,\s*(["'])([^"'\r\n]*)\3/g;
  const calls: VisualPropertyCall[] = [];
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(source)) !== null) {
    const position = sourcePosition(source, match.index);
    calls.push(Object.freeze({
      reference: match[2],
      propertyKey: match[4],
      ...position
    }));
  }
  return calls;
}

function indexVisualObject(
  object: ScriptAssistantVisualObject,
  index: Map<string, ScriptAssistantVisualObject>
) {
  index.set(object.canonicalReference, object);
  for (const child of object.children) indexVisualObject(child, index);
}

function sourcePosition(source: string, index: number): { line: number; column: number } {
  const before = source.slice(0, index);
  const line = before.split('\n').length;
  const lastBreak = before.lastIndexOf('\n');
  return { line, column: index - lastBreak };
}

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
