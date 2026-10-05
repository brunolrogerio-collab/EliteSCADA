import type { VisualEditorPoint } from './visualEditorContracts';

export type BezierPathModel = Readonly<{
  tokens: readonly string[];
  points: readonly VisualEditorPoint[];
  anchorIndexes: readonly number[];
}>;

const TOKEN = /[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?/g;

/** Reads the absolute SVG path subset used by editable bezier objects. */
export function readEditableBezierPath(path: string): BezierPathModel | null {
  const matches = [...path.matchAll(TOKEN)];
  const residue = path.replace(TOKEN, '').replace(/[\s,]/g, '');
  if (residue || matches.length === 0) return null;
  const tokens = matches.map(match => match[0]);
  const points: VisualEditorPoint[] = [];
  const anchorIndexes: number[] = [];
  let command = '';
  let cursor = 0;
  while (cursor < tokens.length) {
    if (/^[A-Za-z]$/.test(tokens[cursor])) command = tokens[cursor++];
    if (!['M', 'L', 'C', 'Q', 'Z'].includes(command)) return null;
    if (command === 'Z') continue;
    const pairCount = command === 'C' ? 3 : command === 'Q' ? 2 : 1;
    if (cursor + pairCount * 2 > tokens.length) return null;
    for (let pair = 0; pair < pairCount; pair++) {
      const x = Number(tokens[cursor + pair * 2]);
      const y = Number(tokens[cursor + pair * 2 + 1]);
      if (!Number.isFinite(x) || !Number.isFinite(y)) return null;
      points.push(Object.freeze({ x, y }));
    }
    anchorIndexes.push(points.length - 1);
    cursor += pairCount * 2;
    if (command === 'M') command = 'L';
  }
  return points.length ? Object.freeze({
    tokens: Object.freeze(tokens),
    points: Object.freeze(points),
    anchorIndexes: Object.freeze(anchorIndexes)
  }) : null;
}

export function writeBezierPathPoint(model: BezierPathModel, pointIndex: number, point: VisualEditorPoint): string | null {
  if (pointIndex < 0 || pointIndex >= model.points.length) return null;
  const tokens = [...model.tokens];
  let cursor = 0;
  let command = '';
  let activePoint = 0;
  while (cursor < tokens.length) {
    if (/^[A-Za-z]$/.test(tokens[cursor])) command = tokens[cursor++];
    if (command === 'Z') continue;
    const pairCount = command === 'C' ? 3 : command === 'Q' ? 2 : 1;
    for (let pair = 0; pair < pairCount; pair++, activePoint++) {
      if (activePoint === pointIndex) {
        tokens[cursor + pair * 2] = compact(point.x);
        tokens[cursor + pair * 2 + 1] = compact(point.y);
        return tokens.join(' ');
      }
    }
    cursor += pairCount * 2;
    if (command === 'M') command = 'L';
  }
  return null;
}

/**
 * Inserts an anchor by splitting the final drawable segment at t=0.5. De Casteljau
 * subdivision preserves the original curve exactly (up to path serialization),
 * so adding an editable point does not visibly reshape the object.
 */
export function insertBezierAnchor(path: string): string | null {
  const commands = parsePath(path);
  if (!commands) return null;
  const lastDrawable = commands.map((command, index) =>
    command.kind === 'L' || command.kind === 'Q' || command.kind === 'C' ? index : -1
  ).filter(index => index >= 0).at(-1) ?? -1;
  if (lastDrawable < 0) return null;
  let anchor: VisualEditorPoint | null = null;
  for (let index = 0; index < lastDrawable; index++) {
    const command = commands[index];
    if (command.kind === 'M') anchor = command.points[0] ?? null;
    else if (command.kind !== 'Z') anchor = command.points.at(-1) ?? anchor;
  }
  if (!anchor) return null;

  const segment = commands[lastDrawable];
  const points = segment.points;
  if (segment.kind === 'L') {
    const end = points[0];
    const midpoint = lerp(anchor, end, 0.5);
    commands.splice(lastDrawable, 1, { kind: 'L', points: [midpoint] }, { kind: 'L', points: [end] });
  } else if (segment.kind === 'Q') {
    const [control, end] = points;
    const firstControl = lerp(anchor, control, 0.5);
    const secondControl = lerp(control, end, 0.5);
    const midpoint = lerp(firstControl, secondControl, 0.5);
    commands.splice(lastDrawable, 1,
      { kind: 'Q', points: [firstControl, midpoint] },
      { kind: 'Q', points: [secondControl, end] });
  } else if (segment.kind === 'C') {
    const [control1, control2, end] = points;
    const first = lerp(anchor, control1, 0.5);
    const middle = lerp(control1, control2, 0.5);
    const last = lerp(control2, end, 0.5);
    const leftControl2 = lerp(first, middle, 0.5);
    const rightControl1 = lerp(middle, last, 0.5);
    const midpoint = lerp(leftControl2, rightControl1, 0.5);
    commands.splice(lastDrawable, 1,
      { kind: 'C', points: [first, leftControl2, midpoint] },
      { kind: 'C', points: [rightControl1, last, end] });
  } else {
    return null;
  }
  return serializePath(commands);
}

/** Removes the final anchor/segment while retaining the path's initial point. */
export function removeBezierAnchor(path: string): string | null {
  const commands = parsePath(path);
  if (!commands) return null;
  const drawableIndices = commands
    .map((command, index) => command.kind === 'L' || command.kind === 'Q' || command.kind === 'C' ? index : -1)
    .filter(index => index >= 0);
  if (drawableIndices.length <= 1) return null;
  commands.splice(drawableIndices.at(-1)!, 1);
  return serializePath(commands);
}

type ParsedCommand = Readonly<{ kind: 'M' | 'L' | 'Q' | 'C'; points: readonly VisualEditorPoint[] }>
  | Readonly<{ kind: 'Z'; points: readonly [] }>;

function parsePath(path: string): ParsedCommand[] | null {
  const matches = [...path.matchAll(TOKEN)];
  const residue = path.replace(TOKEN, '').replace(/[\s,]/g, '');
  if (residue || matches.length === 0) return null;
  const tokens = matches.map(match => match[0]);
  const commands: ParsedCommand[] = [];
  let command: string | null = null;
  let cursor = 0;
  while (cursor < tokens.length) {
    if (/^[A-Za-z]$/.test(tokens[cursor])) {
      const next = tokens[cursor++];
      if (!['M', 'L', 'Q', 'C', 'Z'].includes(next)) return null;
      command = next;
      if (command === 'Z') {
        commands.push({ kind: 'Z', points: [] });
        command = null;
        continue;
      }
    }
    if (!command) return null;
    if (!['M', 'L', 'Q', 'C'].includes(command)) return null;
    const drawingCommand = command as 'M' | 'L' | 'Q' | 'C';
    const count = drawingCommand === 'C' ? 3 : drawingCommand === 'Q' ? 2 : 1;
    if (cursor + count * 2 > tokens.length) return null;
    const points: VisualEditorPoint[] = [];
    for (let index = 0; index < count; index++) {
      const x = Number(tokens[cursor + index * 2]);
      const y = Number(tokens[cursor + index * 2 + 1]);
      if (!Number.isFinite(x) || !Number.isFinite(y)) return null;
      points.push(Object.freeze({ x, y }));
    }
    cursor += count * 2;
    commands.push(Object.freeze({ kind: drawingCommand, points: Object.freeze(points) }) as ParsedCommand);
    if (drawingCommand === 'M') command = 'L';
  }
  return commands.length > 0 && commands[0].kind === 'M' ? commands : null;
}

function serializePath(commands: readonly ParsedCommand[]): string {
  return commands.map(command => command.kind === 'Z'
    ? 'Z'
    : `${command.kind} ${command.points.map(point => `${compact(point.x)} ${compact(point.y)}`).join(' ')}`)
    .join(' ');
}

function lerp(left: VisualEditorPoint, right: VisualEditorPoint, t: number): VisualEditorPoint {
  return Object.freeze({ x: left.x + (right.x - left.x) * t, y: left.y + (right.y - left.y) * t });
}

function compact(value: number): string {
  return Number(value.toFixed(3)).toString();
}
